using System.Collections.Specialized;
using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.Controls;

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

    private AssetPaneViewModel? _observedViewModel;

    public AssetPaneView()
    {
        InitializeComponent();
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

    public event EventHandler? BatchExtractRequested;

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not AssetPaneView view)
        {
            return;
        }

        view.DetachFromViewModel(e.OldValue as AssetPaneViewModel);
        view.AttachToViewModel(e.NewValue as AssetPaneViewModel);
        view.SynchronizeSelectionFromViewModel();
    }

    private void OnAssetListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.UpdateSelection(
                AssetListView.SelectedItems.OfType<AssetListEntryViewModel>(),
                AssetListView.SelectedItem as AssetListEntryViewModel);
        }

        AssetSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnBatchExtractSelectedButtonClick(object sender, RoutedEventArgs e)
    {
        BatchExtractRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachToViewModel(ViewModel);
        SynchronizeSelectionFromViewModel();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachFromViewModel(_observedViewModel);
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
    }

    private void DetachFromViewModel(AssetPaneViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.Items.CollectionChanged -= OnItemsCollectionChanged;

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
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SynchronizeSelectionFromViewModel();
    }

    private void SynchronizeSelectionFromViewModel()
    {
        if (ViewModel is null)
        {
            return;
        }

        var targetSelection = ViewModel.SelectedAsset;
        if (targetSelection is null)
        {
            if (AssetListView.SelectedItem is not null || AssetListView.SelectedItems.Count > 0)
            {
                AssetListView.SelectedItem = null;
                AssetListView.SelectedItems.Clear();
            }

            return;
        }

        if (!ViewModel.Items.Contains(targetSelection))
        {
            return;
        }

        if (!ReferenceEquals(AssetListView.SelectedItem, targetSelection))
        {
            AssetListView.SelectedItem = targetSelection;
        }

        if (AssetListView.SelectedItems.Count != 1 ||
            !ReferenceEquals(AssetListView.SelectedItems[0], targetSelection))
        {
            AssetListView.SelectedItems.Clear();
            AssetListView.SelectedItems.Add(targetSelection);
        }

        AssetListView.ScrollIntoView(targetSelection);
    }

    private void ApplyLocalizedText()
    {
        CurrentBundleLabelText.Text = UiText.Get("AssetPaneView.CurrentBundleLabelText.Text");
        SearchTextBox.PlaceholderText = UiText.Get("AssetPaneView.SearchTextBox.PlaceholderText");
        ModifiedOnlyToggleButton.Content = UiText.Get("AssetPaneView.ModifiedOnlyToggleButton.Content");
        PreviewOnlyToggleButton.Content = UiText.Get("AssetPaneView.PreviewOnlyToggleButton.Content");
        MultiSelectionLabelText.Text = UiText.Get(ResourceKeys.AssetPaneViewMultiSelectionLabel);
        MultiSelectionUnitText.Text = UiText.Get(ResourceKeys.AssetPaneViewMultiSelectionUnit);
        BatchExtractSelectedButton.Content = UiText.Get(ResourceKeys.AssetPaneViewBatchExtractSelectedButton);
        AssetNameHeaderText.Text = UiText.Get("AssetPaneView.AssetNameHeaderText.Text");
        AssetTypeHeaderText.Text = UiText.Get("AssetPaneView.AssetTypeHeaderText.Text");
        AssetSizeHeaderText.Text = UiText.Get("AssetPaneView.AssetSizeHeaderText.Text");
        AssetStateHeaderText.Text = UiText.Get("AssetPaneView.AssetStateHeaderText.Text");
        EmptyStateHintText.Text = UiText.Get("AssetPaneView.EmptyStateHintText.Text");
    }
}
