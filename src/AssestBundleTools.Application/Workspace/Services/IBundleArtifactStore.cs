using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责读写单工作区下可复用的标准化资源包物理缓存。
/// </summary>
public interface IBundleArtifactStore
{
    BundleArtifactManifestDto? Load(string workspaceKey, string bundleCacheKey);

    string? ResolveBundlePath(BundleArtifactManifestDto manifest);

    BundleArtifactManifestDto Save(BundleArtifactSaveRequestDto request);

    BundleArtifactManifestDto UpdateLastAccessed(BundleArtifactManifestDto manifest, DateTimeOffset lastAccessedAtUtc);

    BundleArtifactCacheUsageDto GetUsage(string? workspaceKey = null);

    BundleArtifactPruneResultDto Prune(BundleArtifactPruneRequestDto request);

    void Delete(string workspaceKey, string bundleCacheKey);

    void DeleteWorkspace(string workspaceKey);
}
