using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 为标准化 bundle artifact 保存后的自动容量治理提供策略。
/// </summary>
public interface IBundleArtifactCachePrunePolicy
{
    BundleArtifactPruneRequestDto? CreateRequest(string workspaceKey, string protectedBundleCacheKey);
}

public sealed class NoOpBundleArtifactCachePrunePolicy : IBundleArtifactCachePrunePolicy
{
    public static NoOpBundleArtifactCachePrunePolicy Instance { get; } = new();

    private NoOpBundleArtifactCachePrunePolicy()
    {
    }

    public BundleArtifactPruneRequestDto? CreateRequest(string workspaceKey, string protectedBundleCacheKey) => null;
}
