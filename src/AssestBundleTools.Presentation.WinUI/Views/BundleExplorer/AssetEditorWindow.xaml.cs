using System.ComponentModel;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

/// <summary>
/// 独立资源编辑窗口，第一阶段承载命令式移出的资产文档标签组。
/// </summary>
public sealed partial class AssetEditorWindow : Microsoft.UI.Xaml.Window, IAssetDocumentTabDragHost, IAssetDocumentGroupDragHost
{
    private readonly Func<AssetEditorWindow, AssetDocumentTabItemViewModel, Task> _openDocumentInNewWindowAsync;
    private readonly Func<AssetEditorWindow, Task> _openAssetDocumentGroupInNewWindowAsync;
    private readonly Func<AssetEditorWindow, ResolvedAssetNavigationTarget, Task<bool>> _activateOpenAssetDocumentAsync;
    private readonly AssetEditorWindowAssetTabStateObserver _assetTabStateObserver;
    private bool _isInitialized;
    private bool _isSynchronizingSelection;
    private bool _allowDirectClose;
    private bool _hasDirectCloseBeenRequested;
    private bool _isProcessingCloseRequest;

    public AssetEditorWindow(
        AssetEditorWindowViewModel viewModel,
        Func<AssetEditorWindow, AssetDocumentTabItemViewModel, Task> openDocumentInNewWindowAsync,
        Func<AssetEditorWindow, Task> openAssetDocumentGroupInNewWindowAsync,
        Func<AssetEditorWindow, ResolvedAssetNavigationTarget, Task<bool>> activateOpenAssetDocumentAsync)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _openDocumentInNewWindowAsync = openDocumentInNewWindowAsync ?? throw new ArgumentNullException(nameof(openDocumentInNewWindowAsync));
        _openAssetDocumentGroupInNewWindowAsync = openAssetDocumentGroupInNewWindowAsync ?? throw new ArgumentNullException(nameof(openAssetDocumentGroupInNewWindowAsync));
        _activateOpenAssetDocumentAsync = activateOpenAssetDocumentAsync ?? throw new ArgumentNullException(nameof(activateOpenAssetDocumentAsync));
        InitializeComponent();
        ApplyLocalizedText();
        UpdateWindowTitle();
        TryInitializeWindowLayoutTracking();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.DocumentGroup.PropertyChanged += OnDocumentGroupPropertyChanged;
        _assetTabStateObserver = new AssetEditorWindowAssetTabStateObserver(
            ViewModel.DocumentGroup,
            PublishCurrentGroupSnapshot);
        ViewModel.EmptyGroupRequested += OnEmptyGroupRequested;
        Closed += OnClosed;
    }

    public AssetEditorWindowViewModel ViewModel { get; }

    internal event Action<AssetEditorWindow, AssetEditorWindowGroupSnapshot>? GroupSnapshotChanged;

    internal AssetEditorWindowGroupSnapshot CaptureGroupSnapshot() =>
        AssetEditorWindowGroupSnapshot.FromDocumentGroup(ViewModel.DocumentGroup);

    internal async Task<bool> TryActivateAssetDocumentTabAsync(BundleId bundleId, AssetId assetId)
    {
        if (!ViewModel.DocumentGroup.TryGetAssetTab(bundleId, assetId, out var assetTab))
        {
            return false;
        }

        await ViewModel.ActivateAssetDocumentTabAsync(assetTab);
        SynchronizeSelection(ViewModel.DocumentGroup.DocumentTabs.FirstOrDefault(tab => tab.Wraps(assetTab)));
        BringToFront();
        return true;
    }

    internal async Task<bool> TryActivateSpineGroupDocumentTabAsync(BundleId bundleId, string groupId)
    {
        if (!ViewModel.DocumentGroup.TryGetSpineGroupTab(bundleId, groupId, out var spineGroupTab))
        {
            return false;
        }

        await ViewModel.ActivateSpineGroupDocumentTabAsync(spineGroupTab);
        SynchronizeSelection(ViewModel.DocumentGroup.DocumentTabs.FirstOrDefault(tab => tab.Wraps(spineGroupTab)));
        BringToFront();
        return true;
    }

    private async void OnContentRootLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await ViewModel.ActivateDocumentTabAsync(ViewModel.DocumentGroup.ActiveDocumentTab);
    }

    private void OnAssetDocumentGroupDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        AssetDocumentGroupDragCoordinator.StartDrag(this, args);
    }

    private async void OnAssetDocumentGroupDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        SetAssetDocumentGroupDropPreviewVisible(false);
        await AssetDocumentGroupDragCoordinator.HandleDropCompletedAsync(this, args);
    }

    private void OnAssetDocumentGroupDragEnter(object sender, DragEventArgs e)
    {
        SetAssetDocumentGroupDropPreviewVisible(CanShowAssetDocumentHostDropPreview(e));
    }

    private void OnAssetDocumentGroupDragLeave(object sender, DragEventArgs e)
    {
        SetAssetDocumentGroupDropPreviewVisible(false);
    }

    private void OnAssetDocumentGroupDragOver(object sender, DragEventArgs e)
    {
        if (AssetDocumentTabDragCoordinator.CanShowDropPreview(this, e))
        {
            SetAssetDocumentGroupDropPreviewVisible(true);
            AssetDocumentTabDragCoordinator.HandleDragOver(this, e);
            return;
        }

        SetAssetDocumentGroupDropPreviewVisible(
            AssetDocumentGroupDragCoordinator.CanShowDropPreview(this, e));
        AssetDocumentGroupDragCoordinator.HandleDragOver(this, e);
    }

    private async void OnAssetDocumentGroupDrop(object sender, DragEventArgs e)
    {
        SetAssetDocumentGroupDropPreviewVisible(false);
        if (await AssetDocumentTabDragCoordinator.HandleDropAsync(this, e))
        {
            return;
        }

        await AssetDocumentGroupDragCoordinator.HandleDropAsync(this, e);
    }

    private bool CanShowAssetDocumentHostDropPreview(DragEventArgs e) =>
        AssetDocumentTabDragCoordinator.CanShowDropPreview(this, e) ||
        AssetDocumentGroupDragCoordinator.CanShowDropPreview(this, e);

    private void SetAssetDocumentGroupDropPreviewVisible(bool isVisible)
    {
        AssetDocumentGroupDropPreviewOverlay.Visibility = isVisible
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void OnExtractButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private async void OnModifyButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ModifyFromFileReplacementSourceAsync();
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

    private async void OnApplySpriteEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplySpriteEditAsync();
    }

    private async void OnUndoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.UndoAppliedReplacementAsync();
    }

    private async void OnRedoReplacementButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RedoAppliedReplacementAsync();
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

    private async void OnInspectorSpriteTextureReplacementRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectSpriteTextureReplacementSourceAsync();
    }

    private async void OnInspectorModifyFromFileReplacementSourceRequested(object sender, EventArgs e)
    {
        await ViewModel.ModifyFromFileReplacementSourceAsync();
    }

    private async void OnInspectorApplyReplacementRequested(object sender, EventArgs e)
    {
        await ViewModel.ApplyPendingReplacementAsync();
    }

    private async void OnInspectorUndoReplacementRequested(
        object sender,
        InspectorChangeRecordActionRequestedEventArgs e)
    {
        await ViewModel.UndoAppliedReplacementAsync(e.OperationId);
    }

    private async void OnInspectorRedoReplacementRequested(
        object sender,
        InspectorChangeRecordActionRequestedEventArgs e)
    {
        await ViewModel.RedoAppliedReplacementAsync(e.OperationId);
    }

    private async void OnInspectorDeleteChangeRecordRequested(
        object sender,
        InspectorChangeRecordActionRequestedEventArgs e)
    {
        await ViewModel.DeleteChangeRecordAsync(e.OperationId);
    }

    private async void OnInspectorSpineAtlasPageSelectionRequested(
        object sender,
        SpineAtlasPageSelectionRequestedEventArgs e)
    {
        await ViewModel.SelectActiveSpineGroupAtlasPageAsync(e.Option);
    }

    private void OnPreviewSurfaceOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
    }

    private async void OnPreviewSurfaceSpriteTextureReplacementRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectSpriteTextureReplacementSourceAsync();
    }

    private async void OnResolvedAssetOpenRequested(
        object sender,
        ResolvedAssetOpenRequestedEventArgs e)
    {
        if (await _activateOpenAssetDocumentAsync(this, e.Target))
        {
            return;
        }

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
        if (e.PropertyName == nameof(AssetDocumentGroupViewModel.ActiveDocumentTab))
        {
            SynchronizeSelection();
        }

        PublishCurrentGroupSnapshot();
        Bindings.Update();
    }

    private void OnEmptyGroupRequested(object? sender, EventArgs e)
    {
        CloseWithoutReturningToMainWindow();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        PublishCurrentLayout();
        if (_appWindow is not null)
        {
            _appWindow.Closing -= OnAppWindowClosing;
            _appWindow.Changed -= OnAppWindowChanged;
            _appWindow = null;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.DocumentGroup.PropertyChanged -= OnDocumentGroupPropertyChanged;
        _assetTabStateObserver.Dispose();
        ViewModel.EmptyGroupRequested -= OnEmptyGroupRequested;
        Closed -= OnClosed;
        ViewModel.Dispose();
        ViewModel.DocumentGroup.Clear();
    }

    private void PublishCurrentGroupSnapshot()
    {
        GroupSnapshotChanged?.Invoke(this, CaptureGroupSnapshot());
    }

    private void ApplyLocalizedText()
    {
        ToolTipService.SetToolTip(
            AssetEditorGroupDragHandle,
            UiText.Get(ResourceKeys.AssetEditorWindowDragGroupHandleToolTip));
        ExtractButton.Content = UiText.Get(ResourceKeys.InspectorExtractButton);
        ModifyButton.Content = UiText.Get(ResourceKeys.InspectorModifyButton);
        SelectReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectReplacementButton);
        SelectFileReplacementButton.Content = UiText.Get(ResourceKeys.InspectorSelectFileReplacementButton);
        ApplyReplacementButton.Content = UiText.Get(ResourceKeys.InspectorApplyReplacementButton);
        ApplySpriteEditButton.Content = UiText.Get(ResourceKeys.InspectorApplySpriteEditButton);
        UndoReplacementButton.Content = UiText.Get(ResourceKeys.InspectorUndoReplacementButton);
        RedoReplacementButton.Content = UiText.Get(ResourceKeys.InspectorRedoReplacementButton);
    }

    private void UpdateWindowTitle()
    {
        Title = ViewModel.WindowTitle;
    }

    internal void CloseWithoutReturningToMainWindow()
    {
        if (_hasDirectCloseBeenRequested)
        {
            return;
        }

        _hasDirectCloseBeenRequested = true;
        _allowDirectClose = true;
        Close();
    }

}
