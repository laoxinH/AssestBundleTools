namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 在标准化 bundle artifact 被删除或淘汰时，使进程内 loaded-session 缓存失效。
/// </summary>
public interface IBundleArtifactSessionInvalidator
{
    void InvalidateBundleArtifact(string workspaceKey, string bundleCacheKey);

    void InvalidateWorkspace(string workspaceKey);
}

public sealed class NoOpBundleArtifactSessionInvalidator : IBundleArtifactSessionInvalidator
{
    public static NoOpBundleArtifactSessionInvalidator Instance { get; } = new();

    private NoOpBundleArtifactSessionInvalidator()
    {
    }

    public void InvalidateBundleArtifact(string workspaceKey, string bundleCacheKey)
    {
    }

    public void InvalidateWorkspace(string workspaceKey)
    {
    }
}
