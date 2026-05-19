using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

internal enum WorkspaceBundleArtifactExistingInputStatus
{
    Resolved,
    MissingManifest,
    StaleManifest,
    MissingBundlePath
}

internal sealed record WorkspaceBundleArtifactInputDto(
    string ResolvedBundlePath,
    BundleArtifactManifestDto Manifest,
    string BundleSessionCacheKey);

internal sealed record WorkspaceBundleArtifactExistingInputResolutionDto(
    WorkspaceBundleArtifactExistingInputStatus Status,
    WorkspaceBundleArtifactInputDto? Input);
