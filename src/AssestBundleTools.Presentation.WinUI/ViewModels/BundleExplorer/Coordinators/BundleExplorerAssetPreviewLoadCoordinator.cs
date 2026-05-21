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
    private long _previewActivationSequence;

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

        if (ReferenceEquals(assetTab, _activeAssetTabProvider()))
        {
            assetTab.MarkPreviewActivated(Interlocked.Increment(ref _previewActivationSequence));
        }

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

    public void InvalidatePreview(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        assetTab.CancelPreviewRequest();
        assetTab.Inspector.Preview.ResetState();
    }

    public void ReloadForegroundPreview(AssetDocumentTabViewModel assetTab)
    {
        InvalidatePreview(assetTab);
        LoadForegroundPreview(assetTab, promoteLoadingPreview: true);
    }

    public void DemotePreviewToBackgroundPrewarm(
        AssetDocumentTabViewModel? assetTab,
        IEnumerable<AssetDocumentTabViewModel> assetTabs)
    {
        ArgumentNullException.ThrowIfNull(assetTabs);

        if (assetTab is null ||
            assetTab.Details is not { } details ||
            HasStablePreviewContent(assetTab.Inspector.Preview))
        {
            return;
        }

        var openAssetTabs = assetTabs as IReadOnlyList<AssetDocumentTabViewModel> ?? assetTabs.ToArray();
        if (!openAssetTabs.Contains(assetTab) ||
            ReferenceEquals(assetTab, _activeAssetTabProvider()))
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
            () => openAssetTabs.Contains(assetTab) &&
                  !ReferenceEquals(assetTab, _activeAssetTabProvider()),
            AssetPreviewLoadPriority.BackgroundPrewarm);
    }

    public void PrewarmBackgroundPreviews(BundleDocumentTabViewModel bundleTab, AssetDocumentTabViewModel foregroundAssetTab)
    {
        ArgumentNullException.ThrowIfNull(bundleTab);
        ArgumentNullException.ThrowIfNull(foregroundAssetTab);

        PrewarmBackgroundPreviews(bundleTab.AssetTabs, foregroundAssetTab);
    }

    public void PrewarmBackgroundPreviews(
        IEnumerable<AssetDocumentTabViewModel> assetTabs,
        AssetDocumentTabViewModel foregroundAssetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTabs);
        ArgumentNullException.ThrowIfNull(foregroundAssetTab);

        var openAssetTabs = assetTabs as IReadOnlyList<AssetDocumentTabViewModel> ?? assetTabs.ToArray();
        foreach (var assetTab in OrderPrewarmCandidates(openAssetTabs))
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
                () => openAssetTabs.Contains(assetTab) &&
                      !ReferenceEquals(assetTab, _activeAssetTabProvider()),
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

        if (preview.IsPreviewLoading)
        {
            return false;
        }

        return preview.Image.ImageSource is not null ||
               preview.PreviewAudioSource is not null ||
               preview.Mesh.Geometry is not null ||
               !string.IsNullOrWhiteSpace(preview.PreviewText) ||
               !string.IsNullOrWhiteSpace(preview.PreviewStatusText);
    }

    private static IEnumerable<AssetDocumentTabViewModel> OrderPrewarmCandidates(
        IReadOnlyList<AssetDocumentTabViewModel> openAssetTabs)
    {
        return openAssetTabs
            .Select((assetTab, index) => new { AssetTab = assetTab, Index = index })
            .OrderByDescending(item => item.AssetTab.PreviewActivationSequence)
            .ThenByDescending(item => item.AssetTab.PreviewOpenSequence)
            .ThenByDescending(item => item.Index)
            .Select(static item => item.AssetTab);
    }
}
