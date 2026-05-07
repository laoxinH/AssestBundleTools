using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.Views.BatchTasks;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 工作台主页面。
/// </summary>
/// <remarks>
/// 当前仍由 <see cref="BundleExplorerPage" /> 承载文档里约定的“三栏主工作区”结构，
/// 当前阶段重点是先把导航、只读浏览骨架和轻量操作反馈稳定下来。
/// </remarks>
public sealed partial class BundleExplorerPage : Page, IAssetDocumentTabDragHost, IAssetDocumentGroupDragHost
{
    private readonly BundleExplorerPageChromeCoordinator _chromeCoordinator;
    private readonly BundleExplorerPageNavigationCoordinator _navigationCoordinator;
    private readonly BundleExplorerPagePaneLayoutCoordinator _paneLayoutCoordinator;
    private readonly BundleExplorerPageTabCoordinator _tabCoordinator;
    private readonly AssetEditorWindowCoordinator _assetEditorWindowCoordinator;
    private WorkspaceNavigationRequest? _workspaceNavigationRequest;

    public BundleExplorerPage()
    {
        ViewModel = new BundleExplorerViewModel(
            App.CurrentApp.WorkspaceExplorerGateway,
            App.CurrentApp.WorkspaceAssetInspectionGateway,
            App.CurrentApp.WorkspaceAssetOperationGateway,
            App.CurrentApp.AssetEditDraftRestorePlanGateway,
            App.CurrentApp.AppLogService,
            App.CurrentApp.AppNotificationService,
            App.CurrentApp.LocalizationService,
            App.CurrentApp.FilePickerService,
            App.CurrentApp.WorkspaceContextService,
            App.CurrentApp.WorkspaceImportCoordinatorService,
            App.CurrentApp.BackgroundTaskRunner,
            App.CurrentApp.AssetBundleDecryptorRegistry,
            App.CurrentApp.PathNavigationService);

        InitializeComponent();

        _chromeCoordinator = new BundleExplorerPageChromeCoordinator(this);
        _navigationCoordinator = new BundleExplorerPageNavigationCoordinator(this);
        _paneLayoutCoordinator = new BundleExplorerPagePaneLayoutCoordinator(this);
        _tabCoordinator = new BundleExplorerPageTabCoordinator(this);
        _assetEditorWindowCoordinator = new AssetEditorWindowCoordinator(this);
        ViewModel.SetAssetEditorWindowStateSnapshotProvider(
            _assetEditorWindowCoordinator.CaptureOpenWindowMetadata);

        _chromeCoordinator.ApplyLocalizedText();
        _paneLayoutCoordinator.Initialize();

        ViewModel.WorkspaceShell.PageStateChanged += OnWorkspaceShellPageStateChanged;
        ViewModel.BundlePane.PageStateChanged += OnBundlePanePageStateChanged;
        ViewModel.DocumentWorkspace.PageStateChanged += OnDocumentWorkspacePageStateChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public BundleExplorerViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _paneLayoutCoordinator.ApplyInitialStateAsync();
        await _navigationCoordinator.HandleLoadedAsync(_workspaceNavigationRequest);
        await _assetEditorWindowCoordinator.PromptPendingAssetEditorWindowsRestoreAsync();
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
        _navigationCoordinator.OpenWorkspace(request);
    }

    private async void OnOpenWorkspaceButtonClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var request = await App.CurrentApp.WorkspaceDialogService.RequestOpenWorkspaceFromDirectoryAsync(XamlRoot);
        _navigationCoordinator.OpenWorkspace(request);
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
        await _assetEditorWindowCoordinator.HandleMainAssetSelectionChangedAsync();
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

    private void OnBundleTabViewTabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        if (!_tabCoordinator.TryBuildTabOrderSnapshot(sender, out IReadOnlyList<BundleDocumentTabViewModel> orderedTabs))
        {
            return;
        }

