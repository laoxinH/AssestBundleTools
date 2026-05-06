using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 独立资源编辑窗口，第一阶段承载命令式移出的资产文档标签组。
/// </summary>
public sealed partial class AssetEditorWindow : Microsoft.UI.Xaml.Window, IAssetDocumentTabDragHost
{
    private readonly Func<AssetEditorWindow, AssetDocumentTabViewModel, Task> _openAssetDocumentInNewWindowAsync;
    private readonly Func<AssetEditorWindow, Task> _openAssetDocumentGroupInNewWindowAsync;
    private bool _isInitialized;
    private bool _isSynchronizingSelection;

    public AssetEditorWindow(
        AssetEditorWindowViewModel viewModel,
        Func<AssetEditorWindow, AssetDocumentTabViewModel, Task> openAssetDocumentInNewWindowAsync,
        Func<AssetEditorWindow, Task> openAssetDocumentGroupInNewWindowAsync)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _openAssetDocumentInNewWindowAsync = openAssetDocumentInNewWindowAsync ?? throw new ArgumentNullException(nameof(openAssetDocumentInNewWindowAsync));
        _openAssetDocumentGroupInNewWindowAsync = openAssetDocumentGroupInNewWindowAsync ?? throw new ArgumentNullException(nameof(openAssetDocumentGroupInNewWindowAsync));
        InitializeComponent();
        ApplyLocalizedText();
        UpdateWindowTitle();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.DocumentGroup.PropertyChanged += OnDocumentGroupPropertyChanged;
        ViewModel.EmptyGroupRequested += OnEmptyGroupRequested;
        Closed += OnClosed;
    }

    public AssetEditorWindowViewModel ViewModel { get; }

    private async void OnContentRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await ViewModel.ActivateAssetDocumentTabAsync(ViewModel.DocumentGroup.ActiveAssetTab);
    }

    private async void OnAssetEditorTabViewSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        if (AssetEditorTabView.SelectedItem is AssetDocumentTabViewModel assetTab)
        {
            await ViewModel.ActivateAssetDocumentTabAsync(assetTab);
        }
    }

    private async void OnAssetEditorTabViewTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
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

    private void OnAssetEditorTabViewTabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        if (!TryBuildTabOrderSnapshot(sender, out IReadOnlyList<AssetDocumentTabViewModel> orderedTabs))
        {
            return;
        }

        ViewModel.ReorderAssetDocumentTabs(orderedTabs);
        AssetDocumentTabDragCoordinator.CompleteDrag();
    }

    private void OnAssetEditorTabViewTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        AssetDocumentTabDragCoordinator.StartDrag(this, args);
    }

    private async void OnAssetEditorTabViewTabDroppedOutside(
        TabView sender,
        TabViewTabDroppedOutsideEventArgs args)
    {
        try
        {
            if (AssetDocumentTabDragCoordinator.TryResolveAssetTab(args.Item, args.Tab, out var assetTab))
            {
                await _openAssetDocumentInNewWindowAsync(this, assetTab);
            }
        }
        finally
        {
            AssetDocumentTabDragCoordinator.CompleteDrag();
        }
    }

    private void OnAssetEditorTabViewTabStripDragOver(object sender, DragEventArgs e)
    {
        AssetDocumentTabDragCoordinator.HandleDragOver(this, e);
    }

    private async void OnAssetEditorTabViewTabStripDrop(object sender, DragEventArgs e)
    {
        await AssetDocumentTabDragCoordinator.HandleDropAsync(this, e);
    }

    private void OnAssetEditorTabContextFlyoutOpening(object sender, object e)
    {
        if (sender is not MenuFlyout flyout)
        {
            return;
        }

        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            switch (item.Name)
            {
                case "MoveAssetDocumentToMainWindowMenuItem":
                    item.Text = UiText.Get(ResourceKeys.AssetEditorWindowMoveToMainWindowText);
                    item.IsEnabled = ViewModel.DocumentGroup.HasAssetTabs && !ViewModel.IsBusy;
                    break;
                case "CloseAssetDocumentTabMenuItem":
                    item.Text = UiText.Get(ResourceKeys.AssetEditorWindowTabContextCloseText);
                    item.IsEnabled = ViewModel.DocumentGroup.HasAssetTabs;
                    break;
            }
        }
    }

    private async void OnMoveAssetDocumentToMainWindowMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TryGetTaggedValue(sender, out AssetDocumentTabViewModel? assetTab))
        {
            await ViewModel.MoveAssetDocumentToMainWindowAsync(assetTab);
        }
    }

    private async void OnCloseAssetDocumentTabMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (TryGetTaggedValue(sender, out AssetDocumentTabViewModel? assetTab))
        {
            await ViewModel.CloseAssetDocumentTabAsync(assetTab);
        }
    }

    private async void OnMoveActiveAssetDocumentToMainWindowButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveActiveAssetDocumentToMainWindowAsync();
    }

    private async void OnMoveAssetDocumentGroupToMainWindowButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.MoveAssetDocumentGroupToMainWindowAsync();
    }

    private async void OnOpenAssetDocumentGroupInNewWindowButtonClick(object sender, RoutedEventArgs e)
    {
        await _openAssetDocumentGroupInNewWindowAsync(this);
    }

    private async void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private async void OnSelectReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectWorkspaceReplacementSourceAsync();
    }

    private async void OnSelectFileReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectFileReplacementSourceAsync();
    }

    private async void OnApplyReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplyPendingReplacementAsync();
    }

    private async void OnUndoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.UndoAppliedReplacementAsync();
    }

    private async void OnInspectorExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private void OnInspectorOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
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

    private void OnPreviewSurfaceOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
    }

    private async void OnResolvedAssetOpenRequested(
        object sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        await ViewModel.OpenResolvedAssetAsync(e.Target);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssetEditorWindowViewModel.WindowTitle))
        {
            UpdateWindowTitle();
        }

        Bindings.Update();
    }

    private void OnDocumentGroupPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AssetDocumentGroupViewModel.ActiveAssetTab))
        {
            SynchronizeSelection();
        }

        Bindings.Update();
    }

    private void OnEmptyGroupRequested(object? sender, EventArgs e)
    {
        Close();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.DocumentGroup.PropertyChanged -= OnDocumentGroupPropertyChanged;
        ViewModel.EmptyGroupRequested -= OnEmptyGroupRequested;
        Closed -= OnClosed;
        ViewModel.DocumentGroup.Clear();
    }

    private void ApplyLocalizedText()
    {
        MoveToMainWindowButton.Content = UiText.Get(ResourceKeys.AssetEditorWindowMoveToMainWindowText);
        MoveGroupToMainWindowButton.Content = UiText.Get(ResourceKeys.AssetEditorWindowMoveGroupToMainWindowText);
        OpenGroupInNewWindowButton.Content = UiText.Get(ResourceKeys.AssetEditorWindowOpenGroupInNewWindowText);
        ExtractButton.Content = UiText.Get(ResourceKeys.InspectorExtractButton);
        SelectReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectReplacementButton);
        SelectFileReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectFileReplacementButton);
        ApplyReplacementButton.Content = UiText.Get(ResourceKeys.InspectorApplyReplacementButton);
        UndoReplacementButton.Content = UiText.Get(ResourceKeys.InspectorUndoReplacementButton);
    }

    private void UpdateWindowTitle()
    {
        Title = ViewModel.WindowTitle;
    }

    private void SynchronizeSelection()
    {
        if (_isSynchronizingSelection)
        {
            return;
        }

        _isSynchronizingSelection = true;
        try
        {
            if (!ReferenceEquals(AssetEditorTabView.SelectedItem, ViewModel.DocumentGroup.ActiveAssetTab))
            {
                AssetEditorTabView.SelectedItem = ViewModel.DocumentGroup.ActiveAssetTab;
            }
        }
        finally
        {
            _isSynchronizingSelection = false;
        }
    }

    private static bool TryBuildTabOrderSnapshot<TTabViewModel>(
        TabView tabView,
        out IReadOnlyList<TTabViewModel> orderedTabs)
        where TTabViewModel : class
    {
        ArgumentNullException.ThrowIfNull(tabView);

        var resolvedItems = new List<TTabViewModel>(tabView.TabItems.Count);
        foreach (var item in tabView.TabItems)
        {
            if (item is TTabViewModel typedItem)
            {
                resolvedItems.Add(typedItem);
                continue;
            }

            if (item is FrameworkElement { DataContext: TTabViewModel dataContextItem })
            {
                resolvedItems.Add(dataContextItem);
                continue;
            }

            orderedTabs = Array.Empty<TTabViewModel>();
            return false;
        }

        orderedTabs = resolvedItems;
        return true;
    }

    private static bool TryGetTaggedValue<TValue>(
        object sender,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TValue? value)
        where TValue : class
    {
        value = (sender as FrameworkElement)?.Tag as TValue;
        return value is not null;
    }

    bool IAssetDocumentTabDragHost.CanStartAssetDocumentTabDrag(AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        return !ViewModel.IsBusy &&
               ViewModel.DocumentGroup.AssetTabs.Contains(assetTab);
    }

    bool IAssetDocumentTabDragHost.CanAcceptAssetDocumentTabDrag(AssetDocumentTabDragSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return !ViewModel.IsBusy;
    }

    Task<AssetDocumentTabViewModel?> IAssetDocumentTabDragHost.DetachAssetDocumentTabForDragAsync(
        AssetDocumentTabViewModel assetTab) =>
        ViewModel.DetachAssetDocumentTabForExternalHostAsync(assetTab);

    Task<bool> IAssetDocumentTabDragHost.AttachAssetDocumentTabFromDragAsync(
        AssetDocumentTabViewModel assetTab) =>
        ViewModel.AttachAssetDocumentTabFromExternalHostAsync(assetTab);

    Task IAssetDocumentTabDragHost.CompleteAssetDocumentTabDragMoveAsync() =>
        ViewModel.CompleteExternalAssetDocumentMoveAsync();
}
