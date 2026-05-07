using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责资产文档宿主的空态检查器、文档标签构造与宿主清理。
/// </summary>
internal sealed class BundleExplorerAssetDocumentHostCoordinator
{
    private readonly IBundleExplorerDocumentWorkspaceHost _documentWorkspace;
    private readonly ILocalizationService _localizationService;
    private readonly Action _notifyActionStates;
    private readonly Action _requestWorkspaceBrowserStatePersistence;
    private readonly InspectorInformationTextSet _inspectorInformationTextSet;
    private readonly InspectorPanelViewModel _emptyInspector;

    public BundleExplorerAssetDocumentHostCoordinator(
        IBundleExplorerDocumentWorkspaceHost documentWorkspace,
        ILocalizationService localizationService,
        Action notifyActionStates,
        Action requestWorkspaceBrowserStatePersistence)
    {
        _documentWorkspace = documentWorkspace ?? throw new ArgumentNullException(nameof(documentWorkspace));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _notifyActionStates = notifyActionStates ?? throw new ArgumentNullException(nameof(notifyActionStates));
        _requestWorkspaceBrowserStatePersistence = requestWorkspaceBrowserStatePersistence ?? throw new ArgumentNullException(nameof(requestWorkspaceBrowserStatePersistence));
        _inspectorInformationTextSet = InspectorInformationTextSetFactory.Create(_localizationService);
        _emptyInspector = new InspectorPanelViewModel(_inspectorInformationTextSet);
        ResetEmptyInspector();
    }

    public InspectorPanelViewModel CurrentInspector =>
        _documentWorkspace.ActiveAssetDocumentTab?.Inspector ??
        _documentWorkspace.ActiveSpineGroupDocumentTab?.Inspector ??
        _emptyInspector;

    public AssetDocumentTabViewModel CreateAssetDocumentTab(
        BundleId bundleId,
        AssetId assetId,
        string assetName)
    {
        var tab = new AssetDocumentTabViewModel(bundleId, assetId, assetName, _inspectorInformationTextSet);
        tab.PreviewImageDisplayOptionsChanged += OnPreviewImageDisplayOptionsChanged;
        tab.PreviewImageViewStateChanged += OnPreviewImageViewStateChanged;
        tab.PreviewMeshDisplayOptionsChanged += OnPreviewMeshDisplayOptionsChanged;
        tab.Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        return tab;
    }

    public SpineAssetGroupDocumentTabViewModel CreateSpineGroupDocumentTab(SpineAssetGroupDto spineGroup)
    {
        var tab = new SpineAssetGroupDocumentTabViewModel(spineGroup, _inspectorInformationTextSet);
        tab.Inspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
        return tab;
    }

    public void ClearCurrentInspectorHostState(bool clearAssetTabs)
    {
        if (clearAssetTabs)
        {
            _documentWorkspace.ActiveBundleTab?.ClearAssetTabs();
        }
        else
        {
            _documentWorkspace.ActiveBundleTab?.SetActiveAssetTab(null);
        }

        ResetEmptyInspector();
        _documentWorkspace.NotifyAssetDocumentHostStateChanged();
        _notifyActionStates();
    }

    public void ResetEmptyInspector()
    {
        _emptyInspector.Clear(GetString(ResourceKeys.BundleExplorerInspectorEmpty));
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private void OnPreviewImageDisplayOptionsChanged(object? sender, EventArgs e)
    {
        _requestWorkspaceBrowserStatePersistence();
    }

    private void OnPreviewImageViewStateChanged(object? sender, EventArgs e)
    {
        _requestWorkspaceBrowserStatePersistence();
    }

    private void OnPreviewMeshDisplayOptionsChanged(object? sender, EventArgs e)
    {
        _requestWorkspaceBrowserStatePersistence();
    }
}
