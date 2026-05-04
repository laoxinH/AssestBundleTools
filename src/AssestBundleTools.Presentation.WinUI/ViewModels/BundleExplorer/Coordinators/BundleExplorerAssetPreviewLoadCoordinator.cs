using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责资产文档标签的前台预览加载与后台预热调度。
/// </summary>
internal sealed class BundleExplorerAssetPreviewLoadCoordinator
{
    private readonly BundleExplorerAssetPreviewCoordinator _assetPreviewCoordinator;
    private readonly Func<AssetDocumentTabViewModel?> _activeAssetTabProvider;
    private readonly Func<IReadOnlyList<AppError>, string> _formatFirstErrorMessage;

    public BundleExplorerAssetPreviewLoadCoordinator(
        BundleExplorerAssetPreviewCoordinator assetPreviewCoordinator,
        Func<AssetDocumentTabViewModel?> activeAssetTabProvider,
        Func<IReadOnlyList<AppError>, string> formatFirstErrorMessage)
    {
        _assetPreviewCoordinator = assetPreviewCoordinator ?? throw new ArgumentNullException(nameof(assetPreviewCoordinator));
        _activeAssetTabProvider = activeAssetTabProvider ?? throw new ArgumentNullException(nameof(activeAssetTabProvider));
        _formatFirstErrorMessage = formatFirstErrorMessage ?? throw new ArgumentNullException(nameof(formatFirstErrorMessage));
    }

    public void LoadForegroundPreview(AssetDocumentTabViewModel assetTab, bool promoteLoadingPreview)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        if (!ReferenceEquals(assetTab, _activeAssetTabProvider()) ||
            assetTab.Details is not { } details ||
            HasStablePreviewContent(assetTab.Inspector.Preview) ||
            (assetTab.Inspector.Preview.IsPreviewLoading && !promoteLoadingPreview))
        {
            return;
        }

        if (assetTab.Inspector.Preview.IsPreviewLoading)
        {
            assetTab.CancelPreviewRequest();
        }

        _ = _assetPreviewCoordinator.LoadInspectorPreviewAsync(
            assetTab,
            details,
            _formatFirstErrorMessage,
            () => ReferenceEquals(assetTab, _activeAssetTabProvider()),
            AssetPreviewLoadPriority.Foreground);
    }

    public void PrewarmBackgroundPreviews(BundleDocumentTabViewModel bundleTab, AssetDocumentTabViewModel foregroundAssetTab)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(foregroundAssetTab);

        foreach (var assetTab in bundleTab.AssetTabs)
        {
            if (ReferenceEquals(assetTab, foregroundAssetTab) ||
                assetTab.Details is not { } details ||
                HasPreviewWorkOrContent(assetTab.Inspector.Preview))
            {
                continue;
            }

            _ = _assetPreviewCoordinator.LoadInspectorPreviewAsync(
                assetTab,
                details,
                _formatFirstErrorMessage,
                () => bundleTab.AssetTabs.Contains(assetTab),
                AssetPreviewLoadPriority.BackgroundPrewarm);
        }
    }

    private static bool HasPreviewWorkOrContent(InspectorPreviewViewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        return preview.IsPreviewLoading || HasStablePreviewContent(preview);
    }

    private static bool HasStablePreviewContent(InspectorPreviewViewModel preview)
    {
        ArgumentNullException.ThrowIfNull(preview);

        return preview.Image.ImageSource is not null ||
               preview.PreviewAudioSource is not null ||
               preview.Mesh.Geometry is not null ||
               !string.IsNullOrWhiteSpace(preview.PreviewText) ||
               !string.IsNullOrWhiteSpace(preview.PreviewStatusText);
    }
}
