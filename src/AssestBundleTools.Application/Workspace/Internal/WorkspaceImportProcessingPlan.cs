using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed record WorkspaceImportProcessingPlan(
    ProcessWorkspaceImportsCommand Command,
    IWorkspaceMetadataStore MetadataStore,
    WorkspaceMetadataDto Metadata,
    IBundleArtifactStore BundleArtifactStore,
    IBundleIndexCacheStore BundleIndexCacheStore,
    IWorkspaceImportRecognitionCacheStore RecognitionCacheStore,
    IPreviewCacheStore PreviewCacheStore,
    DateTimeOffset ProcessedAtUtc,
    IReadOnlyList<WorkspaceImportedSourceDto> ImportedSources,
    IReadOnlyList<DiscoveredWorkspaceImportFileDto> DiscoveredFiles,
    WorkspaceImportCandidateScreeningResult DecryptorResolution,
    IReadOnlyDictionary<string, WorkspaceFileFingerprintDto> SourceFingerprintsByPath,
    IReadOnlyList<WorkspaceImportRecognitionCacheEntryDto> RecognitionCacheEntriesToSave,
    IReadOnlyDictionary<string, WorkspaceImportRecognitionCacheEntryDto> ExistingRecognitionEntriesByBundleCacheKey,
    IReadOnlyList<WorkspaceImportBundleWorkResult> CachedWorkerResults,
    int RecognitionCacheHitCount,
    int RecognitionScreeningInputFileCount,
    IReadOnlySet<string> CachedStableInvalidSourcePaths,
    long ScreeningDurationMs,
    string CurrentToolVersion,
    string RecognitionVersion,
    string CodecCatalogFingerprint,
    string ReaderParserVersion,
    IReadOnlyDictionary<string, BundleIndexCacheEntryDto> ExistingCacheEntriesByBundleCacheKey,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PreviewCacheKeysByBundleCacheKey,
    WorkspaceImportExecutionOptions ExecutionOptions,
    IBundleArtifactSessionInvalidator BundleArtifactSessionInvalidator,
    IBundleArtifactCachePrunePolicy BundleArtifactCachePrunePolicy);
