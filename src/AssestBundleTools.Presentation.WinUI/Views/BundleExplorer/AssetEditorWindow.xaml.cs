using System.ComponentModel;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;
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
        : this(
            viewModel,
            openDocumentInNewWindowAsync,
            openAssetDocumentGroupInNewWindowAsync,
            activateOpenAssetDocumentAsync,
            rememberedSize: null)
    {
    }

    internal AssetEditorWindow(
        AssetEditorWindowViewModel viewModel,
        Func<AssetEditorWindow, AssetDocumentTabItemViewModel, Task> openDocumentInNewWindowAsync,
        Func<AssetEditorWindow, Task> openAssetDocumentGroupInNewWindowAsync,
        Func<AssetEditorWindow, ResolvedAssetNavigationTarget, Task<bool>> activateOpenAssetDocumentAsync,
        DpiAwareWindowSize? rememberedSize)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _openDocumentInNewWindowAsync = openDocumentInNewWindowAsync ?? throw new ArgumentNullException(nameof(openDocumentInNewWindowAsync));
        _openAssetDocumentGroupInNewWindowAsync = openAssetDocumentGroupInNewWindowAsync ?? throw new ArgumentNullException(nameof(openAssetDocumentGroupInNewWindowAsync));
        _activateOpenAssetDocumentAsync = activateOpenAssetDocumentAsync ?? throw new ArgumentNullException(nameof(activateOpenAssetDocumentAsync));
        InitializeComponent();
        ModernWindowAppearanceService.TryApplySystemBackdrop(this);
        ApplyLocalizedText();
        UpdateWindowTitle();
        TryInitializeWindowLayoutTracking(rememberedSize);

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

    private async void OnReplaceSpriteTextureButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SelectSpriteTextureReplacementSourceAsync();
    }

    private async void OnSaveSpriteEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplySpriteEditAsync();
    }

    private async void OnExitSpriteEditButtonClick(object sender, RoutedEventArgs e)
    {
        await TryExitSpriteEditModeAsync();
    }

    private void OnUndoSpriteEditButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.UndoSpriteEditForActiveAsset();
    }

    private void OnRedoSpriteEditButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.RedoSpriteEditForActiveAsset();
    }

    private async void OnSaveSpineEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ApplySpineEditAsync();
    }

    private async void OnExitSpineEditButtonClick(object sender, RoutedEventArgs e)
    {
        await TryExitSpineEditModeAsync();
    }

    private async void OnUndoSpineEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.UndoSpineEditForActiveGroupAsync();
    }

    private async void OnRedoSpineEditButtonClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.RedoSpineEditForActiveGroupAsync();
    }

    private async void OnSpineEditPartFileSelectionRequested(
        object sender,
        SpineEditPartFileSelectionRequestedEventArgs e)
    {
        await ViewModel.SelectSpineEditPartFileAsync(e.Part);
    }

    private async void OnSpineEditBatchMatchRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectSpineEditBatchSourceDirectoryAsync();
    }

    private async void OnInspectorExtractRequested(object sender, EventArgs e)
    {
        await ViewModel.ExtractSelectedAssetAsync();
    }

    private void OnInspectorOpenPreviewLocationRequested(object sender, EventArgs e)
    {
        ViewModel.OpenCurrentPreviewLocation();
    }

    private async void OnInspectorLoadMoreTextRequested(object sender, EventArgs e)
    {
        await ViewModel.LoadMorePreviewTextLinesAsync();
    }

    private async void OnInspectorSelectFileReplacementSourceRequested(object sender, EventArgs e)
    {
        await ViewModel.SelectFileReplacementSourceAsync();
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

    private async void OnPreviewSurfaceLoadMoreTextRequested(object sender, EventArgs e)
    {
        await ViewModel.LoadMorePreviewTextLinesAsync();
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

        if (e.PropertyName is nameof(AssetEditorWindowViewModel.SaveTextEditButtonContentKey) or
            nameof(AssetEditorWindowViewModel.SaveTextEditButtonToolTipKey) or
            nameof(AssetEditorWindowViewModel.IsTextEditLoading) or
            nameof(AssetEditorWindowViewModel.IsTextEditSaving) or
            nameof(AssetEditorWindowViewModel.IsTextEditBusy))
        {
            ApplySaveTextEditButtonText();
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
        _windowSizeTracker = null;

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

    private async Task<bool> TryExitSpriteEditModeAsync()
    {
        var image = ViewModel.Inspector.Preview.Image;
        if (!image.IsSpriteEditModeActive)
        {
            return true;
        }

        if (!image.HasUnsavedSpriteEditChanges)
        {
            ViewModel.ExitSpriteEditModeForActiveAsset(discardChanges: false);
            return true;
        }

        if (ContentRoot.XamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = ContentRoot.XamlRoot,
            Title = UiText.Get(ResourceKeys.InspectorUnsavedSpriteEditDialogTitle),
            Content = UiText.Get(ResourceKeys.InspectorUnsavedSpriteEditDialogDescription),
            PrimaryButtonText = UiText.Get(ResourceKeys.InspectorApplySpineEditButton),
            SecondaryButtonText = UiText.Get(ResourceKeys.CommonDiscardButton),
            CloseButtonText = UiText.Get(ResourceKeys.CommonCancelButton),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (await ViewModel.ApplySpriteEditAsync())
            {
                ViewModel.ExitSpriteEditModeForActiveAsset(discardChanges: false);
                return true;
            }

            return false;
        }

        if (result == ContentDialogResult.Secondary)
        {
            ViewModel.ExitSpriteEditModeForActiveAsset(discardChanges: true);
            return true;
        }

        return false;
    }

    private async Task<bool> TryExitSpineEditModeAsync()
    {
        var spineGroupTab = ViewModel.DocumentGroup.ActiveSpineGroupTab;
        if (spineGroupTab?.IsSpineEditModeActive != true)
        {
            return true;
        }

        if (spineGroupTab.SpineEditDraft?.HasChanges != true)
        {
            await ViewModel.ExitSpineEditModeForActiveGroupAsync(discardChanges: false);
            return true;
        }

        if (ContentRoot.XamlRoot is null)
        {
            return false;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = ContentRoot.XamlRoot,
            Title = UiText.Get(ResourceKeys.InspectorUnsavedSpineEditDialogTitle),
            Content = UiText.Get(ResourceKeys.InspectorUnsavedSpineEditDialogDescription),
            PrimaryButtonText = UiText.Get(ResourceKeys.InspectorApplySpriteEditButton),
            SecondaryButtonText = UiText.Get(ResourceKeys.CommonDiscardButton),
            CloseButtonText = UiText.Get(ResourceKeys.CommonCancelButton),
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return await ViewModel.ApplySpineEditAsync();
        }

        if (result == ContentDialogResult.Secondary)
        {
            await ViewModel.ExitSpineEditModeForActiveGroupAsync(discardChanges: true);
            return true;
        }

        return false;
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
