using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed class WorkspaceImportBundleWorker
{
    private readonly IWorkspaceImportFileSystemService _workspaceImportFileSystemService;
    private readonly IAssetBundleReader _assetBundleReader;

    public WorkspaceImportBundleWorker(
        IWorkspaceImportFileSystemService workspaceImportFileSystemService,
        IAssetBundleReader assetBundleReader)
    {
        _workspaceImportFileSystemService = workspaceImportFileSystemService ?? throw new ArgumentNullException(nameof(workspaceImportFileSystemService));
        _assetBundleReader = assetBundleReader ?? throw new ArgumentNullException(nameof(assetBundleReader));
    }

    public async Task<WorkspaceImportBundleWorkResult> ExecuteAsync(
        WorkspaceImportProcessingPlan plan,
        DiscoveredWorkspaceImportFileDto discoveredFile,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bundleCacheKey = WorkspaceImportPathUtility.CreateBundleCacheKey(discoveredFile.SourcePath);
        var sourceFingerprint = _workspaceImportFileSystemService.CreateFingerprint(discoveredFile.SourcePath);
        plan.ExistingCacheEntriesByBundleCacheKey.TryGetValue(bundleCacheKey, out var existingCacheEntry);

        var cacheDecision = ResolveCacheDecision(
            discoveredFile,
            bundleCacheKey,
            sourceFingerprint,
            existingCacheEntry,
            plan);
        if (cacheDecision.CanReuse)
        {
            return new WorkspaceImportBundleWorkResult(
                BundleCacheKey: bundleCacheKey,
                SourcePath: discoveredFile.SourcePath,
                LogicalPath: discoveredFile.LogicalPath,
                ShouldDeleteExistingArtifacts: false,
                Outcome: WorkspaceImportBundleWorkOutcome.Reused,
                CacheDiagnostic: cacheDecision.Diagnostic,
                CacheEntryToSave: null,
                IssueDiagnostic: null);
        }

        var decryptor = plan.DecryptorResolution.Decryptor;
        var decryptionResult = await decryptor.DecryptAsync(
            new AssetBundleDecryptionRequestDto(
                SourcePath: discoveredFile.SourcePath,
                WorkspaceKey: plan.Command.WorkspaceKey,
                WorkspaceDataRootPath: plan.Command.WorkspaceDataRootPath,
                BundleCacheKey: bundleCacheKey),
            cancellationToken);
        if (decryptionResult.IsCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!decryptionResult.IsSuccess || decryptionResult.Value is null)
        {
            return CreateFailureResult(
                discoveredFile,
                bundleCacheKey,
                cacheDecision.Diagnostic,
                existingCacheEntry is not null,
                WorkspaceImportIssueKind.DecryptionFailed,
                decryptor.Descriptor.Id,
                detailMessage: null,
                decryptionResult.Errors);
        }

        if (!_assetBundleReader.CanRead(decryptionResult.Value.ResolvedBundlePath))
        {
            return CreateSkippedResult(
                discoveredFile,
                bundleCacheKey,
                cacheDecision.Diagnostic,
                existingCacheEntry is not null,
                decryptorId: decryptionResult.Value.Decryptor.Id,
                resolvedBundlePath: decryptionResult.Value.ResolvedBundlePath);
        }

        var readResult = await _assetBundleReader.ReadAsync(
            new AssetBundleReadRequestDto(
                BundlePath: decryptionResult.Value.ResolvedBundlePath,
                LogicalPath: discoveredFile.LogicalPath,
                WorkspaceKey: plan.Command.WorkspaceKey,
                WorkspaceDataRootPath: plan.Command.WorkspaceDataRootPath),
            cancellationToken);
        if (readResult.IsCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!readResult.IsSuccess || readResult.Value is null)
        {
            return CreateFailureResult(
                discoveredFile,
                bundleCacheKey,
                cacheDecision.Diagnostic,
                existingCacheEntry is not null,
                WorkspaceImportIssueKind.ReadFailed,
                decryptionResult.Value.Decryptor.Id,
                detailMessage: null,
                readResult.Errors);
        }

        return new WorkspaceImportBundleWorkResult(
            BundleCacheKey: bundleCacheKey,
            SourcePath: discoveredFile.SourcePath,
            LogicalPath: discoveredFile.LogicalPath,
            ShouldDeleteExistingArtifacts: existingCacheEntry is not null,
            Outcome: WorkspaceImportBundleWorkOutcome.Indexed,
            CacheDiagnostic: cacheDecision.Diagnostic,
            CacheEntryToSave: CreateCacheEntry(
                plan,
                discoveredFile,
                bundleCacheKey,
                sourceFingerprint,
                decryptionResult.Value,
                readResult.Value),
            IssueDiagnostic: null);
    }

    private CacheDecision ResolveCacheDecision(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        BundleIndexCacheEntryDto? existingCacheEntry,
        WorkspaceImportProcessingPlan plan)
    {
        if (existingCacheEntry is null)
        {
            return new CacheDecision(
                CanReuse: false,
                Diagnostic: new WorkspaceImportCacheDecisionDiagnosticDto(
                    SourcePath: discoveredFile.SourcePath,
                    LogicalPath: discoveredFile.LogicalPath,
                    BundleCacheKey: bundleCacheKey,
                    Kind: WorkspaceImportCacheDecisionKind.CacheMiss,
                    DetailMessage: new LocalizedMessageDto(WorkspaceImportMessageTokens.CacheCacheMiss)));
        }

        var reuseDecision = BundleIndexCacheReusePolicy.Evaluate(
            existingCacheEntry,
            sourceFingerprint,
            plan.DecryptorResolution.Decryptor.Descriptor,
            plan.CurrentToolVersion,
            _assetBundleReader);
        return new CacheDecision(
            CanReuse: reuseDecision.CanReuse,
            Diagnostic: new WorkspaceImportCacheDecisionDiagnosticDto(
                SourcePath: discoveredFile.SourcePath,
                LogicalPath: discoveredFile.LogicalPath,
                BundleCacheKey: bundleCacheKey,
                Kind: reuseDecision.Kind,
                DetailMessage: reuseDecision.DetailMessage));
    }

    private static WorkspaceImportBundleWorkResult CreateFailureResult(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceImportCacheDecisionDiagnosticDto cacheDiagnostic,
        bool shouldDeleteExistingArtifacts,
        WorkspaceImportIssueKind issueKind,
        string? decryptorId,
        LocalizedMessageDto? detailMessage,
        IReadOnlyList<AppError> errors)
    {
        return new WorkspaceImportBundleWorkResult(
            BundleCacheKey: bundleCacheKey,
            SourcePath: discoveredFile.SourcePath,
            LogicalPath: discoveredFile.LogicalPath,
            ShouldDeleteExistingArtifacts: shouldDeleteExistingArtifacts,
            Outcome: WorkspaceImportBundleWorkOutcome.Failed,
            CacheDiagnostic: cacheDiagnostic,
            CacheEntryToSave: null,
            IssueDiagnostic: new WorkspaceImportIssueDiagnosticDto(
                SourcePath: discoveredFile.SourcePath,
                LogicalPath: discoveredFile.LogicalPath,
                Kind: issueKind,
                DecryptorId: decryptorId,
                DetailMessage: detailMessage,
                Errors: errors));
    }

    private static WorkspaceImportBundleWorkResult CreateSkippedResult(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceImportCacheDecisionDiagnosticDto cacheDiagnostic,
        bool shouldDeleteExistingArtifacts,
        string? decryptorId,
        string resolvedBundlePath)
    {
        return new WorkspaceImportBundleWorkResult(
            BundleCacheKey: bundleCacheKey,
            SourcePath: discoveredFile.SourcePath,
            LogicalPath: discoveredFile.LogicalPath,
            ShouldDeleteExistingArtifacts: shouldDeleteExistingArtifacts,
            Outcome: WorkspaceImportBundleWorkOutcome.Skipped,
            CacheDiagnostic: cacheDiagnostic,
            CacheEntryToSave: null,
            IssueDiagnostic: new WorkspaceImportIssueDiagnosticDto(
                SourcePath: discoveredFile.SourcePath,
                LogicalPath: discoveredFile.LogicalPath,
                Kind: WorkspaceImportIssueKind.ReaderRejected,
                DecryptorId: decryptorId,
                DetailMessage: new LocalizedMessageDto(
                    WorkspaceImportMessageTokens.ReaderNoRegisteredReaderRecognizedOutput,
                    [resolvedBundlePath]),
                Errors: Array.Empty<AppError>()));
    }

    private static BundleIndexCacheEntryDto CreateCacheEntry(
        WorkspaceImportProcessingPlan plan,
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        AssetBundleDecryptionResultDto decryptionResult,
        AssetBundleReadResultDto readResult)
    {
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: plan.Command.WorkspaceKey,
            BundleCacheKey: bundleCacheKey,
            BundleId: WorkspaceImportPathUtility.CreateBundleId(discoveredFile.SourcePath),
            DisplayName: readResult.DisplayName,
            LogicalPath: readResult.LogicalPath,
            SourceFingerprint: sourceFingerprint,
            CachedAtUtc: plan.ProcessedAtUtc,
            ParserVersion: readResult.ParserVersion,
            ToolVersion: plan.CurrentToolVersion,
            Dependencies: readResult.Dependencies,
            Assets: readResult.Assets
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
            DecryptorId: decryptionResult.Decryptor.Id,
            DecryptorVersion: decryptionResult.Decryptor.Version,
            SerializedFileNames: readResult.SerializedFileNames);
    }

    private sealed record CacheDecision(
        bool CanReuse,
        WorkspaceImportCacheDecisionDiagnosticDto Diagnostic);
}
