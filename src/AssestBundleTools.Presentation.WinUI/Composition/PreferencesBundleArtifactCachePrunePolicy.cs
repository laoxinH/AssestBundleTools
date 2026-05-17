using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.Composition;

/// <summary>
/// 将应用偏好设置适配为 Application 可消费的 artifact 自动清理策略。
/// </summary>
internal sealed class PreferencesBundleArtifactCachePrunePolicy : IBundleArtifactCachePrunePolicy
{
    private readonly IAppPreferencesService _appPreferencesService;

    public PreferencesBundleArtifactCachePrunePolicy(IAppPreferencesService appPreferencesService)
    {
        _appPreferencesService = appPreferencesService ?? throw new ArgumentNullException(nameof(appPreferencesService));
    }

    public BundleArtifactPruneRequestDto? CreateRequest(string workspaceKey, string protectedBundleCacheKey)
    {
        if (!_appPreferencesService.BundleArtifactCacheAutoPruneEnabled ||
            string.IsNullOrWhiteSpace(workspaceKey))
        {
            return null;
        }

        return new BundleArtifactPruneRequestDto(
            WorkspaceKey: workspaceKey,
            MaximumSizeInBytes: _appPreferencesService.BundleArtifactCacheMaximumSizeInBytes,
            MaximumArtifactCount: null,
            ProtectedBundleCacheKeys: string.IsNullOrWhiteSpace(protectedBundleCacheKey)
                ? Array.Empty<string>()
                : [protectedBundleCacheKey]);
    }
}
