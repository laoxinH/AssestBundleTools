using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Reading;

/// <summary>
/// 将 Application 层的 artifact 失效事件桥接到 AssetsTools loaded-session 缓存。
/// </summary>
public sealed class AssetsToolsBundleArtifactSessionInvalidator : IBundleArtifactSessionInvalidator
{
    public void InvalidateBundleArtifact(string workspaceKey, string bundleCacheKey)
    {
        AssetsToolsBundleLoadedSessionCache.Shared.InvalidateBundleArtifact(workspaceKey, bundleCacheKey);
    }

    public void InvalidateWorkspace(string workspaceKey)
    {
        AssetsToolsBundleLoadedSessionCache.Shared.InvalidateWorkspace(workspaceKey);
    }
}
