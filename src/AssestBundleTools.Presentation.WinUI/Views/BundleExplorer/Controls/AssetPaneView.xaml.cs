using System.Collections.Specialized;
using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 中间区域单个资产窗格的可复用视图。
/// </summary>
/// <remarks>
/// 这个控件只负责把窗格状态渲染成 UI，并把选择、筛选等交互以事件形式抛回页面。
/// 真正的数据加载和用例调用仍由页面级 ViewModel 处理，避免控件直接参与业务编排。
/// </remarks>
public sealed partial class AssetPaneView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(AssetPaneViewModel),
            typeof(AssetPaneView),
            new PropertyMetadata(null, OnViewModelChanged));

    private readonly AssetPaneSearchViewCoordinator _searchCoordinator;
    private readonly AssetPaneSelectionViewCoordinator _selectionCoordinator;
    private readonly AssetPaneAssetNameToolTipCoordinator _assetNameToolTipCoordinator;
    private AssetPaneViewModel? _observedViewModel;
    private bool _isUpdatingViewModeToggles;

    public AssetPaneView()
    {
        InitializeComponent();
        InitializeDragExport();
        _searchCoordinator = new AssetPaneSearchViewCoordinator(
            SearchHost,
            SearchTranslateTransform,
            SearchToggleButton,
            () => ViewModel);
        _selectionCoordinator = new AssetPaneSelectionViewCoordinator(
            AssetListView,
            SpineGroupListView,
            () => ViewModel);
        _assetNameToolTipCoordinator = new AssetPaneAssetNameToolTipCoordinator();
        AssetListView.AddHandler(
            PointerWheelChangedEvent,
            new PointerEventHandler(OnAssetPaneListPointerWheelChanged),
            handledEventsToo: true);
        SpineGroupListView.AddHandler(
            PointerWheelChangedEvent,
            new PointerEventHandler(OnAssetPaneListPointerWheelChanged),
            handledEventsToo: true);
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public AssetPaneViewModel? ViewModel
    {
        get => (AssetPaneViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public event EventHandler? AssetSelectionChanged;

    public event EventHandler? FilterChanged;

    public event EventHandler? ViewModeChanged;

    public event EventHandler? BatchExtractRequested;

    public event EventHandler? OpenSelectedItemInNewWindowRequested;

    public event EventHandler? ExtractSelectedItemRequested;

    public event EventHandler? ReplaceSelectedAssetRequested;

    public void ScrollSelectedItemIntoView()
    {
        _selectionCoordinator.ScrollSelectedItemIntoView();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AssetPaneView view)
        {
            return;
        }

        view.DetachFromViewModel(e.OldValue as AssetPaneViewModel);
        view.AttachToViewModel(e.NewValue as AssetPaneViewModel);
        view.SynchronizeSelectionFromViewModel();
        view.SynchronizeSpineGroupSelectionFromViewModel();
        view.UpdateViewModeToggleState();
        view.UpdateListSelectionMode();
        view.UpdateSearchState(useTransitions: false);
        view.UpdateSearchToggleToolTip();
    }

    private void OnAssetListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.IsAssetViewMode != true)
        {
            _selectionCoordinator.TryApplyAssetSelectionToViewModel();
            return;
        }

        if (ViewModel?.IsMultiSelectionMode == true)
        {
            _selectionCoordinator.TryApplyAssetSelectionToViewModel();
            return;
        }

        if (_selectionCoordinator.TryApplyAssetSelectionToViewModel())
        {
            if (ViewModel?.IsMultiSelectionMode == true)
            {
                return;
            }

            AssetSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSpineGroupListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.IsSpineGroupViewMode != true)
        {
            _selectionCoordinator.TryApplySpineGroupSelectionToViewModel();
            return;
        }

        if (ViewModel?.IsMultiSelectionMode == true)
        {
            _selectionCoordinator.TryApplySpineGroupSelectionToViewModel();
            return;
        }

        if (_selectionCoordinator.TryApplySpineGroupSelectionToViewModel())
        {
            if (ViewModel?.IsMultiSelectionMode == true)
            {
                return;
            }

            AssetSelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel?.IsReplacingAssetTypeOptions == true)
        {
            return;
        }

        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnAssetNameSortButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            _selectionCoordinator.RunAssetItemsMutationPreservingSelection(
                () => viewModel.ToggleAssetSort(AssetPaneSortColumn.Name));
        }
    }

    private void OnAssetSizeSortButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is { } viewModel)
        {
            _selectionCoordinator.RunAssetItemsMutationPreservingSelection(
                () => viewModel.ToggleAssetSort(AssetPaneSortColumn.Size));
        }
    }

    /// <summary>
    /// ToggleButton 先完成自身勾选状态切换，再由这里把最终状态同步回 ViewModel。
    /// 这样可以避免点击流程里“控件内部切换”和“代码手动取反”互相打架。
    /// </summary>
    private void OnModifiedOnlyToggleButtonChecked(object sender, RoutedEventArgs e)
    {
        SetModifiedOnlyToggleState(isEnabled: true);
    }

    private void OnModifiedOnlyToggleButtonUnchecked(object sender, RoutedEventArgs e)
    {
        SetModifiedOnlyToggleState(isEnabled: false);
    }

    private void OnPreviewOnlyToggleButtonChecked(object sender, RoutedEventArgs e)
    {
        SetPreviewOnlyToggleState(isEnabled: true);
    }

    private void OnPreviewOnlyToggleButtonUnchecked(object sender, RoutedEventArgs e)
    {
        SetPreviewOnlyToggleState(isEnabled: false);
    }

    private void OnAssetsViewModeToggleButtonChecked(object sender, RoutedEventArgs e)
    {
        SetViewMode(AssetPaneViewMode.Assets);
    }

    private void OnAssetsViewModeToggleButtonUnchecked(object sender, RoutedEventArgs e)
    {
        RestoreViewModeToggleStateIfNeeded();
    }

    private void OnSpineGroupsViewModeToggleButtonChecked(object sender, RoutedEventArgs e)
    {
        SetViewMode(AssetPaneViewMode.SpineGroups);
    }

    private void OnSpineGroupsViewModeToggleButtonUnchecked(object sender, RoutedEventArgs e)
    {
        RestoreViewModeToggleStateIfNeeded();
    }

    private void OnBatchExtractSelectedButtonClick(object sender, RoutedEventArgs e)
    {
        BatchExtractRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchToggleButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.ToggleSearch();

        if (!ViewModel.IsSearchExpanded)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => SearchTextBox.Focus(FocusState.Programmatic));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachToViewModel(ViewModel);
        SynchronizeSelectionFromViewModel();
        SynchronizeSpineGroupSelectionFromViewModel();
        UpdateViewModeToggleState();
        UpdateListSelectionMode();
        UpdateSearchState(useTransitions: false);
        UpdateSearchToggleToolTip();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        EndMultiSelectionDragGesture();
        DetachFromViewModel(_observedViewModel);
        _searchCoordinator.Stop();
        _assetNameToolTipCoordinator.Close();
    }

    private void AttachToViewModel(AssetPaneViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _observedViewModel.Items.CollectionChanged += OnItemsCollectionChanged;
        _observedViewModel.SpineGroupItems.CollectionChanged += OnSpineGroupItemsCollectionChanged;
    }

    private void DetachFromViewModel(AssetPaneViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Items.CollectionChanged -= OnItemsCollectionChanged;
        viewModel.SpineGroupItems.CollectionChanged -= OnSpineGroupItemsCollectionChanged;

        if (ReferenceEquals(_observedViewModel, viewModel))
        {
            _observedViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AssetPaneViewModel.SelectedAsset))
        {
            SynchronizeSelectionFromViewModel();
            return;
        }

        if (e.PropertyName is nameof(AssetPaneViewModel.SelectedSpineGroup))
        {
            SynchronizeSpineGroupSelectionFromViewModel();
            return;
        }

        if (e.PropertyName is nameof(AssetPaneViewModel.IsAssetViewMode) or
            nameof(AssetPaneViewModel.IsSpineGroupViewMode))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                UpdateViewModeToggleState();
                UpdateListSelectionMode();
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateViewModeToggleState();
                UpdateListSelectionMode();
            });
            return;
        }

        if (e.PropertyName is nameof(AssetPaneViewModel.IsMultiSelectionMode))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                UpdateListSelectionMode();
                return;
            }

            DispatcherQueue.TryEnqueue(UpdateListSelectionMode);
            return;
        }

        if (e.PropertyName is nameof(AssetPaneViewModel.IsSearchExpanded))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                UpdateSearchState();
                UpdateSearchToggleToolTip();
                return;
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateSearchState();
                UpdateSearchToggleToolTip();
            });
            return;
        }

        if (e.PropertyName is nameof(AssetPaneViewModel.CanToggleSearch))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                UpdateSearchToggleToolTip();
                return;
            }

            DispatcherQueue.TryEnqueue(UpdateSearchToggleToolTip);
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeSelectionFromViewModel();
    }

    private void OnSpineGroupItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeSpineGroupSelectionFromViewModel();
    }

    private void SynchronizeSelectionFromViewModel()
    {
        _selectionCoordinator.SynchronizeAssetSelectionFromViewModel();
    }

    private void SynchronizeSpineGroupSelectionFromViewModel()
    {
        _selectionCoordinator.SynchronizeSpineGroupSelectionFromViewModel();
    }

    private void UpdateSearchState()
    {
        _searchCoordinator.UpdateSearchState(ViewModel);
    }

    private void UpdateSearchState(bool useTransitions)
    {
        _searchCoordinator.UpdateSearchState(ViewModel, useTransitions);
    }

    private void UpdateSearchToggleToolTip()
    {
        _searchCoordinator.UpdateSearchToggleToolTip(ViewModel);
    }

    private void ApplyLocalizedText()
    {
        SearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetPaneViewSearchTextBoxPlaceholder);
        AssetsViewModeToggleButton.Content = UiText.Get(ResourceKeys.AssetPaneViewModeAssetsToggleButtonContent);
        SpineGroupsViewModeToggleButton.Content = UiText.Get(ResourceKeys.AssetPaneViewModeSpineGroupsToggleButtonContent);
        ModifiedOnlyToggleButton.Content = UiText.Get(ResourceKeys.AssetPaneViewModifiedOnlyToggleButtonContent);
        PreviewOnlyToggleButton.Content = UiText.Get(ResourceKeys.AssetPaneViewPreviewOnlyToggleButtonContent);
        MultiSelectionLabelText.Text = UiText.Get(ResourceKeys.AssetPaneViewMultiSelectionLabel);
        MultiSelectionUnitText.Text = UiText.Get(ResourceKeys.AssetPaneViewMultiSelectionUnit);
        ApplyMultiSelectionLocalizedText();
        AssetNameHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetNameHeaderText);
        AssetTypeHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetTypeHeaderText);
        AssetSizeHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetSizeHeaderText);
        AssetStateHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetStateHeaderText);
        var sortByNameText = UiText.Get(ResourceKeys.AssetPaneViewSortByNameToolTip);
        var sortBySizeText = UiText.Get(ResourceKeys.AssetPaneViewSortBySizeToolTip);
        ToolTipService.SetToolTip(AssetNameSortButton, sortByNameText);
        ToolTipService.SetToolTip(AssetSizeSortButton, sortBySizeText);
        AutomationProperties.SetName(AssetNameSortButton, sortByNameText);
        AutomationProperties.SetName(AssetSizeSortButton, sortBySizeText);
        SpineGroupNameHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetNameHeaderText);
        SpineGroupPartsHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewSpineGroupPartsHeaderText);
        SpineGroupTexturePagesHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewSpineGroupTexturePagesHeaderText);
        SpineGroupStateHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetStateHeaderText);
        EmptyStateHintText.Text = UiText.Get(ResourceKeys.AssetPaneViewEmptyStateHintText);
        UpdateSearchToggleToolTip();
    }

    private void SetModifiedOnlyToggleState(bool isEnabled)
    {
        if (ViewModel is null || ViewModel.ShowModifiedOnly == isEnabled)
        {
            return;
        }

        ViewModel.ShowModifiedOnly = isEnabled;
    }

    private void SetPreviewOnlyToggleState(bool isEnabled)
    {
        if (ViewModel is null || ViewModel.ShowPreviewableOnly == isEnabled)
        {
            return;
        }

        ViewModel.ShowPreviewableOnly = isEnabled;
    }

    private void SetViewMode(AssetPaneViewMode viewMode)
    {
        if (_isUpdatingViewModeToggles || ViewModel is null)
        {
            return;
        }

        if (ViewModel.ViewMode == viewMode)
        {
            UpdateViewModeToggleState();
            return;
        }

        ViewModel.ViewMode = viewMode;
        ViewModeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RestoreViewModeToggleStateIfNeeded()
    {
        if (_isUpdatingViewModeToggles)
        {
            return;
        }

        UpdateViewModeToggleState();
    }

    private void UpdateViewModeToggleState()
    {
        _isUpdatingViewModeToggles = true;
        try
        {
            AssetsViewModeToggleButton.IsChecked = ViewModel?.IsAssetViewMode == true;
            SpineGroupsViewModeToggleButton.IsChecked = ViewModel?.IsSpineGroupViewMode == true;
        }
        finally
        {
            _isUpdatingViewModeToggles = false;
        }
    }
}
