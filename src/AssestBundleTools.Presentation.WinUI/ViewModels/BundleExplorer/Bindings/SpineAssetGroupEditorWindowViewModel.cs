using System.Collections.ObjectModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 独立 Spine 资源组窗口的轻量文档宿主。
/// </summary>
public sealed class SpineAssetGroupEditorWindowViewModel : ObservableObject
{
    private readonly SpineAssetGroupInspectorPresenter _presenter;
    private readonly ISpineAssetGroupDetailsLoader _detailsLoader;
    private readonly InspectorPanelViewModel _emptyInspector;
    private SpineAssetGroupDocumentTabViewModel? _activeTab;
    private bool _isBusy;

    internal SpineAssetGroupEditorWindowViewModel(
        SpineAssetGroupDocumentTabViewModel initialTab,
        IBundleExplorerBundlePaneHost bundlePane,
        ILocalizationService localizationService,
        ISpineAssetGroupDetailsLoader detailsLoader)
    {
        ArgumentNullException.ThrowIfNull(initialTab);
        ArgumentNullException.ThrowIfNull(localizationService);
        _detailsLoader = detailsLoader ?? throw new ArgumentNullException(nameof(detailsLoader));
        _presenter = new SpineAssetGroupInspectorPresenter(
            bundlePane ?? throw new ArgumentNullException(nameof(bundlePane)),
            localizationService);
        _emptyInspector = new InspectorPanelViewModel(
            InspectorInformationTextSetFactory.Create(localizationService));
        _emptyInspector.Clear(localizationService.GetRequiredString(ResourceKeys.BundleExplorerInspectorEmpty));

        Tabs.Add(initialTab);
        ActiveTab = initialTab;
        _presenter.PresentSpineGroupDocumentTab(initialTab);
    }

    public event EventHandler? EmptyGroupRequested;

    public ObservableCollection<SpineAssetGroupDocumentTabViewModel> Tabs { get; } = new();

    public SpineAssetGroupDocumentTabViewModel? ActiveTab
    {
        get => _activeTab;
        private set
        {
            if (SetProperty(ref _activeTab, value))
            {
                OnPropertyChanged(nameof(Inspector));
                OnPropertyChanged(nameof(WindowTitle));
            }
        }
    }

    public InspectorPanelViewModel Inspector => ActiveTab?.Inspector ?? _emptyInspector;

    public string WindowTitle => ActiveTab?.DisplayName ?? string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task ActivateTabAsync(SpineAssetGroupDocumentTabViewModel? tab)
    {
        if (tab is null || IsBusy)
        {
            return;
        }

        if (!Tabs.Contains(tab))
        {
            Tabs.Add(tab);
        }

        ActiveTab = tab;
        _presenter.PresentSpineGroupDocumentTab(tab);
        await LoadDetailsAsync(tab);
    }

    public async Task CloseTabAsync(SpineAssetGroupDocumentTabViewModel? tab)
    {
        if (tab is null || IsBusy || !Tabs.Remove(tab))
        {
            return;
        }

        if (ReferenceEquals(ActiveTab, tab))
        {
            ActiveTab = Tabs.FirstOrDefault();
            if (ActiveTab is not null)
            {
                await ActivateTabAsync(ActiveTab);
            }
        }

        if (Tabs.Count == 0)
        {
            EmptyGroupRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SelectAtlasPageAsync(SpineAtlasPageOptionViewModel? atlasPageOption)
    {
        if (atlasPageOption is null ||
            ActiveTab is not { } tab ||
            !tab.SelectAtlasPageOption(atlasPageOption))
        {
            return;
        }

        _presenter.PresentSpineGroupDocumentTab(tab);
        OnPropertyChanged(nameof(Inspector));

        var texturePreview = await _detailsLoader.LoadTexturePreviewAsync(tab, atlasPageOption);
        if (!ReferenceEquals(ActiveTab, tab) ||
            !ReferenceEquals(tab.SelectedAtlasPageOption, atlasPageOption))
        {
            return;
        }

        tab.ApplyTexturePreview(texturePreview);
        _presenter.PresentSpineGroupDocumentTab(tab);
        OnPropertyChanged(nameof(Inspector));
    }

    private async Task LoadDetailsAsync(SpineAssetGroupDocumentTabViewModel tab)
    {
        if (tab.Details is not null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var expectedGroupId = tab.GroupId;
            var result = await _detailsLoader.LoadAsync(tab);
            if (result is null ||
                !ReferenceEquals(ActiveTab, tab) ||
                !string.Equals(expectedGroupId, tab.GroupId, StringComparison.Ordinal))
            {
                return;
            }

            tab.ApplyDetails(result.Details, result.TexturePreview);
            _presenter.PresentSpineGroupDocumentTab(tab);
            OnPropertyChanged(nameof(Inspector));
        }
        finally
        {
            IsBusy = false;
        }
    }
}
