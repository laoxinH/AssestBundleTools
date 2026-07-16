using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class AssetPaneView
{
    private bool _isMultiSelectionDragGestureActive;
    private uint _multiSelectionDragPointerId;
    private object? _lastMultiSelectionDragRowContext;

    private void OnExitMultiSelectionButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.ExitMultiSelectionMode();
        UpdateListSelectionMode();
        SynchronizeCurrentSelectionFromViewModel();
        FocusCurrentListView();
    }

    private void OnSelectAllMultiSelectionButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.SelectAllVisibleItems();
        SynchronizeCurrentSelectionFromViewModel();
        FocusCurrentListView();
    }

    private void OnInvertMultiSelectionButtonClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        ViewModel.InvertVisibleSelection();
        SynchronizeCurrentSelectionFromViewModel();
        FocusCurrentListView();
    }

    private void ApplyMultiSelectionLocalizedText()
    {
        SetIconButtonText(
            ExitMultiSelectionButton,
            UiText.Get(ResourceKeys.AssetPaneViewExitMultiSelectionButtonToolTip));
        SetIconButtonText(
            SelectAllMultiSelectionButton,
            UiText.Get(ResourceKeys.AssetPaneViewSelectAllMultiSelectionButtonToolTip));
        SetIconButtonText(
            InvertMultiSelectionButton,
            UiText.Get(ResourceKeys.AssetPaneViewInvertMultiSelectionButtonToolTip));
        SetIconButtonText(
            BatchExtractSelectedButton,
            UiText.Get(ResourceKeys.AssetPaneViewBatchExtractSelectedButton));
    }

    private void SynchronizeCurrentSelectionFromViewModel()
    {
        if (ViewModel?.IsSpineGroupViewMode == true)
        {
            SynchronizeSpineGroupSelectionFromViewModel();
            return;
        }

        SynchronizeSelectionFromViewModel();
    }

    private void FocusCurrentListView()
    {
        if (ViewModel?.IsSpineGroupViewMode == true)
        {
            SpineGroupListView.Focus(FocusState.Programmatic);
            return;
        }

        AssetListView.Focus(FocusState.Programmatic);
    }

    private void TryHandleMultiSelectionRowPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel?.IsMultiSelectionMode != true ||
            sender is not FrameworkElement { Tag: not null } row)
        {
            return;
        }

        var pointer = e.GetCurrentPoint(row);
        if (pointer.Properties.IsRightButtonPressed ||
            !pointer.Properties.IsLeftButtonPressed)
        {
            return;
        }

        var handled = row.Tag switch
        {
            AssetListEntryViewModel assetEntry when ViewModel.IsAssetViewMode =>
                _selectionCoordinator.ToggleAssetSelectionInMultiSelectionMode(assetEntry),
            SpineAssetGroupEntryViewModel spineGroupEntry when ViewModel.IsSpineGroupViewMode =>
                _selectionCoordinator.ToggleSpineGroupSelectionInMultiSelectionMode(spineGroupEntry),
            _ => false
        };

        if (!handled)
        {
            return;
        }

        _isMultiSelectionDragGestureActive = true;
        _multiSelectionDragPointerId = e.Pointer.PointerId;
        _lastMultiSelectionDragRowContext = row.Tag;
        e.Handled = true;
        FocusCurrentListView();
    }

    private void TryHandleMultiSelectionRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isMultiSelectionDragGestureActive ||
            ViewModel?.IsMultiSelectionMode != true ||
            sender is not FrameworkElement { Tag: not null } row)
        {
            return;
        }

        if (e.Pointer.PointerId != _multiSelectionDragPointerId)
        {
            return;
        }

        var pointer = e.GetCurrentPoint(row);
        if (!pointer.Properties.IsLeftButtonPressed ||
            pointer.Properties.IsRightButtonPressed)
        {
            EndMultiSelectionDragGesture();
            return;
        }

        var handled = row.Tag switch
        {
            AssetListEntryViewModel assetEntry when ViewModel.IsAssetViewMode =>
                TryContinueAssetMultiSelectionDrag(assetEntry),
            SpineAssetGroupEntryViewModel spineGroupEntry when ViewModel.IsSpineGroupViewMode =>
                TryContinueSpineGroupMultiSelectionDrag(spineGroupEntry),
            _ => false
        };

        if (handled)
        {
            e.Handled = true;
        }
    }

    private void OnAssetPaneListPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!_isMultiSelectionDragGestureActive ||
            ViewModel?.IsMultiSelectionMode != true ||
            sender is not ListView listView)
        {
            return;
        }

        if (e.Pointer.PointerId != _multiSelectionDragPointerId)
        {
            return;
        }

        var pointer = e.GetCurrentPoint(listView);
        if (pointer.Properties.IsRightButtonPressed)
        {
            EndMultiSelectionDragGesture();
            return;
        }

        var pointerPosition = pointer.Position;
        TryContinueMultiSelectionDragFromListPointerPosition(listView, pointerPosition);
        DispatcherQueue.TryEnqueue(
            DispatcherQueuePriority.Low,
            () => TryContinueMultiSelectionDragFromListPointerPosition(listView, pointerPosition));
    }

    private void TryContinueMultiSelectionDragFromListPointerPosition(ListView listView, Point pointerPosition)
    {
        if (!_isMultiSelectionDragGestureActive ||
            ViewModel?.IsMultiSelectionMode != true)
        {
            return;
        }

        var rowContext = FindRowContextAtPointerPosition(listView, pointerPosition);
        _ = rowContext switch
        {
            AssetListEntryViewModel assetEntry when ViewModel.IsAssetViewMode =>
                TryContinueAssetMultiSelectionDrag(assetEntry),
            SpineAssetGroupEntryViewModel spineGroupEntry when ViewModel.IsSpineGroupViewMode =>
                TryContinueSpineGroupMultiSelectionDrag(spineGroupEntry),
            _ => false
        };
    }

    private bool TryContinueAssetMultiSelectionDrag(AssetListEntryViewModel assetEntry)
    {
        var anchorSelection = _lastMultiSelectionDragRowContext as AssetListEntryViewModel ?? assetEntry;
        var handled = _selectionCoordinator.ToggleAssetSelectionRangeInMultiSelectionMode(
            anchorSelection,
            assetEntry);

        if (handled)
        {
            _lastMultiSelectionDragRowContext = assetEntry;
        }

        return handled;
    }

    private bool TryContinueSpineGroupMultiSelectionDrag(SpineAssetGroupEntryViewModel spineGroupEntry)
    {
        var anchorSelection = _lastMultiSelectionDragRowContext as SpineAssetGroupEntryViewModel ?? spineGroupEntry;
        var handled = _selectionCoordinator.ToggleSpineGroupSelectionRangeInMultiSelectionMode(
            anchorSelection,
            spineGroupEntry);

        if (handled)
        {
            _lastMultiSelectionDragRowContext = spineGroupEntry;
        }

        return handled;
    }

    private static object? FindRowContextAtPointerPosition(ListView listView, Point pointerPosition)
    {
        return VisualTreeHelper
            .FindElementsInHostCoordinates(pointerPosition, listView)
            .OfType<FrameworkElement>()
            .Select(element => element.Tag)
            .FirstOrDefault(tag => tag is AssetListEntryViewModel or SpineAssetGroupEntryViewModel);
    }

    private void EndMultiSelectionDragGesture()
    {
        _isMultiSelectionDragGestureActive = false;
        _lastMultiSelectionDragRowContext = null;
    }

    private static void SetIconButtonText(Button button, string text)
    {
        ToolTipService.SetToolTip(button, text);
        AutomationProperties.SetName(button, text);
    }

    private void UpdateListSelectionMode()
    {
        var useAssetMultiSelection =
            ViewModel?.IsAssetViewMode == true &&
            ViewModel.IsMultiSelectionMode;
        var useSpineGroupMultiSelection =
            ViewModel?.IsSpineGroupViewMode == true &&
            ViewModel.IsMultiSelectionMode;

        AssetListView.SelectionMode = useAssetMultiSelection
            ? ListViewSelectionMode.Multiple
            : ListViewSelectionMode.Extended;
        SpineGroupListView.SelectionMode = useSpineGroupMultiSelection
            ? ListViewSelectionMode.Multiple
            : ListViewSelectionMode.Extended;
        _dragExportSourceCoordinator?.UpdateContainerEligibility();
    }
}
