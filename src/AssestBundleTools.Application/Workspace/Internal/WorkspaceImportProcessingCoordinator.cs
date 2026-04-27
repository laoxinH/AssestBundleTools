using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 统一协调工作区导入来源到索引缓存的处理流程，供导入命令与工作区打开链路复用。
/// </summary>
internal sealed class WorkspaceImportProcessingCoordinator
{
    private const string ToolSchemaVersion = "workspace-cache/preview-1";

    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceImportSourceDiscoveryService _workspaceImportSourceDiscoveryService;
    private readonly IWorkspaceImportFileSystemService _workspaceImportFileSystemService;
    private readonly IAssetBundleReader _assetBundleReader;
    private readonly WorkspaceImportCandidateScreeningCoordinator _workspaceImportCandidateScreeningCoordinator;

    public WorkspaceImportProcessingCoordinator(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceImportSourceDiscoveryService workspaceImportSourceDiscoveryService,
        IWorkspaceImportFileSystemService workspaceImportFileSystemService,
        IAssetBundleSourceInspector assetBundleSourceInspector,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IAssetBundleReader assetBundleReader)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _workspaceImportSourceDiscoveryService = workspaceImportSourceDiscoveryService ?? throw new ArgumentNullException(nameof(workspaceImportSourceDiscoveryService));
        _workspaceImportFileSystemService = workspaceImportFileSystemService ?? throw new ArgumentNullException(nameof(workspaceImportFileSystemService));
        _assetBundleReader = assetBundleReader ?? throw new ArgumentNullException(nameof(assetBundleReader));
        _workspaceImportCandidateScreeningCoordinator = new WorkspaceImportCandidateScreeningCoordinator(
            assetBundleSourceInspector ?? throw new ArgumentNullException(nameof(assetBundleSourceInspector)),
            assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry)));
    }

    public async Task<Result<WorkspaceImportProcessingResultDto>> ProcessAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceImportProcessingResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceKey))
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(ApplicationErrors.WorkspaceImportWorkspaceKeyRequired());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceDataRootPath))
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(ApplicationErrors.WorkspaceImportDataRootPathRequired());
        }

        var metadataStore = _workspaceMetadataStoreFactory.Create(command.WorkspaceDataRootPath);
        var metadata = metadataStore.Load(command.WorkspaceKey);
        if (metadata is null)
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(
                ApplicationErrors.WorkspaceImportMetadataNotFound(command.WorkspaceKey));
        }

        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(command.WorkspaceDataRootPath);
        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(command.WorkspaceDataRootPath);
        var processedAtUtc = DateTimeOffset.UtcNow;

        var importedSources = metadata.ImportedSources ?? Array.Empty<WorkspaceImportedSourceDto>();
        var discoveredFiles = _workspaceImportSourceDiscoveryService.Discover(
            importedSources,
            metadata.ExcludedBundleSourcePaths);
        // 导入处理的自动识别主链路分成四层：
        // 1. 导入源识别：把来源展开后的文件做直检；
        // 2. 解密器识别：对无法直判的来源继续执行 Probe / 自动识别；
        // 3. Reader 路径识别：解密后再由 Reader 决定是否可读；
        // 4. 缓存命中识别：结合指纹、解密器版本和解析器版本决定复用还是重建。
        var screeningResult = await _workspaceImportCandidateScreeningCoordinator.ScreenAsync(
            metadata.AssetBundleDecryptorId,
            discoveredFiles,
            cancellationToken);
        if (!screeningResult.IsSuccess || screeningResult.Value is null)
        {
            return Result<WorkspaceImportProcessingResultDto>.Failure(screeningResult.Errors);
        }

        var decryptorResolution = screeningResult.Value;
        var decryptor = decryptorResolution.Decryptor;
        var currentToolVersion = ResolveToolVersion();
        var existingCacheEntries = bundleIndexCacheStore.LoadAll(command.WorkspaceKey)
            .ToDictionary(static entry => entry.BundleCacheKey, StringComparer.OrdinalIgnoreCase);
        var previewCacheKeysByBundle = BuildPreviewCacheKeysByBundle(previewCacheStore.LoadDescriptors(command.WorkspaceKey));
        var observedBundleCacheKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var indexedBundleCount = 0;
        var reusedBundleCount = 0;
        var skippedFileCount = 0;
        var failedFileCount = 0;
        var cacheDiagnostics = new List<WorkspaceImportCacheDecisionDiagnosticDto>();
        var issueDiagnostics = new List<WorkspaceImportIssueDiagnosticDto>(decryptorResolution.ScreenedOutDiagnostics);

        foreach (var discoveredFile in decryptorResolution.CandidateFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bundleCacheKey = WorkspaceImportPathUtility.CreateBundleCacheKey(discoveredFile.SourcePath);
            observedBundleCacheKeys.Add(bundleCacheKey);
            // 来源指纹属于缓存命中识别输入，但文件系统访问能力仍由基础设施层提供。
            var sourceFingerprint = _workspaceImportFileSystemService.CreateFingerprint(discoveredFile.SourcePath);
            if (!existingCacheEntries.TryGetValue(bundleCacheKey, out var existingCacheEntry))
            {
                cacheDiagnostics.Add(new WorkspaceImportCacheDecisionDiagnosticDto(
                    SourcePath: discoveredFile.SourcePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    BundleCacheKey: bundleCacheKey,
                    Kind: WorkspaceImportCacheDecisionKind.CacheMiss,
                    DetailMessage: new LocalizedMessageDto(WorkspaceImportMessageTokens.CacheCacheMiss)));
            }
            else
            {
                var reuseDecision = BundleIndexCacheReusePolicy.Evaluate(
                    existingCacheEntry,
                    sourceFingerprint,
                    decryptor.Descriptor,
                    currentToolVersion,
                    _assetBundleReader);
                cacheDiagnostics.Add(new WorkspaceImportCacheDecisionDiagnosticDto(
                    SourcePath: discoveredFile.SourcePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    BundleCacheKey: bundleCacheKey,
                    Kind: reuseDecision.Kind,
                    DetailMessage: reuseDecision.DetailMessage));

                if (reuseDecision.CanReuse)
                {
                    reusedBundleCount++;
                    continue;
                }
            }

            if (existingCacheEntry is not null)
            {
                DeleteBundleArtifacts(
                    command.WorkspaceKey,
                    existingCacheEntry.BundleCacheKey,
                    bundleIndexCacheStore,
                    previewCacheStore,
                    previewCacheKeysByBundle);
                existingCacheEntries.Remove(existingCacheEntry.BundleCacheKey);
            }

            var decryptionResult = await decryptor.DecryptAsync(
                new AssetBundleDecryptionRequestDto(
                    SourcePath: discoveredFile.SourcePath,
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDataRootPath: command.WorkspaceDataRootPath),
                cancellationToken);
            if (!decryptionResult.IsSuccess || decryptionResult.Value is null)
            {
                failedFileCount++;
                issueDiagnostics.Add(new WorkspaceImportIssueDiagnosticDto(
                    SourcePath: discoveredFile.SourcePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    Kind: WorkspaceImportIssueKind.DecryptionFailed,
                    DecryptorId: decryptor.Descriptor.Id,
                    DetailMessage: null,
                    Errors: decryptionResult.Errors));
                continue;
            }

            if (!_assetBundleReader.CanRead(decryptionResult.Value.ResolvedBundlePath))
            {
                skippedFileCount++;
                issueDiagnostics.Add(new WorkspaceImportIssueDiagnosticDto(
                    SourcePath: discoveredFile.SourcePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    Kind: WorkspaceImportIssueKind.ReaderRejected,
                    DecryptorId: decryptionResult.Value.Decryptor.Id,
                    DetailMessage: CreateReaderRejectedDetail(decryptionResult.Value.ResolvedBundlePath),
                    Errors: Array.Empty<AppError>()));
                continue;
            }

            var readResult = await _assetBundleReader.ReadAsync(
                new AssetBundleReadRequestDto(
                    BundlePath: decryptionResult.Value.ResolvedBundlePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    WorkspaceKey: command.WorkspaceKey,
                    WorkspaceDataRootPath: command.WorkspaceDataRootPath),
                cancellationToken);
            if (!readResult.IsSuccess || readResult.Value is null)
            {
                failedFileCount++;
                issueDiagnostics.Add(new WorkspaceImportIssueDiagnosticDto(
                    SourcePath: discoveredFile.SourcePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    Kind: WorkspaceImportIssueKind.ReadFailed,
                    DecryptorId: decryptionResult.Value.Decryptor.Id,
                    DetailMessage: null,
                    Errors: readResult.Errors));
                continue;
            }

            var cacheEntry = new BundleIndexCacheEntryDto(
                WorkspaceKey: command.WorkspaceKey,
                BundleCacheKey: bundleCacheKey,
                BundleId: WorkspaceImportPathUtility.CreateBundleId(discoveredFile.SourcePath),
                DisplayName: readResult.Value.DisplayName,
                LogicalPath: readResult.Value.LogicalPath,
                SourceFingerprint: sourceFingerprint,
                CachedAtUtc: processedAtUtc,
                ParserVersion: readResult.Value.ParserVersion,
                ToolVersion: currentToolVersion,
                Dependencies: readResult.Value.Dependencies,
                Assets: readResult.Value.Assets
                    .Select(static asset => new BundleIndexAssetCacheEntryDto(
                        AssetId: asset.AssetId,
                        AssetName: asset.AssetName,
                        AssetType: asset.AssetType,
                        AssetPath: asset.AssetPath,
                        ContainerPath: asset.ContainerPath,
                        SizeInBytes: asset.SizeInBytes,
                        PreferredDisplaySizeInBytes: asset.PreferredDisplaySizeInBytes,
                        HasPreview: asset.HasPreview,
                        CanExtract: asset.CanExtract,
                        CanReplace: asset.CanReplace,
                        SerializedFileName: asset.SerializedFileName,
                        PathId: asset.PathId,
                        AssetTypeDisplayName: asset.AssetTypeDisplayName,
                        Dependencies: asset.Dependencies?
                            .Select(static dependency => new BundleIndexAssetDependencyCacheEntryDto(
                                AssetId: dependency.AssetId,
                                AssetName: dependency.AssetName,
                                AssetType: dependency.AssetType,
                                PathId: dependency.PathId,
                                AssetTypeDisplayName: dependency.AssetTypeDisplayName))
                            .ToArray()))
                    .ToArray(),
                DecryptorId: decryptionResult.Value.Decryptor.Id,
                DecryptorVersion: decryptionResult.Value.Decryptor.Version,
                SerializedFileNames: readResult.Value.SerializedFileNames);

            bundleIndexCacheStore.Save(cacheEntry);
            existingCacheEntries[bundleCacheKey] = cacheEntry;
            indexedBundleCount++;
        }

        foreach (var staleCacheEntry in existingCacheEntries.Values
                     .Where(entry => !observedBundleCacheKeys.Contains(entry.BundleCacheKey))
                     .ToArray())
        {
            cacheDiagnostics.Add(new WorkspaceImportCacheDecisionDiagnosticDto(
                SourcePath: staleCacheEntry.SourceFingerprint.SourcePath,
                LogicalPath: staleCacheEntry.LogicalPath,
                BundleCacheKey: staleCacheEntry.BundleCacheKey,
                Kind: WorkspaceImportCacheDecisionKind.SourceRemoved,
                DetailMessage: new LocalizedMessageDto(WorkspaceImportMessageTokens.CacheSourceRemoved)));
            DeleteBundleArtifacts(
                command.WorkspaceKey,
                staleCacheEntry.BundleCacheKey,
                bundleIndexCacheStore,
                previewCacheStore,
                previewCacheKeysByBundle);
        }

        // 自动识别是工作区级策略而不是单次 UI 临时状态。
        // 只有当这轮导入确实成功建立或复用了有效索引时，才把解析出的真实解密器回写到工作区元数据，
        // 避免把一次失败探测误固化成长期配置。
        if (decryptorResolution.ShouldPersistResolvedDecryptorId &&
            indexedBundleCount + reusedBundleCount > 0 &&
            !string.Equals(metadata.AssetBundleDecryptorId, decryptor.Descriptor.Id, StringComparison.OrdinalIgnoreCase))
        {
            metadataStore.Save(metadata with
            {
                AssetBundleDecryptorId = decryptor.Descriptor.Id
            });
        }

        return Result<WorkspaceImportProcessingResultDto>.Success(
            new WorkspaceImportProcessingResultDto(
                WorkspaceKey: metadata.WorkspaceKey,
                WorkspaceDisplayName: metadata.DisplayName,
                ResolvedDecryptorId: decryptor.Descriptor.Id,
                UsedAutoDetection: decryptorResolution.UsedAutoDetection,
                AutoDetectionDiagnostic: decryptorResolution.AutoDetectionDiagnostic,
                RegisteredSourceCount: importedSources.Count,
                DiscoveredFileCount: discoveredFiles.Count,
                ScreenedOutFileCount: decryptorResolution.ScreenedOutDiagnostics.Count,
                IndexedBundleCount: indexedBundleCount,
                ReusedBundleCount: reusedBundleCount,
                SkippedFileCount: skippedFileCount,
                FailedFileCount: failedFileCount,
                CacheDiagnostics: cacheDiagnostics,
                IssueDiagnostics: issueDiagnostics,
                ProcessedAtUtc: processedAtUtc));
    }
    private static LocalizedMessageDto CreateReaderRejectedDetail(string resolvedBundlePath)
    {
        return new LocalizedMessageDto(
            WorkspaceImportMessageTokens.ReaderNoRegisteredReaderRecognizedOutput,
            [resolvedBundlePath]);
    }

    private static Dictionary<string, List<string>> BuildPreviewCacheKeysByBundle(
        IReadOnlyList<PreviewCacheDescriptorDto> previewDescriptors)
    {
        var previewCacheKeysByBundle = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var previewDescriptor in previewDescriptors)
        {
            if (string.IsNullOrWhiteSpace(previewDescriptor.BundleCacheKey) ||
                string.IsNullOrWhiteSpace(previewDescriptor.PreviewCacheKey))
            {
                continue;
            }

            if (!previewCacheKeysByBundle.TryGetValue(previewDescriptor.BundleCacheKey, out var previewCacheKeys))
            {
                previewCacheKeys = new List<string>();
                previewCacheKeysByBundle[previewDescriptor.BundleCacheKey] = previewCacheKeys;
            }

            previewCacheKeys.Add(previewDescriptor.PreviewCacheKey);
        }

        return previewCacheKeysByBundle;
    }

    private static void DeleteBundleArtifacts(
        string workspaceKey,
        string bundleCacheKey,
        IBundleIndexCacheStore bundleIndexCacheStore,
        IPreviewCacheStore previewCacheStore,
        IDictionary<string, List<string>> previewCacheKeysByBundle)
    {
        WorkspaceBundleArtifactCleanupHelper.DeleteBundleArtifacts(
            workspaceKey,
            bundleCacheKey,
            bundleIndexCacheStore,
            previewCacheStore,
            previewCacheKeysByBundle.TryGetValue(bundleCacheKey, out var previewCacheKeys)
                ? previewCacheKeys
                : Array.Empty<string>());

        previewCacheKeysByBundle.Remove(bundleCacheKey);
    }

    private static string ResolveToolVersion()
    {
        var assemblyVersion = typeof(WorkspaceImportProcessingCoordinator).Assembly.GetName().Version?.ToString()
                              ?? "1.0.0";
        return $"{assemblyVersion}+{ToolSchemaVersion}";
    }
}
