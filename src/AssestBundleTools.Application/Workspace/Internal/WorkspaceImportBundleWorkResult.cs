using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed record WorkspaceImportBundleWorkResult(
    string BundleCacheKey,
    string SourcePath,
    string LogicalPath,
    bool ShouldDeleteExistingArtifacts,
    WorkspaceImportBundleWorkOutcome Outcome,
    WorkspaceImportCacheDecisionDiagnosticDto CacheDiagnostic,
    BundleIndexCacheEntryDto? CacheEntryToSave,
    WorkspaceImportIssueDiagnosticDto? IssueDiagnostic);

internal enum WorkspaceImportBundleWorkOutcome
{
    Reused = 0,
    Indexed = 1,
    Skipped = 2,
    Failed = 3
}