        ViewModel.ReorderBundleTabs(orderedTabs);
    }

    private void OnBundleTabContextFlyoutOpening(object sender, object e)
    {
        _tabCoordinator.ConfigureTabContextFlyout(
            sender as MenuFlyout,
            openInNewWindowMenuItemName: null,
            closeOtherMenuItemName: "CloseOtherBundleTabsMenuItem",
            closeAllMenuItemName: "CloseAllBundleTabsMenuItem",
            tabCount: ViewModel.DocumentWorkspace.BundleTabs.Count,
            canOpenInNewWindow: false);
    }

    private async void OnCloseOtherBundleTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (_tabCoordinator.TryGetTaggedValue(sender, out BundleDocumentTabViewModel? bundleTab))
        {
            await ViewModel.CloseOtherBundleTabsAsync(bundleTab);
        }
    }

    private async void OnCloseAllBundleTabsMenuItemClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.CloseAllBundleTabsAsync();
    }

    private void OnBundleSearchToggleButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleBundleSearch();
        _paneLayoutCoordinator.FocusBundleSearchIfExpanded();
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
        var canAcceptImport = ViewModel.WorkspaceShell.HasWorkspace &&
                              e.DataView.Contains(StandardDataFormats.StorageItems);
        e.AcceptedOperation = canAcceptImport
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(canAcceptImport);
    }

    private void OnWorkspaceImportDragLeave(object sender, DragEventArgs e)
    {
        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(false);
    }

    private async void OnWorkspaceImportDrop(object sender, DragEventArgs e)
    {
        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(false);

        if (!ViewModel.WorkspaceShell.HasWorkspace ||
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
        _chromeCoordinator.UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: true, isPressed: false);
    }

    private void OnCollapsedBundlePaneActionPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _chromeCoordinator.UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: false, isPressed: false);
    }

    private void OnCollapsedBundlePaneActionPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _chromeCoordinator.UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: true, isPressed: true);
    }

    private void OnCollapsedBundlePaneActionPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _chromeCoordinator.UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: true, isPressed: false);
    }

    private void OnCollapsedBundlePaneActionPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _chromeCoordinator.UpdateCollapsedBundlePaneActionVisualState(sender, isPointerOver: false, isPressed: false);
    }

    private void OnBundlePaneCollapseButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.ToggleBundlePaneCollapsed();

    private void OnBatchExtractChipButtonClick(object sender, RoutedEventArgs e) =>
        Frame?.Navigate(typeof(BatchTasksPage));

    private void OnReturnToConsoleIconButtonClick(object sender, RoutedEventArgs e) =>
        _navigationCoordinator.NavigateToConsole();

    private void OnSaveAsBundleChipButtonClick(object sender, RoutedEventArgs e) =>
        Frame?.Navigate(typeof(BatchTasksPage));

    private async void OnRestoreAssetEditorWindowsChipButtonClick(object sender, RoutedEventArgs e)
    {
        await _assetEditorWindowCoordinator.RestorePendingAssetEditorWindowsAsync();
    }

    private async void OnInspectorExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private void OnInspectorOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
    }

    private async void OnInspectorResolvedAssetOpenRequested(
        object sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        await _assetEditorWindowCoordinator.HandleMainResolvedAssetOpenRequestedAsync(e.Target);
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

    private void OnWorkspaceShellPageStateChanged(
        object? sender,
        BundleExplorerPageStateChangedEventArgs<BundleExplorerWorkspaceShellPageStateChange> e)
    {
        _assetEditorWindowCoordinator.HandleWorkspaceShellPageStateChanged(e);
        _navigationCoordinator.HandleWorkspaceShellPageStateChanged(e);
    }

    private void OnBundlePanePageStateChanged(
        object? sender,
        BundleExplorerPageStateChangedEventArgs<BundleExplorerBundlePanePageStateChange> e)
    {
        _paneLayoutCoordinator.HandleBundlePanePageStateChanged(e);
    }

    private void OnDocumentWorkspacePageStateChanged(
        object? sender,
        BundleExplorerPageStateChangedEventArgs<BundleExplorerDocumentWorkspacePageStateChange> e)
    {
        _assetEditorWindowCoordinator.HandleDocumentWorkspacePageStateChanged(e);
        _tabCoordinator.HandleDocumentWorkspacePageStateChanged(e);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.FlushWorkspaceBrowserState();
        _assetEditorWindowCoordinator.CloseAll();
        ViewModel.DocumentWorkspace.PageStateChanged -= OnDocumentWorkspacePageStateChanged;
        ViewModel.BundlePane.PageStateChanged -= OnBundlePanePageStateChanged;
        ViewModel.WorkspaceShell.PageStateChanged -= OnWorkspaceShellPageStateChanged;
        _paneLayoutCoordinator.Stop();
        _chromeCoordinator.Stop();
    }

    bool IAssetDocumentTabDragHost.CanStartAssetDocumentTabDrag(AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentNullException.ThrowIfNull(documentTab);

        return !ViewModel.IsBusy &&
               ViewModel.DocumentWorkspace.CurrentDocumentTabs.Contains(documentTab);
    }

    bool IAssetDocumentTabDragHost.CanAcceptAssetDocumentTabDrag(AssetDocumentTabDragSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return !ViewModel.IsBusy &&
               ViewModel.WorkspaceShell.HasWorkspace;
    }

    Task<AssetDocumentTabItemViewModel?> IAssetDocumentTabDragHost.DetachAssetDocumentTabForDragAsync(
        AssetDocumentTabItemViewModel documentTab) =>
        ViewModel.DetachDocumentTabForEditorWindowAsync(documentTab);

    Task<bool> IAssetDocumentTabDragHost.AttachAssetDocumentTabFromDragAsync(
        AssetDocumentTabItemViewModel documentTab) =>
        ViewModel.AttachDocumentTabFromEditorWindowAsync(documentTab);

    Task IAssetDocumentTabDragHost.CompleteAssetDocumentTabDragMoveAsync() =>
        Task.CompletedTask;

    bool IAssetDocumentGroupDragHost.CanStartAssetDocumentGroupDrag()
    {
        return !ViewModel.IsBusy &&
               ViewModel.DocumentWorkspace.HasDocumentTabs;
    }

    bool IAssetDocumentGroupDragHost.CanAcceptAssetDocumentGroupDrag(AssetDocumentGroupDragSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return !ViewModel.IsBusy &&
               ViewModel.WorkspaceShell.HasWorkspace;
    }

    Task<AssetDocumentGroupTransfer?> IAssetDocumentGroupDragHost.DetachAssetDocumentGroupForDragAsync() =>
        ViewModel.DetachActiveAssetDocumentGroupForEditorWindowAsync();

    Task<bool> IAssetDocumentGroupDragHost.AttachAssetDocumentGroupFromDragAsync(
        AssetDocumentGroupTransfer transfer) =>
        ViewModel.AttachAssetDocumentGroupFromEditorWindowAsync(transfer);

    Task IAssetDocumentGroupDragHost.CompleteAssetDocumentGroupDragMoveAsync() =>
        Task.CompletedTask;

    Task IAssetDocumentGroupDragHost.OpenAssetDocumentGroupInNewWindowFromDragAsync() =>
        _assetEditorWindowCoordinator.OpenActiveAssetDocumentGroupInNewWindowAsync();
}
