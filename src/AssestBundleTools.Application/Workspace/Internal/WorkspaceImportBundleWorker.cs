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
        WorkspaceImportBundleCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var discoveredFile = candidate.File;
        var decryptor = candidate.Decryptor;

        var bundleCacheKey = WorkspaceImportPathUtility.CreateBundleCacheKey(discoveredFile.SourcePath);
        var sourceFingerprint = plan.SourceFingerprintsByPath.TryGetValue(discoveredFile.SourcePath, out var plannedFingerprint)
            ? plannedFingerprint
            : _workspaceImportFileSystemService.CreateFingerprint(discoveredFile.SourcePath);
        plan.ExistingCacheEntriesByBundleCacheKey.TryGetValue(bundleCacheKey, out var existingCacheEntry);

        var cacheDecision = ResolveCacheDecision(
            discoveredFile,
            bundleCacheKey,
            sourceFingerprint,
            existingCacheEntry,
            plan,
            decryptor);
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
                IssueDiagnostic: null,
                RecognitionCacheEntryToSave: candidate.CanPersistRecognition
                    ? CreateRecognitionCacheEntry(
                        plan,
                        discoveredFile,
                        bundleCacheKey,
                        sourceFingerprint,
                        decryptor,
                        WorkspaceImportRecognitionDisposition.Candidate)
                    : null);
        }

        var artifactResult = await WorkspaceBundleArtifactResolver.ResolveAsync(
            plan.BundleArtifactStore,
            decryptor,
            plan.Command.WorkspaceKey,
            plan.Command.WorkspaceDataRootPath,
            bundleCacheKey,
            discoveredFile.SourcePath,
            sourceFingerprint,
            plan.ProcessedAtUtc,
            cancellationToken,
            plan.BundleArtifactSessionInvalidator,
            plan.BundleArtifactCachePrunePolicy);
        if (artifactResult.IsCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!artifactResult.IsSuccess || artifactResult.Value is null)
        {
            return CreateFailureResult(
                discoveredFile,
                bundleCacheKey,
                cacheDecision.Diagnostic,
                existingCacheEntry is not null,
                WorkspaceImportIssueKind.DecryptionFailed,
                decryptor.Descriptor.Id,
                detailMessage: null,
                artifactResult.Errors);
        }

        if (!_assetBundleReader.CanRead(artifactResult.Value.ResolvedBundlePath))
        {
            plan.BundleArtifactStore.Delete(plan.Command.WorkspaceKey, bundleCacheKey);
            plan.BundleArtifactSessionInvalidator.InvalidateBundleArtifact(plan.Command.WorkspaceKey, bundleCacheKey);
            return CreateSkippedResult(
                discoveredFile,
                bundleCacheKey,
                cacheDecision.Diagnostic,
                existingCacheEntry is not null,
                decryptorId: artifactResult.Value.Decryptor.Id,
                resolvedBundlePath: artifactResult.Value.ResolvedBundlePath,
                recognitionCacheEntry: candidate.CanPersistRecognition
                    ? CreateRecognitionCacheEntry(
                        plan,
                        discoveredFile,
                        bundleCacheKey,
                        sourceFingerprint,
                        decryptor,
                        WorkspaceImportRecognitionDisposition.ReaderRejected,
                        WorkspaceImportIssueKind.ReaderRejected,
                        new LocalizedMessageDto(
                            WorkspaceImportMessageTokens.ReaderNoRegisteredReaderRecognizedOutput,
                            [artifactResult.Value.ResolvedBundlePath]))
                    : null);
        }

        var readResult = await _assetBundleReader.ReadAsync(
            new AssetBundleReadRequestDto(
                BundlePath: artifactResult.Value.ResolvedBundlePath,
                LogicalPath: discoveredFile.LogicalPath,
                WorkspaceKey: plan.Command.WorkspaceKey,
                WorkspaceDataRootPath: plan.Command.WorkspaceDataRootPath,
                BundleSessionCacheKey: artifactResult.Value.BundleSessionCacheKey),
            cancellationToken);
        if (readResult.IsCancelled)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        if (!readResult.IsSuccess || readResult.Value is null)
        {
            plan.BundleArtifactStore.Delete(plan.Command.WorkspaceKey, bundleCacheKey);
            plan.BundleArtifactSessionInvalidator.InvalidateBundleArtifact(plan.Command.WorkspaceKey, bundleCacheKey);
            return CreateFailureResult(
                discoveredFile,
                bundleCacheKey,
                cacheDecision.Diagnostic,
                existingCacheEntry is not null,
                WorkspaceImportIssueKind.ReadFailed,
                artifactResult.Value.Decryptor.Id,
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
                artifactResult.Value,
                readResult.Value),
            IssueDiagnostic: null,
            RecognitionCacheEntryToSave: candidate.CanPersistRecognition
                ? CreateRecognitionCacheEntry(
                    plan,
                    discoveredFile,
                    bundleCacheKey,
                    sourceFingerprint,
                    decryptor,
                    WorkspaceImportRecognitionDisposition.Candidate)
                : null);
    }

    private CacheDecision ResolveCacheDecision(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        BundleIndexCacheEntryDto? existingCacheEntry,
        WorkspaceImportProcessingPlan plan,
        IAssetBundleDecryptor decryptor)
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
            decryptor.Descriptor,
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
                Errors: errors),
            RecognitionCacheEntryToSave: null);
    }

    private static WorkspaceImportBundleWorkResult CreateSkippedResult(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceImportCacheDecisionDiagnosticDto cacheDiagnostic,
        bool shouldDeleteExistingArtifacts,
        string? decryptorId,
        string resolvedBundlePath,
        WorkspaceImportRecognitionCacheEntryDto? recognitionCacheEntry)
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
                Errors: Array.Empty<AppError>()),
            RecognitionCacheEntryToSave: recognitionCacheEntry);
    }

    private static WorkspaceImportRecognitionCacheEntryDto CreateRecognitionCacheEntry(
        WorkspaceImportProcessingPlan plan,
        DiscoveredWorkspaceImportFileDto file,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        IAssetBundleDecryptor decryptor,
        WorkspaceImportRecognitionDisposition disposition,
        WorkspaceImportIssueKind? issueKind = null,
        LocalizedMessageDto? detailMessage = null) =>
        new(
            plan.Command.WorkspaceKey,
            bundleCacheKey,
            file.SourcePath,
            file.LogicalPath,
            sourceFingerprint,
            plan.RecognitionVersion,
            plan.CodecCatalogFingerprint,
            disposition,
            decryptor.Descriptor.Id,
            decryptor.Descriptor.Version,
            disposition == WorkspaceImportRecognitionDisposition.ReaderRejected
                ? plan.ReaderParserVersion
                : null,
            issueKind,
            detailMessage,
            plan.ProcessedAtUtc);

    private static BundleIndexCacheEntryDto CreateCacheEntry(
        WorkspaceImportProcessingPlan plan,
        DiscoveredWorkspaceImportFileDto discoveredFile,
        string bundleCacheKey,
        WorkspaceFileFingerprintDto sourceFingerprint,
        WorkspaceBundleArtifactResolutionDto artifactResolution,
        AssetBundleReadResultDto readResult)
    {
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: plan.Command.WorkspaceKey,
            BundleCacheKey: bundleCacheKey,
            BundleId: WorkspaceImportPathUtility.CreateBundleId(discoveredFile.SourcePath),
            DisplayName: ResolveImportedBundleDisplayName(discoveredFile, readResult),
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
                            AssetTypeDisplayName: dependency.AssetTypeDisplayName,
                            SerializedFileName: dependency.SerializedFileName,
                            ExternalReferencePath: dependency.ExternalReferencePath))
                        .ToArray(),
                    IncludeInAssetList: asset.IncludeInAssetList))
                .ToArray(),
            DecryptorId: artifactResolution.Decryptor.Id,
            DecryptorVersion: artifactResolution.Decryptor.Version,
            SerializedFileNames: readResult.SerializedFileNames,
            OriginalCompression: readResult.OriginalCompression);
    }

    private static string ResolveImportedBundleDisplayName(
        DiscoveredWorkspaceImportFileDto discoveredFile,
        AssetBundleReadResultDto readResult)
    {
        return TryGetLeafName(discoveredFile.SourcePath) ??
               TryGetLeafName(discoveredFile.LogicalPath) ??
               readResult.DisplayName;
    }

    private static string? TryGetLeafName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim().Replace('\\', '/');
        var leafName = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(leafName) ? null : leafName;
    }

    private sealed record CacheDecision(
        bool CanReuse,
        WorkspaceImportCacheDecisionDiagnosticDto Diagnostic);
}
