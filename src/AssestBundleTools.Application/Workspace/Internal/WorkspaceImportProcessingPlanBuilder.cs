using System.Diagnostics;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportProcessingPlanBuilder
{
    private const string ToolSchemaVersion = "workspace-cache/preview-1";
    private const string RecognitionSchemaVersion = "workspace-import-recognition/1";

    private readonly IWorkspaceMetadataStoreFactory _workspaceMetadataStoreFactory;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IWorkspaceImportSourceDiscoveryService _workspaceImportSourceDiscoveryService;
    private readonly WorkspaceImportCandidateScreeningCoordinator _workspaceImportCandidateScreeningCoordinator;
    private readonly WorkspaceImportRecognitionPlanner _workspaceImportRecognitionPlanner;
    private readonly IAssetBundleReader _assetBundleReader;
    private readonly IWorkspaceImportExecutionOptionsResolver _workspaceImportExecutionOptionsResolver;
    private readonly IBundleArtifactSessionInvalidator _bundleArtifactSessionInvalidator;
    private readonly IBundleArtifactCachePrunePolicy _bundleArtifactCachePrunePolicy;

    public WorkspaceImportProcessingPlanBuilder(
        IWorkspaceMetadataStoreFactory workspaceMetadataStoreFactory,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IWorkspaceImportSourceDiscoveryService workspaceImportSourceDiscoveryService,
        IWorkspaceImportFileSystemService workspaceImportFileSystemService,
        WorkspaceImportCandidateScreeningCoordinator workspaceImportCandidateScreeningCoordinator,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IAssetBundleReader assetBundleReader,
        IWorkspaceImportExecutionOptionsResolver workspaceImportExecutionOptionsResolver,
        IBundleArtifactSessionInvalidator bundleArtifactSessionInvalidator,
        IBundleArtifactCachePrunePolicy bundleArtifactCachePrunePolicy)
    {
        _workspaceMetadataStoreFactory = workspaceMetadataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceMetadataStoreFactory));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _workspaceImportSourceDiscoveryService = workspaceImportSourceDiscoveryService ?? throw new ArgumentNullException(nameof(workspaceImportSourceDiscoveryService));
        _workspaceImportCandidateScreeningCoordinator = workspaceImportCandidateScreeningCoordinator ?? throw new ArgumentNullException(nameof(workspaceImportCandidateScreeningCoordinator));
        var decryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
        _assetBundleReader = assetBundleReader ?? throw new ArgumentNullException(nameof(assetBundleReader));
        _workspaceImportRecognitionPlanner = new WorkspaceImportRecognitionPlanner(
            workspaceImportFileSystemService ?? throw new ArgumentNullException(nameof(workspaceImportFileSystemService)),
            decryptorRegistry,
            _assetBundleReader);
        _workspaceImportExecutionOptionsResolver = workspaceImportExecutionOptionsResolver ?? throw new ArgumentNullException(nameof(workspaceImportExecutionOptionsResolver));
        _bundleArtifactSessionInvalidator = bundleArtifactSessionInvalidator ?? throw new ArgumentNullException(nameof(bundleArtifactSessionInvalidator));
        _bundleArtifactCachePrunePolicy = bundleArtifactCachePrunePolicy ?? throw new ArgumentNullException(nameof(bundleArtifactCachePrunePolicy));
    }

    public async Task<Result<WorkspaceImportProcessingPlan>> BuildAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken)
    {
        var metadataStore = _workspaceMetadataStoreFactory.Create(command.WorkspaceDataRootPath);
        var metadata = metadataStore.Load(command.WorkspaceKey);
        if (metadata is null)
        {
            return Result<WorkspaceImportProcessingPlan>.Failure(
                ApplicationErrors.WorkspaceImportMetadataNotFound(command.WorkspaceKey));
        }

        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(command.WorkspaceDataRootPath);
        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(command.WorkspaceDataRootPath);
        var recognitionCacheStore = _workspaceDataStoreFactory.CreateWorkspaceImportRecognitionCacheStore(command.WorkspaceDataRootPath);
        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(command.WorkspaceDataRootPath);
        var importedSources = metadata.ImportedSources ?? Array.Empty<WorkspaceImportedSourceDto>();
        var discoveredFiles = _workspaceImportSourceDiscoveryService.Discover(
            importedSources,
            metadata.ExcludedBundleSourcePaths);
        var currentToolVersion = ResolveVersion(ToolSchemaVersion);
        var recognitionVersion = ResolveVersion(RecognitionSchemaVersion);
        var existingIndexEntries = bundleIndexCacheStore.LoadAll(command.WorkspaceKey)
            .ToDictionary(static entry => entry.BundleCacheKey, StringComparer.OrdinalIgnoreCase);
        var existingRecognitionEntries = recognitionCacheStore.LoadAll(command.WorkspaceKey)
            .ToDictionary(static entry => entry.BundleCacheKey, StringComparer.OrdinalIgnoreCase);
        var recognitionPlan = _workspaceImportRecognitionPlanner.Prepare(
            metadata.AssetBundleDecryptorId ?? string.Empty,
            discoveredFiles,
            existingIndexEntries,
            existingRecognitionEntries,
            recognitionVersion,
            currentToolVersion);

        var screeningStopwatch = Stopwatch.StartNew();
        var screeningResult = await _workspaceImportCandidateScreeningCoordinator.ScreenAsync(
            metadata.AssetBundleDecryptorId,
            recognitionPlan.FilesRequiringScreening,
            cancellationToken);
        screeningStopwatch.Stop();
        if (!screeningResult.IsSuccess || screeningResult.Value is null)
        {
            return Result<WorkspaceImportProcessingPlan>.Failure(screeningResult.Errors);
        }

        var mergedScreeningResult = MergeScreeningResults(screeningResult.Value, recognitionPlan);
        var processedAtUtc = DateTimeOffset.UtcNow;
        var recognitionEntriesToSave = CreateScreenedOutRecognitionEntries(
            command.WorkspaceKey,
            screeningResult.Value,
            recognitionPlan,
            recognitionVersion,
            processedAtUtc);

        return Result<WorkspaceImportProcessingPlan>.Success(
            new WorkspaceImportProcessingPlan(
                Command: command,
                MetadataStore: metadataStore,
                Metadata: metadata,
                BundleArtifactStore: bundleArtifactStore,
                BundleIndexCacheStore: bundleIndexCacheStore,
                RecognitionCacheStore: recognitionCacheStore,
                PreviewCacheStore: previewCacheStore,
                ProcessedAtUtc: processedAtUtc,
                ImportedSources: importedSources,
                DiscoveredFiles: discoveredFiles,
                DecryptorResolution: mergedScreeningResult,
                SourceFingerprintsByPath: recognitionPlan.SourceFingerprintsByPath,
                RecognitionCacheEntriesToSave: recognitionEntriesToSave,
                ExistingRecognitionEntriesByBundleCacheKey: existingRecognitionEntries,
                CachedWorkerResults: recognitionPlan.CachedWorkerResults,
                RecognitionCacheHitCount: recognitionPlan.CacheHitCount,
                RecognitionScreeningInputFileCount: recognitionPlan.FilesRequiringScreening.Count,
                NewlyScreenedOutFileCount: screeningResult.Value.ScreenedOutDiagnostics.Count,
                CachedStableInvalidSourcePaths: recognitionPlan.CachedStableInvalidSourcePaths,
                ScreeningDurationMs: screeningStopwatch.ElapsedMilliseconds,
                CurrentToolVersion: currentToolVersion,
                RecognitionVersion: recognitionVersion,
                CodecCatalogFingerprint: recognitionPlan.CodecCatalogFingerprint,
                ReaderParserVersion: _assetBundleReader.CacheParserVersion,
                ExistingCacheEntriesByBundleCacheKey: existingIndexEntries,
                PreviewCacheKeysByBundleCacheKey: BuildPreviewCacheKeysByBundle(
                    previewCacheStore.LoadDescriptors(command.WorkspaceKey)),
                ExecutionOptions: _workspaceImportExecutionOptionsResolver.ResolveOptions(),
                BundleArtifactSessionInvalidator: _bundleArtifactSessionInvalidator,
                BundleArtifactCachePrunePolicy: _bundleArtifactCachePrunePolicy));
    }

    private static WorkspaceImportCandidateScreeningResult MergeScreeningResults(
        WorkspaceImportCandidateScreeningResult freshResult,
        WorkspaceImportRecognitionPlan recognitionPlan) =>
        new(
            freshResult.Decryptor,
            freshResult.UsedAutoDetection,
            freshResult.AutoDetectionDiagnostic,
            recognitionPlan.CachedCandidates
                .Concat(freshResult.BundleCandidates)
                .OrderBy(static candidate => candidate.File.LogicalPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static candidate => candidate.File.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            recognitionPlan.CachedScreenedOutDiagnostics
                .Concat(freshResult.ScreenedOutDiagnostics)
                .ToArray(),
            freshResult.ProbeTelemetry,
            freshResult.StableRecognitionSourcePaths);

    private static IReadOnlyList<WorkspaceImportRecognitionCacheEntryDto> CreateScreenedOutRecognitionEntries(
        string workspaceKey,
        WorkspaceImportCandidateScreeningResult freshResult,
        WorkspaceImportRecognitionPlan recognitionPlan,
        string recognitionVersion,
        DateTimeOffset cachedAtUtc)
    {
        var freshSourcePaths = recognitionPlan.FilesRequiringScreening
            .Select(static file => file.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return freshResult.ScreenedOutDiagnostics
            .Where(diagnostic => freshSourcePaths.Contains(diagnostic.SourcePath) &&
                                 freshResult.CanPersistRecognition(diagnostic.SourcePath) &&
                                 recognitionPlan.SourceFingerprintsByPath.TryGetValue(diagnostic.SourcePath, out _))
            .Select(diagnostic => new WorkspaceImportRecognitionCacheEntryDto(
                workspaceKey,
                WorkspaceImportPathUtility.CreateBundleCacheKey(diagnostic.SourcePath),
                diagnostic.SourcePath,
                diagnostic.LogicalPath,
                recognitionPlan.SourceFingerprintsByPath[diagnostic.SourcePath],
                recognitionVersion,
                recognitionPlan.CodecCatalogFingerprint,
                WorkspaceImportRecognitionDisposition.ScreenedOut,
                diagnostic.DecryptorId,
                DecryptorVersion: null,
                ReaderParserVersion: null,
                diagnostic.Kind,
                diagnostic.DetailMessage,
                cachedAtUtc))
            .ToArray();
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildPreviewCacheKeysByBundle(
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

        return previewCacheKeysByBundle.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveVersion(string schemaVersion)
    {
        var assemblyVersion = typeof(WorkspaceImportProcessingPlanBuilder).Assembly.GetName().Version?.ToString()
                              ?? "1.0.0";
        return $"{assemblyVersion}+{schemaVersion}";
    }
}
