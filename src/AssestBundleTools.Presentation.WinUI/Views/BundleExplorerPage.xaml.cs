using System.ComponentModel;
using System.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views;

/// <summary>
/// 工作台主页面。
/// </summary>
/// <remarks>
/// 当前仍由 <see cref="BundleExplorerPage" /> 承载文档里约定的“三栏主工作区”结构，
/// 当前阶段重点是先把导航、只读浏览骨架和轻量操作反馈稳定下来。
/// </remarks>
public sealed partial class BundleExplorerPage : Page
{
    private const double BundleSearchHostExpandedHeight = 40d;
    private const double BundlePaneCollapsedWidth = 76d;
    private const double BundlePaneTransitionDurationMilliseconds = 240d;
    private static readonly GridLength ExpandedBundlePaneColumnWidth = new(1.08, GridUnitType.Star);
    private static readonly GridLength ExpandedAssetPaneColumnWidth = new(1.56, GridUnitType.Star);
    private static readonly GridLength ExpandedInspectorPaneColumnWidth = new(1.36, GridUnitType.Star);
    private static readonly GridLength BalancedContentPaneColumnWidth = new(1d, GridUnitType.Star);
    private readonly Storyboard _bundleSearchShowStoryboard = new();
    private readonly Storyboard _bundleSearchHideStoryboard = new();
    private readonly Storyboard _bundlePaneExpandedShowStoryboard = new();
    private readonly Storyboard _bundlePaneCollapsedShowStoryboard = new();
    private readonly Storyboard _collapsedBundleImportFileInteractionStoryboard = new();
    private readonly Storyboard _collapsedBundleImportDirectoryInteractionStoryboard = new();
    private CancellationTokenSource? _bundlePaneTransitionCts;
    private WorkspaceNavigationRequest? _workspaceNavigationRequest;
    private bool _isWorkspaceImportDragActive;
    private bool _isSynchronizingAssetDocumentTabSelection;

    public BundleExplorerPage()
    {
        ViewModel = new BundleExplorerViewModel(
            App.CurrentApp.WorkspaceBrowserService,
            App.CurrentApp.AppLogService,
            App.CurrentApp.AppNotificationService,
            App.CurrentApp.LocalizationService,
            App.CurrentApp.FilePickerService,
            App.CurrentApp.WorkspaceContextService,
            App.CurrentApp.WorkspaceImportCoordinatorService,
            App.CurrentApp.AssetBundleDecryptorRegistry,
            App.CurrentApp.PathNavigationService);

        InitializeComponent();
        ApplyLocalizedText();
        InitializeBundlePaneAnimations();
        InitializeBundleSearchAnimations();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public BundleExplorerViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await UpdateBundlePaneCollapseStateAsync(useTransitions: false);
        UpdateBundleSearchState(useTransitions: false);
        await ViewModel.InitializeAsync(_workspaceNavigationRequest);
        SynchronizeWindowWorkspaceRegistration();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _workspaceNavigationRequest = e.Parameter as WorkspaceNavigationRequest;
    }

