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
    IPreviewCacheStore PreviewCacheStore,
    DateTimeOffset ProcessedAtUtc,
    IReadOnlyList<WorkspaceImportedSourceDto> ImportedSources,
    IReadOnlyList<DiscoveredWorkspaceImportFileDto> DiscoveredFiles,
    WorkspaceImportCandidateScreeningResult DecryptorResolution,
    long ScreeningDurationMs,
    string CurrentToolVersion,
    IReadOnlyDictionary<string, BundleIndexCacheEntryDto> ExistingCacheEntriesByBundleCacheKey,
    IReadOnlyDictionary<string, IReadOnlyList<string>> PreviewCacheKeysByBundleCacheKey,
    WorkspaceImportExecutionOptions ExecutionOptions,
    IBundleArtifactSessionInvalidator BundleArtifactSessionInvalidator,
    IBundleArtifactCachePrunePolicy BundleArtifactCachePrunePolicy);
