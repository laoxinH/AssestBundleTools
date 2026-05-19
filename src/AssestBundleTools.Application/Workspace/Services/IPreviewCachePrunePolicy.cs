using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 为预览缓存保存后的自动容量治理提供策略。
/// </summary>
public interface IPreviewCachePrunePolicy
{
    PreviewCachePruneRequestDto? CreateRequest(string workspaceKey, string protectedPreviewCacheKey);
}

public sealed class NoOpPreviewCachePrunePolicy : IPreviewCachePrunePolicy
{
    public static NoOpPreviewCachePrunePolicy Instance { get; } = new();

    private NoOpPreviewCachePrunePolicy()
    {
    }

    public PreviewCachePruneRequestDto? CreateRequest(string workspaceKey, string protectedPreviewCacheKey) => null;
}
