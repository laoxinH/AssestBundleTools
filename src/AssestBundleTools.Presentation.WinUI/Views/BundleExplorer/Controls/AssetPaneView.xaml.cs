using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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
    private const double SearchHostExpandedHeight = 40d;
    private const int AssetNameToolTipDelayMilliseconds = 320;
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(AssetPaneViewModel),
            typeof(AssetPaneView),
            new PropertyMetadata(null, OnViewModelChanged));

    private readonly Storyboard _searchShowStoryboard = new();
    private readonly Storyboard _searchHideStoryboard = new();
    private AssetPaneViewModel? _observedViewModel;
    private bool _isSynchronizingSelectionFromViewModel;
    private CancellationTokenSource? _assetNameToolTipDelayCancellationSource;
    private ToolTip? _currentAssetNameToolTip;
    private TextBlock? _currentAssetNameToolTipTarget;

    public AssetPaneView()
    {
        InitializeComponent();
        InitializeSearchAnimations();
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
        view.UpdateSearchState(useTransitions: false);
        view.UpdateSearchToggleToolTip();
    }

    private void OnAssetListSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingSelectionFromViewModel)
        {
            return;
        }

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
        UpdateSearchState(useTransitions: false);
        UpdateSearchToggleToolTip();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachFromViewModel(_observedViewModel);
        _searchShowStoryboard.Stop();
        _searchHideStoryboard.Stop();
        CloseAssetNameToolTip();
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

    private void SynchronizeSelectionFromViewModel()
    {
        if (ViewModel is null)
        {
            return;
        }

        _isSynchronizingSelectionFromViewModel = true;
        try
        {
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
        finally
        {
            _isSynchronizingSelectionFromViewModel = false;
        }
    }

    /// <summary>
    /// 资产列表搜索框复用与左侧资源包搜索一致的展开/收起过渡，
    /// 保持工作台交互节奏统一，同时避免直接切换 Visibility 带来的跳闪。
    /// </summary>
    private void InitializeSearchAnimations()
    {
        var showHeightAnimation = new DoubleAnimation
        {
            From = 0,
            To = SearchHostExpandedHeight,
            Duration = TimeSpan.FromMilliseconds(220),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showHeightAnimation, SearchHost);
        Storyboard.SetTargetProperty(showHeightAnimation, nameof(Height));

        var showOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showOpacityAnimation, SearchHost);
        Storyboard.SetTargetProperty(showOpacityAnimation, nameof(Opacity));

        var showTranslateAnimation = new DoubleAnimation
        {
            From = -6,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showTranslateAnimation, SearchTranslateTransform);
        Storyboard.SetTargetProperty(showTranslateAnimation, nameof(TranslateTransform.Y));

        _searchShowStoryboard.Children.Add(showHeightAnimation);
        _searchShowStoryboard.Children.Add(showOpacityAnimation);
        _searchShowStoryboard.Children.Add(showTranslateAnimation);

        var hideHeightAnimation = new DoubleAnimation
        {
            From = SearchHostExpandedHeight,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideHeightAnimation, SearchHost);
        Storyboard.SetTargetProperty(hideHeightAnimation, nameof(Height));

        var hideOpacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideOpacityAnimation, SearchHost);
        Storyboard.SetTargetProperty(hideOpacityAnimation, nameof(Opacity));

        var hideTranslateAnimation = new DoubleAnimation
        {
            From = 0,
            To = -6,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideTranslateAnimation, SearchTranslateTransform);
        Storyboard.SetTargetProperty(hideTranslateAnimation, nameof(TranslateTransform.Y));

        _searchHideStoryboard.Children.Add(hideHeightAnimation);
        _searchHideStoryboard.Children.Add(hideOpacityAnimation);
        _searchHideStoryboard.Children.Add(hideTranslateAnimation);
        _searchHideStoryboard.Completed += OnSearchHideCompleted;
    }

    private void UpdateSearchState()
    {
        UpdateSearchState(useTransitions: true);
    }

    private void UpdateSearchState(bool useTransitions)
    {
        var isSearchExpanded = ViewModel?.IsSearchExpanded == true;

        if (isSearchExpanded)
        {
            _searchHideStoryboard.Stop();
            SearchHost.Visibility = Visibility.Visible;

            if (!useTransitions)
            {
                SearchHost.Height = SearchHostExpandedHeight;
                SearchHost.Opacity = 1;
                SearchTranslateTransform.Y = 0;
                return;
            }

            SearchHost.Height = 0;
            SearchHost.Opacity = 0;
            SearchTranslateTransform.Y = -6;
            _searchShowStoryboard.Begin();
            return;
        }

        if (!useTransitions)
        {
            SearchHost.Visibility = Visibility.Collapsed;
            SearchHost.Height = 0;
            SearchHost.Opacity = 0;
            SearchTranslateTransform.Y = -6;
            return;
        }

        if (SearchHost.Visibility == Visibility.Visible)
        {
            _searchShowStoryboard.Stop();
            _searchHideStoryboard.Begin();
        }
    }

    private void OnSearchHideCompleted(object? sender, object e)
    {
        if (ViewModel?.IsSearchExpanded == true)
        {
            return;
        }

        SearchHost.Visibility = Visibility.Collapsed;
        SearchHost.Height = 0;
        SearchHost.Opacity = 0;
        SearchTranslateTransform.Y = -6;
    }

    /// <summary>
    /// 资产名称提示只在名称列局部生效，并使用更短的延迟打开，
    /// 避免沿用系统默认等待时间导致“已经想确认名字了，但提示还没出来”。
    /// </summary>
    private async void OnAssetNamePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        CloseAssetNameToolTip();

        var cancellationSource = new CancellationTokenSource();
        _assetNameToolTipDelayCancellationSource = cancellationSource;

        try
        {
            await Task.Delay(AssetNameToolTipDelayMilliseconds, cancellationSource.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cancellationSource.IsCancellationRequested)
        {
            return;
        }

        if (!ReferenceEquals(_assetNameToolTipDelayCancellationSource, cancellationSource))
        {
            return;
        }

        OpenAssetNameToolTip(textBlock);
    }

    private void OnAssetNamePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        CloseAssetNameToolTipFor(textBlock);
    }

    private void OnAssetNamePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        CloseAssetNameToolTipFor(textBlock);
    }

    private void OnAssetNameTextBlockUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBlock textBlock)
        {
            return;
        }

        CloseAssetNameToolTipFor(textBlock);
    }

    private void OnAssetRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        UpdateAssetRowVisualState(sender, isPointerOver: true, isPressed: false);
    }

    private void OnAssetRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        UpdateAssetRowVisualState(sender, isPointerOver: false, isPressed: false);
    }

    private void OnAssetRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        UpdateAssetRowVisualState(sender, isPointerOver: true, isPressed: true);
    }

    private void OnAssetRowPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        UpdateAssetRowVisualState(sender, isPointerOver: true, isPressed: false);
    }

    private void OnAssetRowPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        UpdateAssetRowVisualState(sender, isPointerOver: false, isPressed: false);
    }

    private void UpdateSearchToggleToolTip()
    {
        var resourceKey = ViewModel?.IsSearchExpanded == true
            ? ResourceKeys.AssetPaneViewSearchToggleButtonCloseToolTip
            : ResourceKeys.AssetPaneViewSearchToggleButtonOpenToolTip;

        ToolTipService.SetToolTip(SearchToggleButton, UiText.Get(resourceKey));
    }

    private void ApplyLocalizedText()
    {
        SearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetPaneViewSearchTextBoxPlaceholder);
        ModifiedOnlyToggleButton.Content = UiText.Get(ResourceKeys.AssetPaneViewModifiedOnlyToggleButtonContent);
        PreviewOnlyToggleButton.Content = UiText.Get(ResourceKeys.AssetPaneViewPreviewOnlyToggleButtonContent);
        MultiSelectionLabelText.Text = UiText.Get(ResourceKeys.AssetPaneViewMultiSelectionLabel);
        MultiSelectionUnitText.Text = UiText.Get(ResourceKeys.AssetPaneViewMultiSelectionUnit);
        BatchExtractSelectedButton.Content = UiText.Get(ResourceKeys.AssetPaneViewBatchExtractSelectedButton);
        AssetNameHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetNameHeaderText);
        AssetTypeHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetTypeHeaderText);
        AssetSizeHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetSizeHeaderText);
        AssetStateHeaderText.Text = UiText.Get(ResourceKeys.AssetPaneViewAssetStateHeaderText);
        EmptyStateHintText.Text = UiText.Get(ResourceKeys.AssetPaneViewEmptyStateHintText);
        UpdateSearchToggleToolTip();
    }

    /// <summary>
    /// 资产列表行的悬停/按压叠层直接复用左侧资源包列表同一组颜色，
    /// 让三栏工作台里的“可点击列表项”保持一致的反馈语言。
    /// </summary>
    private static void UpdateAssetRowVisualState(object sender, bool isPointerOver, bool isPressed)
    {
        if (sender is not Grid rowRoot)
        {
            return;
        }

        if (rowRoot.FindName("AssetRowHoverOverlay") is not Border hoverOverlay ||
            rowRoot.FindName("AssetRowPressedOverlay") is not Border pressedOverlay)
        {
            return;
        }

        hoverOverlay.Opacity = isPointerOver && !isPressed ? 1d : 0d;
        pressedOverlay.Opacity = isPressed ? 1d : 0d;
    }

    private void OpenAssetNameToolTip(TextBlock textBlock)
    {
        if (ToolTipService.GetToolTip(textBlock) is not ToolTip toolTip)
        {
            return;
        }

        _currentAssetNameToolTip = toolTip;
        _currentAssetNameToolTipTarget = textBlock;
        toolTip.IsOpen = true;
    }

    private void CloseAssetNameToolTipFor(TextBlock textBlock)
    {
        if (!ReferenceEquals(_currentAssetNameToolTipTarget, textBlock))
        {
            CancelPendingAssetNameToolTipDelay();
            return;
        }

        CloseAssetNameToolTip();
    }

    private void CloseAssetNameToolTip()
    {
        CancelPendingAssetNameToolTipDelay();

        if (_currentAssetNameToolTip is not null)
        {
            _currentAssetNameToolTip.IsOpen = false;
            _currentAssetNameToolTip = null;
        }

        _currentAssetNameToolTipTarget = null;
    }

    private void CancelPendingAssetNameToolTipDelay()
    {
        if (_assetNameToolTipDelayCancellationSource is null)
        {
            return;
        }

        _assetNameToolTipDelayCancellationSource.Cancel();
        _assetNameToolTipDelayCancellationSource.Dispose();
        _assetNameToolTipDelayCancellationSource = null;
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
}