    private async void OnNewWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var request = await App.CurrentApp.WorkspaceDialogService.RequestNewWorkspaceAsync(XamlRoot);
        OpenWorkspace(request);
    }

    private async void OnOpenWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var request = await App.CurrentApp.WorkspaceDialogService.RequestOpenWorkspaceFromDirectoryAsync(XamlRoot);
        OpenWorkspace(request);
    }

    private async void OnBundleNodeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BundleNodeViewModel bundleNode })
        {
            return;
        }

        await ViewModel.OpenBundleInTabAsync(bundleNode);
    }

    private void OnBundleNodeContextFlyoutOpening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            switch (item.Name)
            {
                case "OpenContainingLocationMenuItem":
                    item.Text = UiText.Get(ResourceKeys.BundleExplorerPageBundleNodeOpenContainingLocationMenuItemText);
                    break;
                case "RemoveBundleMenuItem":
                    item.Text = UiText.Get(ResourceKeys.BundleExplorerPageBundleNodeRemoveMenuItemText);
                    break;
            }
        }
    }

    private void OnBundleNodeOpenContainingLocationMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BundleNodeViewModel bundleNode })
        {
            return;
        }

        ViewModel.OpenBundleSourceLocation(bundleNode);
    }

    private async void OnBundleNodeRemoveMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: BundleNodeViewModel bundleNode })
        {
            return;
        }

        await ViewModel.RemoveBundleFromWorkspaceAsync(bundleNode);
    }

    private async void OnPrimaryPaneAssetSelectionChanged(object sender, EventArgs e)
    {
        await ViewModel.HandlePrimaryPaneSelectionChangedAsync();
    }

    private async void OnPrimaryPaneFilterChanged(object sender, EventArgs e)
    {
        await ViewModel.RefreshPrimaryPaneAsync();
    }

    private async void OnBundleTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BundleTabView.SelectedItem is BundleDocumentTabViewModel bundleTab)
        {
            await ViewModel.ActivateBundleTabAsync(bundleTab);
        }
    }

    private async void OnBundleTabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is BundleDocumentTabViewModel bundleTab)
        {
            await ViewModel.CloseBundleTabAsync(bundleTab);
            return;
        }

        if (args.Tab is FrameworkElement { DataContext: BundleDocumentTabViewModel dataContextTab })
        {
            await ViewModel.CloseBundleTabAsync(dataContextTab);
        }
    }

    private void OnBundleTabContextFlyoutOpening(object sender, object e)
    {
        ConfigureTabContextFlyout(
            sender as MenuFlyout,
            closeOtherMenuItemName: "CloseOtherBundleTabsMenuItem",
            closeAllMenuItemName: "CloseAllBundleTabsMenuItem",
            tabCount: ViewModel.BundleTabs.Count);
    }

    private async void OnCloseOtherBundleTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TryGetTaggedValue(sender, out BundleDocumentTabViewModel? bundleTab))
        {
            await ViewModel.CloseOtherBundleTabsAsync(bundleTab);
        }
    }

    private async void OnCloseAllBundleTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.CloseAllBundleTabsAsync();
    }

    private async void OnAssetDocumentTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingAssetDocumentTabSelection ||
            ViewModel.IsSynchronizingAssetDocumentHostSelection)
        {
            return;
        }

        if (AssetDocumentTabView.SelectedItem is AssetDocumentTabViewModel assetTab)
        {
            await ViewModel.ActivateAssetDocumentTabAsync(assetTab);
        }
    }

    private async void OnAssetDocumentTabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Item is AssetDocumentTabViewModel assetTab)
        {
            await ViewModel.CloseAssetDocumentTabAsync(assetTab);
            return;
        }

        if (args.Tab is FrameworkElement { DataContext: AssetDocumentTabViewModel dataContextTab })
        {
            await ViewModel.CloseAssetDocumentTabAsync(dataContextTab);
        }
    }

    private void OnAssetDocumentTabContextFlyoutOpening(object sender, object e)
    {
        ConfigureTabContextFlyout(
            sender as MenuFlyout,
            closeOtherMenuItemName: "CloseOtherAssetDocumentTabsMenuItem",
            closeAllMenuItemName: "CloseAllAssetDocumentTabsMenuItem",
            tabCount: ViewModel.CurrentAssetTabs.Count);
    }

    private async void OnCloseOtherAssetDocumentTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TryGetTaggedValue(sender, out AssetDocumentTabViewModel? assetTab))
        {
            await ViewModel.CloseOtherAssetDocumentTabsAsync(assetTab);
        }
    }

    private async void OnCloseAllAssetDocumentTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.CloseAllAssetDocumentTabsAsync();
    }

    private void OnBundleSearchToggleButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleBundleSearch();

        if (!ViewModel.IsBundleSearchExpanded)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => BundleSearchTextBox.Focus(FocusState.Programmatic));
    }

    private async void OnBundleImportFileButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RequestBundleFileImportAsync();
    }

    private async void OnBundleImportDirectoryButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RequestBundleDirectoryImportAsync();
    }

    private void OnWorkspaceImportDragOver(object sender, DragEventArgs e)
    {
        var canAcceptImport = ViewModel.HasWorkspace &&
                              e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = canAcceptImport
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        SetWorkspaceImportDropOverlayVisible(canAcceptImport);
    }

    private void OnWorkspaceImportDragLeave(object sender, DragEventArgs e)
    {
        SetWorkspaceImportDropOverlayVisible(false);
    }

    private async void OnWorkspaceImportDrop(object sender, DragEventArgs e)
    {
        SetWorkspaceImportDropOverlayVisible(false);

        if (!ViewModel.HasWorkspace ||
            !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var storageItems = await e.DataView.GetStorageItemsAsync();
        var droppedPaths = storageItems
            .Select(static item => item.Path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (droppedPaths.Length == 0)
        {
            return;
        }

        await ViewModel.RequestDragDropImportAsync(droppedPaths);
    }

    private void OnCollapsedBundlePaneActionPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: true, isPressed: false);
    }

    private void OnCollapsedBundlePaneActionPointerExited(object sender, PointerRoutedEventArgs e)
    {
        UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: false, isPressed: false);
    }

    private void OnCollapsedBundlePaneActionPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: true, isPressed: true);
    }

    private void OnCollapsedBundlePaneActionPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        // 当前释放事件能够到达按钮本身时，默认回到“悬停”状态；
        // 若指针已经移出，Exited / Canceled 会紧接着把状态复位。
        UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: true, isPressed: false);
    }

    private void OnCollapsedBundlePaneActionPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: false, isPressed: false);
    }

    private void OnBundlePaneCollapseButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleBundlePaneCollapsed();
    }

    private void OnBatchExtractChipButtonClick(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(BatchTasksPage));
    }

    private void OnReturnToConsoleIconButtonClick(object sender, RoutedEventArgs e)
    {
        if (TryNavigateFromShell(ShellNavigationKeys.LaunchGuide))
        {
            return;
        }

        Frame?.Navigate(typeof(HomePage));
    }

    private void OnSaveAsBundleChipButtonClick(object sender, RoutedEventArgs e)
    {
        Frame?.Navigate(typeof(BatchTasksPage));
    }

    private async void OnInspectorExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private void OnInspectorOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
    }

    private async void OnPrimaryPaneBatchExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetsAsync();
    }

    private async void OnInspectorSelectFileReplacementSourceRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectFileReplacementSourceAsync();
    }

    private async void OnInspectorSelectReplacementSourceRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectWorkspaceReplacementSourceAsync();
    }

    private async void OnInspectorApplyReplacementRequested(object sender, EventArgs e)
    {
        await ViewModel.ApplyPendingReplacementAsync();
    }

    private async void OnInspectorUndoReplacementRequested(object sender, EventArgs e)
    {
        await ViewModel.UndoAppliedReplacementAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BundleExplorerViewModel.IsBundleSearchExpanded))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                UpdateBundleSearchState();
                return;
            }

            DispatcherQueue.TryEnqueue(UpdateBundleSearchState);
            return;
        }

        if (e.PropertyName is nameof(BundleExplorerViewModel.IsBundlePaneCollapsed))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                _ = UpdateBundlePaneCollapseStateAsync();
                return;
            }

            DispatcherQueue.TryEnqueue(() => _ = UpdateBundlePaneCollapseStateAsync());
            return;
        }

        if (e.PropertyName is nameof(BundleExplorerViewModel.HasWorkspace))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                SynchronizeWindowWorkspaceRegistration();
                return;
            }

            DispatcherQueue.TryEnqueue(SynchronizeWindowWorkspaceRegistration);
            return;
        }

        if (e.PropertyName is nameof(BundleExplorerViewModel.CurrentAssetTabs) or
            nameof(BundleExplorerViewModel.ActiveAssetDocumentTab))
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                SynchronizeAssetDocumentTabSelection();
                return;
            }

            DispatcherQueue.TryEnqueue(SynchronizeAssetDocumentTabSelection);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.FlushWorkspaceBrowserState();
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _bundleSearchShowStoryboard.Stop();
        _bundleSearchHideStoryboard.Stop();
        _bundlePaneExpandedShowStoryboard.Stop();
        _bundlePaneCollapsedShowStoryboard.Stop();
        _collapsedBundleImportFileInteractionStoryboard.Stop();
        _collapsedBundleImportDirectoryInteractionStoryboard.Stop();
        CancelBundlePaneTransition();
    }

    /// <summary>
    /// 显式填充页面静态文案，避免自动本地化失效时导致布局被空文本撑坏。
    /// </summary>
    private void ApplyLocalizedText()
    {
        PageEyebrowText.Text = UiText.Get("BundleExplorerPage.PageEyebrowText.Text");
        PageTitleText.Text = UiText.Get("BundleExplorerPage.PageTitleText.Text");
        CreateWorkspaceChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageNewWorkspaceButton);
        OpenWorkspaceChipText.Text = UiText.Get(ResourceKeys.BundleExplorerPageOpenWorkspaceButton);
        ToolTipService.SetToolTip(ReturnToConsoleIconButton, UiText.Get(ResourceKeys.BundleExplorerPageReturnToConsoleButton));
        RefreshChipText.Text = UiText.Get("BundleExplorerPage.RefreshButton.Content");
        BatchExtractChipText.Text = UiText.Get("BundleExplorerPage.BatchExtractButton.Content");
        SaveAsBundleChipText.Text = UiText.Get("BundleExplorerPage.SaveAsBundleButton.Content");
        LeftPanelTitleText.Text = UiText.Get("BundleExplorerPage.LeftPanelTitleText.Text");
        BundleSearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.BundleExplorerPageBundleSearchTextBoxPlaceholder);
        ToolTipService.SetToolTip(
            BundleImportFileButton,
            UiText.Get(ResourceKeys.BundleExplorerPageBundleImportFileButtonToolTip));
        ToolTipService.SetToolTip(
            BundleImportDirectoryButton,
            UiText.Get(ResourceKeys.BundleExplorerPageBundleImportDirectoryButtonToolTip));
        ToolTipService.SetToolTip(
            CollapsedBundleImportFileButton,
            UiText.Get(ResourceKeys.BundleExplorerPageBundleImportFileButtonToolTip));
        ToolTipService.SetToolTip(
            CollapsedBundleImportDirectoryButton,
            UiText.Get(ResourceKeys.BundleExplorerPageBundleImportDirectoryButtonToolTip));
        CollapsedBundleImportFileButtonText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneImportFileButtonText);
        CollapsedBundleImportDirectoryButtonText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneImportDirectoryButtonText);
        CollapsedBundlePaneBundleCountLabelText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneBundleCountLabelText);
        CollapsedBundlePaneGroupCountLabelText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneGroupCountLabelText);
        CollapsedBundlePaneSelectionLabelText.Text = UiText.Get(ResourceKeys.BundleExplorerPageCollapsedBundlePaneSelectionLabelText);
        WorkspaceImportDropOverlayTitleText.Text = UiText.Get(ResourceKeys.WorkspaceImportDropOverlayTitle);
        WorkspaceImportDropOverlayHintText.Text = UiText.Get(ResourceKeys.WorkspaceImportDropOverlayHint);
        CenterPanelTitleText.Text = UiText.Get("BundleExplorerPage.CenterPanelTitleText.Text");
        RightPanelTitleText.Text = UiText.Get("BundleExplorerPage.RightPanelTitleText.Text");
    }

    private void SetWorkspaceImportDropOverlayVisible(bool isVisible)
    {
        if (_isWorkspaceImportDragActive == isVisible)
        {
            return;
        }

        _isWorkspaceImportDragActive = isVisible;
        WorkspaceImportDropOverlay.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// 两组标签的右键菜单文案与启用策略保持一致，
    /// 因此统一在页面层做一次配置，避免资源包标签和资产详情标签各写一套重复逻辑。
    /// </summary>
    private static void ConfigureTabContextFlyout(
        MenuFlyout? flyout,
        string closeOtherMenuItemName,
        string closeAllMenuItemName,
        int tabCount)
    {
        if (flyout is null)
        {
            return;
        }

        var closeOtherMenuItem = FindMenuFlyoutItem(flyout, closeOtherMenuItemName);
        if (closeOtherMenuItem is not null)
        {
            closeOtherMenuItem.Text = UiText.Get("BundleExplorerPage.TabContext.CloseOther.Text");
            closeOtherMenuItem.IsEnabled = tabCount > 1;
        }

        var closeAllMenuItem = FindMenuFlyoutItem(flyout, closeAllMenuItemName);
        if (closeAllMenuItem is not null)
        {
            closeAllMenuItem.Text = UiText.Get("BundleExplorerPage.TabContext.CloseAll.Text");
            closeAllMenuItem.IsEnabled = tabCount > 0;
        }
    }

    private static MenuFlyoutItem? FindMenuFlyoutItem(MenuFlyout flyout, string itemName)
    {
        ArgumentNullException.ThrowIfNull(flyout);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemName);

        return flyout.Items
            .OfType<MenuFlyoutItem>()
            .FirstOrDefault(item => string.Equals(item.Name, itemName, StringComparison.Ordinal));
    }

    private static bool TryGetTaggedValue<TValue>(
        object sender,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TValue? value)
        where TValue : class
    {
        value = (sender as FrameworkElement)?.Tag as TValue;
        return value is not null;
    }

    /// <summary>
    /// WinUI 的 TabView 在切换 ItemsSource 时可能会先回落到第一项，
    /// 这里显式把控件选中态对齐回 ViewModel 中已记录的活动资产标签，
    /// 避免资源包切换时把“当前详情标签”误写成第一项。
    /// </summary>
    private void SynchronizeAssetDocumentTabSelection()
    {
        if (_isSynchronizingAssetDocumentTabSelection)
        {
            return;
        }

        _isSynchronizingAssetDocumentTabSelection = true;
        try
        {
            if (!ReferenceEquals(AssetDocumentTabView.SelectedItem, ViewModel.ActiveAssetDocumentTab))
            {
                AssetDocumentTabView.SelectedItem = ViewModel.ActiveAssetDocumentTab;
            }
        }
        finally
        {
            _isSynchronizingAssetDocumentTabSelection = false;
        }
    }

    /// <summary>
    /// 收缩态资源包操作按钮采用显式指针动画控制，
    /// 避免不同 WinUI 模板状态在当前项目样式叠加下出现“看起来配置了，但实际不触发”的问题。
    /// </summary>
    private void UpdateCollapsedBundlePaneActionVisualState(object sender, bool isPointerOver, bool isPressed)
    {
        if (!TryGetCollapsedBundlePaneActionTargets(
                sender,
                out var actionTransform,
                out var hoverOverlay,
                out var pressedOverlay,
                out var hoverStroke,
                out var storyboard))
        {
            return;
        }

        var hoverOpacity = isPointerOver ? (isPressed ? 0.16d : 0.4d) : 0d;
        var pressedOpacity = isPressed ? 0.52d : 0d;
        var strokeOpacity = isPointerOver ? (isPressed ? 0.52d : 0.26d) : 0d;
        var translateY = isPressed ? 0.5d : isPointerOver ? -1d : 0d;
        var scale = isPressed ? 0.985d : isPointerOver ? 1.015d : 1d;
        var duration = TimeSpan.FromMilliseconds(isPressed ? 80d : isPointerOver ? 115d : 135d);

        storyboard.Stop();
        storyboard.Children.Clear();
        storyboard.Children.Add(CreateDoubleAnimation(hoverOverlay, nameof(Opacity), hoverOpacity, duration));
        storyboard.Children.Add(CreateDoubleAnimation(pressedOverlay, nameof(Opacity), pressedOpacity, duration));
        storyboard.Children.Add(CreateDoubleAnimation(hoverStroke, nameof(Opacity), strokeOpacity, duration));
        storyboard.Children.Add(CreateDoubleAnimation(actionTransform, nameof(CompositeTransform.TranslateY), translateY, duration, enableDependentAnimation: true));
        storyboard.Children.Add(CreateDoubleAnimation(actionTransform, nameof(CompositeTransform.ScaleX), scale, duration, enableDependentAnimation: true));
        storyboard.Children.Add(CreateDoubleAnimation(actionTransform, nameof(CompositeTransform.ScaleY), scale, duration, enableDependentAnimation: true));
        storyboard.Begin();
    }

    private bool TryGetCollapsedBundlePaneActionTargets(
        object sender,
        out CompositeTransform actionTransform,
        out Border hoverOverlay,
        out Border pressedOverlay,
        out Border hoverStroke,
        out Storyboard storyboard)
    {
        if (ReferenceEquals(sender, CollapsedBundleImportFileButton))
        {
            actionTransform = CollapsedBundleImportFileActionTransform;
            hoverOverlay = CollapsedBundleImportFileHoverOverlay;
            pressedOverlay = CollapsedBundleImportFilePressedOverlay;
            hoverStroke = CollapsedBundleImportFileHoverStroke;
            storyboard = _collapsedBundleImportFileInteractionStoryboard;
            return true;
        }

        if (ReferenceEquals(sender, CollapsedBundleImportDirectoryButton))
        {
            actionTransform = CollapsedBundleImportDirectoryActionTransform;
            hoverOverlay = CollapsedBundleImportDirectoryHoverOverlay;
            pressedOverlay = CollapsedBundleImportDirectoryPressedOverlay;
            hoverStroke = CollapsedBundleImportDirectoryHoverStroke;
            storyboard = _collapsedBundleImportDirectoryInteractionStoryboard;
            return true;
        }

        actionTransform = null!;
        hoverOverlay = null!;
        pressedOverlay = null!;
        hoverStroke = null!;
        storyboard = null!;
        return false;
    }

    private static DoubleAnimation CreateDoubleAnimation(
        DependencyObject target,
        string propertyPath,
        double to,
        TimeSpan duration,
        bool enableDependentAnimation = false)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration,
            EnableDependentAnimation = enableDependentAnimation,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, propertyPath);
        return animation;
    }

    /// <summary>
    /// 复用与控制台页一致的工作区打开策略。
    /// 若目标工作区已在任一已运行窗口中承载，则优先激活既有实例；
    /// 否则再决定是当前窗口跳转，还是启动新的主进程。
    /// </summary>
    private void OpenWorkspace(WorkspaceNavigationRequest? request)
    {
        if (request is null)
        {
            return;
        }

        if (App.CurrentApp.TryActivateOpenedWorkspace(request))
        {
            return;
        }

        // 工作台页在“尚未绑定任何工作区”的空态下，也遵循与控制台一致的首次复用规则，
        // 避免第一份工作区因为偏好是“新建窗口”而额外派生新进程。
        if (request.OpenTarget == WorkspaceOpenTarget.NewWindow &&
            TryGetHostWindow(out var hostWindow) &&
            App.CurrentApp.ShouldReuseCurrentWindowForFirstWorkspace(hostWindow))
        {
            request = request with { OpenTarget = WorkspaceOpenTarget.CurrentWindow };
        }

        if (request.OpenTarget == WorkspaceOpenTarget.NewWindow)
        {
            App.CurrentApp.WorkspaceWindowService.OpenWorkspaceInNewWindow(request);
            return;
        }

        if (TryNavigateFromShell(ShellNavigationKeys.Workbench, request))
        {
            return;
        }

        Frame?.Navigate(typeof(BundleExplorerPage), request);
    }

    /// <summary>
    /// 页面只在工作区真正绑定成功后，把结果同步回宿主窗口。
    /// 这样窗口去重逻辑可以保持在 App 层，而页面层只负责上报最终状态。
    /// </summary>
    private void SynchronizeWindowWorkspaceRegistration()
    {
        if (!TryGetHostWindow(out var hostWindow))
        {
            return;
        }

        if (ViewModel.TryGetCurrentWorkspaceNavigationRequest(out var navigationRequest))
        {
            hostWindow.RegisterWorkspace(navigationRequest);
            return;
        }

        hostWindow.ClearRegisteredWorkspace();
    }

    private bool TryNavigateFromShell(string navigationKey, object? parameter = null)
    {
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is ShellPage shellPage)
            {
                shellPage.NavigateToSection(navigationKey, parameter);
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool TryGetHostWindow([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssestBundleTools.Presentation.WinUI.MainWindow? hostWindow)
    {
        hostWindow = null;
        DependencyObject? current = this;

        while (current is not null)
        {
            if (current is ShellPage shellPage && shellPage.HostWindow is not null)
            {
                hostWindow = shellPage.HostWindow;
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    /// <summary>
    /// 初始化左侧资源包区域在“完整列表”和“摘要窄条”之间切换时的轻量动画。
    /// 当前将列宽连续过渡与卡片轻微淡入位移组合起来，
    /// 让布局切换更自然，同时保持 WinUI 布局稳定。
    /// </summary>
    private void InitializeBundlePaneAnimations()
    {
        var expandedOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(expandedOpacityAnimation, BundlePaneExpandedCard);
        Storyboard.SetTargetProperty(expandedOpacityAnimation, nameof(Opacity));

        var expandedTranslateAnimation = new DoubleAnimation
        {
            From = 10,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(expandedTranslateAnimation, BundlePaneExpandedTranslateTransform);
        Storyboard.SetTargetProperty(expandedTranslateAnimation, nameof(TranslateTransform.X));

        _bundlePaneExpandedShowStoryboard.Children.Add(expandedOpacityAnimation);
        _bundlePaneExpandedShowStoryboard.Children.Add(expandedTranslateAnimation);

        var collapsedOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(collapsedOpacityAnimation, BundlePaneCollapsedCard);
        Storyboard.SetTargetProperty(collapsedOpacityAnimation, nameof(Opacity));

        var collapsedTranslateAnimation = new DoubleAnimation
        {
            From = -10,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(collapsedTranslateAnimation, BundlePaneCollapsedTranslateTransform);
        Storyboard.SetTargetProperty(collapsedTranslateAnimation, nameof(TranslateTransform.X));

        _bundlePaneCollapsedShowStoryboard.Children.Add(collapsedOpacityAnimation);
        _bundlePaneCollapsedShowStoryboard.Children.Add(collapsedTranslateAnimation);
    }

    private Task UpdateBundlePaneCollapseStateAsync()
    {
        return UpdateBundlePaneCollapseStateAsync(useTransitions: true);
    }

    /// <summary>
    /// 同步左侧资源包区域的布局状态。
    /// 当需要动画时，会先连续过渡三列宽度，再淡入目标卡片，
    /// 从而让“收起 / 展开”不再是突兀跳变。
    /// </summary>
    private async Task UpdateBundlePaneCollapseStateAsync(bool useTransitions)
    {
        CancelBundlePaneTransition();

        var transitionCts = new CancellationTokenSource();
        _bundlePaneTransitionCts = transitionCts;

        try
        {
            var isCollapsed = ViewModel.IsBundlePaneCollapsed;

            if (!useTransitions || !CanAnimateBundlePaneLayout())
            {
                ApplyBundlePaneLayoutImmediately(isCollapsed);
                return;
            }

            var startLeftWidth = BundlePaneColumnDefinition.ActualWidth;
            var startCenterWidth = AssetPaneColumnDefinition.ActualWidth;
            var startRightWidth = InspectorPaneColumnDefinition.ActualWidth;
            var totalWidth = startLeftWidth + startCenterWidth + startRightWidth;

            if (totalWidth <= 0d)
            {
                ApplyBundlePaneLayoutImmediately(isCollapsed);
                return;
            }

            var (targetLeftWidth, targetCenterWidth, targetRightWidth) = GetBundlePaneTargetWidths(totalWidth, isCollapsed);
            PrepareBundlePaneSourceCard(isCollapsed);

            await AnimateBundlePaneWidthsAsync(
                startLeftWidth,
                startCenterWidth,
                startRightWidth,
                targetLeftWidth,
                targetCenterWidth,
                targetRightWidth,
                transitionCts.Token);

            transitionCts.Token.ThrowIfCancellationRequested();

            ApplyBundlePaneFinalColumnWidths(isCollapsed);
            ShowBundlePaneTargetCard(isCollapsed, useTransitions: true);
        }
        catch (OperationCanceledException)
        {
            // 连续点击时允许新的动画接管当前布局状态，取消旧动画属于预期行为。
        }
        finally
        {
            if (ReferenceEquals(_bundlePaneTransitionCts, transitionCts))
            {
                _bundlePaneTransitionCts = null;
            }

            transitionCts.Dispose();
        }
    }

    private bool CanAnimateBundlePaneLayout() =>
        BundleWorkspaceGrid.ActualWidth > 0d &&
        BundlePaneColumnDefinition.ActualWidth > 0d &&
        AssetPaneColumnDefinition.ActualWidth > 0d &&
        InspectorPaneColumnDefinition.ActualWidth > 0d;

    private void CancelBundlePaneTransition()
    {
        if (_bundlePaneTransitionCts is null)
        {
            return;
        }

        _bundlePaneTransitionCts.Cancel();
        _bundlePaneTransitionCts.Dispose();
        _bundlePaneTransitionCts = null;
    }

    private void ApplyBundlePaneLayoutImmediately(bool isCollapsed)
    {
        _bundlePaneExpandedShowStoryboard.Stop();
        _bundlePaneCollapsedShowStoryboard.Stop();
        ApplyBundlePaneFinalColumnWidths(isCollapsed);
        ShowBundlePaneTargetCard(isCollapsed, useTransitions: false);
    }

    private void PrepareBundlePaneSourceCard(bool isCollapsed)
    {
        _bundlePaneExpandedShowStoryboard.Stop();
        _bundlePaneCollapsedShowStoryboard.Stop();

        if (isCollapsed)
        {
            BundlePaneExpandedCard.Visibility = Visibility.Visible;
            BundlePaneExpandedCard.Opacity = 1;
            BundlePaneExpandedTranslateTransform.X = 0;
            BundlePaneCollapsedCard.Visibility = Visibility.Collapsed;
            BundlePaneCollapsedCard.Opacity = 0;
            BundlePaneCollapsedTranslateTransform.X = 0;
            return;
        }

        BundlePaneCollapsedCard.Visibility = Visibility.Visible;
        BundlePaneCollapsedCard.Opacity = 1;
        BundlePaneCollapsedTranslateTransform.X = 0;
        BundlePaneExpandedCard.Visibility = Visibility.Collapsed;
        BundlePaneExpandedCard.Opacity = 0;
        BundlePaneExpandedTranslateTransform.X = 0;
    }

    private void ShowBundlePaneTargetCard(bool isCollapsed, bool useTransitions)
    {
        var targetCard = isCollapsed ? BundlePaneCollapsedCard : BundlePaneExpandedCard;
        var sourceCard = isCollapsed ? BundlePaneExpandedCard : BundlePaneCollapsedCard;
        var targetTransform = isCollapsed ? BundlePaneCollapsedTranslateTransform : BundlePaneExpandedTranslateTransform;

        sourceCard.Visibility = Visibility.Collapsed;
        sourceCard.Opacity = 0;

        targetCard.Visibility = Visibility.Visible;

        if (!useTransitions)
        {
            targetCard.Opacity = 1;
            targetTransform.X = 0;
            return;
        }

        targetCard.Opacity = 0;
        targetTransform.X = isCollapsed ? -10 : 10;

        if (isCollapsed)
        {
            _bundlePaneCollapsedShowStoryboard.Begin();
            return;
        }

        _bundlePaneExpandedShowStoryboard.Begin();
    }

    private void ApplyBundlePaneFinalColumnWidths(bool isCollapsed)
    {
        BundlePaneColumnDefinition.Width = isCollapsed
            ? new GridLength(BundlePaneCollapsedWidth)
            : ExpandedBundlePaneColumnWidth;
        AssetPaneColumnDefinition.Width = isCollapsed
            ? BalancedContentPaneColumnWidth
            : ExpandedAssetPaneColumnWidth;
        InspectorPaneColumnDefinition.Width = isCollapsed
            ? BalancedContentPaneColumnWidth
            : ExpandedInspectorPaneColumnWidth;
    }

    private (double Left, double Center, double Right) GetBundlePaneTargetWidths(double totalWidth, bool isCollapsed)
    {
        if (isCollapsed)
        {
            var collapsedLeftWidth = Math.Min(BundlePaneCollapsedWidth, totalWidth);
            var remainingWidth = Math.Max(0d, totalWidth - collapsedLeftWidth);
            var balancedWidth = remainingWidth / 2d;
            return (collapsedLeftWidth, balancedWidth, balancedWidth);
        }

        const double expandedRatioSum = 1.08d + 1.56d + 1.36d;
        return (
            totalWidth * 1.08d / expandedRatioSum,
            totalWidth * 1.56d / expandedRatioSum,
            totalWidth * 1.36d / expandedRatioSum);
    }

    private async Task AnimateBundlePaneWidthsAsync(
        double startLeftWidth,
        double startCenterWidth,
        double startRightWidth,
        double targetLeftWidth,
        double targetCenterWidth,
        double targetRightWidth,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawProgress = stopwatch.Elapsed.TotalMilliseconds / BundlePaneTransitionDurationMilliseconds;
            var progress = Math.Clamp(rawProgress, 0d, 1d);
            var easedProgress = EaseInOutCubic(progress);

            SetBundlePaneColumnWidths(
                Lerp(startLeftWidth, targetLeftWidth, easedProgress),
                Lerp(startCenterWidth, targetCenterWidth, easedProgress),
                Lerp(startRightWidth, targetRightWidth, easedProgress));

            if (progress >= 1d)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(16), cancellationToken);
        }
    }

    private void SetBundlePaneColumnWidths(double leftWidth, double centerWidth, double rightWidth)
    {
        BundlePaneColumnDefinition.Width = new GridLength(Math.Max(0d, leftWidth), GridUnitType.Pixel);
        AssetPaneColumnDefinition.Width = new GridLength(Math.Max(0d, centerWidth), GridUnitType.Pixel);
        InspectorPaneColumnDefinition.Width = new GridLength(Math.Max(0d, rightWidth), GridUnitType.Pixel);
    }

    private static double Lerp(double start, double end, double progress) => start + ((end - start) * progress);

    private static double EaseInOutCubic(double progress)
    {
        if (progress < 0.5d)
        {
            return 4d * progress * progress * progress;
        }

        return 1d - (Math.Pow(-2d * progress + 2d, 3d) / 2d);
    }

    /// <summary>
    /// 初始化左侧资源包搜索框的展开与收起动画。
    /// 动画采用高度、透明度和轻微位移组合，
    /// 让搜索区域更接近 Win11 面板里常见的柔和展开体验。
    /// </summary>
    private void InitializeBundleSearchAnimations()
    {
        var showHeightAnimation = new DoubleAnimation
        {
            From = 0,
            To = BundleSearchHostExpandedHeight,
            Duration = TimeSpan.FromMilliseconds(220),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showHeightAnimation, BundleSearchHost);
        Storyboard.SetTargetProperty(showHeightAnimation, nameof(Height));

        var showOpacityAnimation = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showOpacityAnimation, BundleSearchHost);
        Storyboard.SetTargetProperty(showOpacityAnimation, nameof(Opacity));

        var showTranslateAnimation = new DoubleAnimation
        {
            From = -6,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(showTranslateAnimation, BundleSearchTranslateTransform);
        Storyboard.SetTargetProperty(showTranslateAnimation, nameof(TranslateTransform.Y));

        _bundleSearchShowStoryboard.Children.Add(showHeightAnimation);
        _bundleSearchShowStoryboard.Children.Add(showOpacityAnimation);
        _bundleSearchShowStoryboard.Children.Add(showTranslateAnimation);

        var hideHeightAnimation = new DoubleAnimation
        {
            From = BundleSearchHostExpandedHeight,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(180),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideHeightAnimation, BundleSearchHost);
        Storyboard.SetTargetProperty(hideHeightAnimation, nameof(Height));

        var hideOpacityAnimation = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideOpacityAnimation, BundleSearchHost);
        Storyboard.SetTargetProperty(hideOpacityAnimation, nameof(Opacity));

        var hideTranslateAnimation = new DoubleAnimation
        {
            From = 0,
            To = -6,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        Storyboard.SetTarget(hideTranslateAnimation, BundleSearchTranslateTransform);
        Storyboard.SetTargetProperty(hideTranslateAnimation, nameof(TranslateTransform.Y));

        _bundleSearchHideStoryboard.Children.Add(hideHeightAnimation);
        _bundleSearchHideStoryboard.Children.Add(hideOpacityAnimation);
        _bundleSearchHideStoryboard.Children.Add(hideTranslateAnimation);
        _bundleSearchHideStoryboard.Completed += OnBundleSearchHideCompleted;
    }

    private void UpdateBundleSearchState()
    {
        UpdateBundleSearchState(useTransitions: true);
    }

    private void UpdateBundleSearchState(bool useTransitions)
    {
        if (ViewModel.IsBundleSearchExpanded)
        {
            _bundleSearchHideStoryboard.Stop();
            BundleSearchHost.Visibility = Visibility.Visible;

            if (!useTransitions)
            {
                BundleSearchHost.Height = BundleSearchHostExpandedHeight;
                BundleSearchHost.Opacity = 1;
                BundleSearchTranslateTransform.Y = 0;
                return;
            }

            BundleSearchHost.Height = 0;
            BundleSearchHost.Opacity = 0;
            BundleSearchTranslateTransform.Y = -6;
            _bundleSearchShowStoryboard.Begin();
            return;
        }

        if (!useTransitions)
        {
            BundleSearchHost.Visibility = Visibility.Collapsed;
            BundleSearchHost.Height = 0;
            BundleSearchHost.Opacity = 0;
            BundleSearchTranslateTransform.Y = -6;
            return;
        }

        if (BundleSearchHost.Visibility == Visibility.Visible)
        {
            _bundleSearchShowStoryboard.Stop();
            _bundleSearchHideStoryboard.Begin();
        }
    }

    private void OnBundleSearchHideCompleted(object? sender, object e)
    {
        if (ViewModel.IsBundleSearchExpanded)
        {
            return;
        }

        BundleSearchHost.Visibility = Visibility.Collapsed;
        BundleSearchHost.Height = 0;
        BundleSearchHost.Opacity = 0;
        BundleSearchTranslateTransform.Y = -6;
    }

}
