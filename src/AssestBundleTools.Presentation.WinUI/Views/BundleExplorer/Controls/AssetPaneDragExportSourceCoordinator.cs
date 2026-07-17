using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 把虚拟化列表容器适配为 Windows 文件拖出源，不参与资产提取决策。
/// </summary>
internal sealed class AssetPaneDragExportSourceCoordinator
{
    private const double ElevatedDragThreshold = 8d;
    private readonly ListView _assetListView;
    private readonly ListView _spineGroupListView;
    private readonly Func<bool> _canStartDrag;
    private readonly Func<object, Task<AssetPaneDragExportPayload?>> _prepareAsync;
    private readonly Func<string, Task> _cleanupAsync;
    private readonly bool _isElevated;
    private readonly Func<object, Task> _elevatedDragAttempted;
    private readonly Dictionary<ListViewItem, object> _itemsByContainer = [];
    private ListView? _elevatedGestureListView;
    private ListViewItem? _elevatedGestureContainer;
    private object? _elevatedGestureItem;
    private uint _elevatedGesturePointerId;
    private Point _elevatedGestureStartPosition;
    private bool _isElevatedDragActive;

    public AssetPaneDragExportSourceCoordinator(
        ListView assetListView,
        ListView spineGroupListView,
        Func<bool> canStartDrag,
        Func<object, Task<AssetPaneDragExportPayload?>> prepareAsync,
        Func<string, Task> cleanupAsync,
        bool isElevated,
        Func<object, Task> elevatedDragAttempted)
    {
        _assetListView = assetListView ?? throw new ArgumentNullException(nameof(assetListView));
        _spineGroupListView = spineGroupListView ?? throw new ArgumentNullException(nameof(spineGroupListView));
        _canStartDrag = canStartDrag ?? throw new ArgumentNullException(nameof(canStartDrag));
        _prepareAsync = prepareAsync ?? throw new ArgumentNullException(nameof(prepareAsync));
        _cleanupAsync = cleanupAsync ?? throw new ArgumentNullException(nameof(cleanupAsync));
        _isElevated = isElevated;
        _elevatedDragAttempted = elevatedDragAttempted ?? throw new ArgumentNullException(nameof(elevatedDragAttempted));

        _assetListView.ContainerContentChanging += OnContainerContentChanging;
        _spineGroupListView.ContainerContentChanging += OnContainerContentChanging;
        if (_isElevated)
        {
            AttachElevatedGestureHandlers(_assetListView);
            AttachElevatedGestureHandlers(_spineGroupListView);
        }
    }

    public void UpdateContainerEligibility()
    {
        UpdateContainerEligibility(_assetListView);
        UpdateContainerEligibility(_spineGroupListView);
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer is not ListViewItem container)
        {
            return;
        }

        if (args.InRecycleQueue)
        {
            DetachContainer(container);
            _itemsByContainer.Remove(container);
            return;
        }

        ConfigureContainer(container, args.Item);
    }

    private void UpdateContainerEligibility(ListView listView)
    {
        foreach (var item in listView.Items)
        {
            if (listView.ContainerFromItem(item) is ListViewItem container)
            {
                ConfigureContainer(container, item);
            }
        }
    }

    private void ConfigureContainer(ListViewItem container, object? item)
    {
        DetachContainer(container);

        if (item is null || !CanExport(item))
        {
            container.CanDrag = false;
            _itemsByContainer.Remove(container);
            return;
        }

        _itemsByContainer[container] = item;
        if (_isElevated)
        {
            container.CanDrag = false;
            return;
        }

        container.CanDrag = _canStartDrag();
        container.DragStarting += OnDragStarting;
    }

    private void DetachContainer(ListViewItem container)
    {
        container.CanDrag = false;
        container.DragStarting -= OnDragStarting;
    }

    private void AttachElevatedGestureHandlers(ListView listView)
    {
        listView.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnElevatedPointerPressed),
            handledEventsToo: true);
        listView.AddHandler(
            UIElement.PointerMovedEvent,
            new PointerEventHandler(OnElevatedPointerMoved),
            handledEventsToo: true);
        listView.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnElevatedPointerReleased),
            handledEventsToo: true);
        listView.AddHandler(
            UIElement.PointerCanceledEvent,
            new PointerEventHandler(OnElevatedPointerCanceled),
            handledEventsToo: true);
        listView.AddHandler(
            UIElement.PointerCaptureLostEvent,
            new PointerEventHandler(OnElevatedPointerCanceled),
            handledEventsToo: true);
    }

    private void OnElevatedPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (_isElevatedDragActive ||
            !_canStartDrag() ||
            sender is not ListView listView ||
            !TryResolveGestureTarget(listView, args.OriginalSource, out var container, out var item) ||
            !CanExport(item))
        {
            ResetElevatedGesture();
            return;
        }

        var point = args.GetCurrentPoint(listView);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ResetElevatedGesture();
            return;
        }

        _elevatedGestureListView = listView;
        _elevatedGestureContainer = container;
        _elevatedGestureItem = item;
        _elevatedGesturePointerId = args.Pointer.PointerId;
        _elevatedGestureStartPosition = point.Position;
    }

    private void OnElevatedPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!IsCurrentElevatedGesture(sender, args, out var listView))
        {
            return;
        }

        var point = args.GetCurrentPoint(listView);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ResetElevatedGesture();
            return;
        }

        var deltaX = point.Position.X - _elevatedGestureStartPosition.X;
        var deltaY = point.Position.Y - _elevatedGestureStartPosition.Y;
        if ((deltaX * deltaX) + (deltaY * deltaY) >= ElevatedDragThreshold * ElevatedDragThreshold)
        {
            ReportElevatedDragAttempt();
        }
    }

    private void OnElevatedPointerReleased(object sender, PointerRoutedEventArgs args) =>
        ResetElevatedGesture();

    private void OnElevatedPointerCanceled(object sender, PointerRoutedEventArgs args) =>
        ResetElevatedGesture();

    private bool IsCurrentElevatedGesture(
        object sender,
        PointerRoutedEventArgs args,
        out ListView listView)
    {
        listView = sender as ListView ?? null!;
        return listView is not null &&
               ReferenceEquals(listView, _elevatedGestureListView) &&
               args.Pointer.PointerId == _elevatedGesturePointerId;
    }

    private async void ReportElevatedDragAttempt()
    {
        if (_elevatedGestureListView is null ||
            _elevatedGestureContainer is not { } container ||
            _elevatedGestureItem is not { } item)
        {
            ResetElevatedGesture();
            return;
        }

        ResetElevatedGesture(releasePointerCaptures: false);
        _isElevatedDragActive = true;
        var originalOpacity = container.Opacity;
        container.Opacity = 0.62;

        try
        {
            await _elevatedDragAttempted(item);
        }
        finally
        {
            container.Opacity = originalOpacity;
            _isElevatedDragActive = false;
        }
    }

    private void ResetElevatedGesture(bool releasePointerCaptures = true)
    {
        var listView = _elevatedGestureListView;
        var container = _elevatedGestureContainer;
        _elevatedGestureListView = null;
        _elevatedGestureContainer = null;
        _elevatedGestureItem = null;
        _elevatedGesturePointerId = 0;
        _elevatedGestureStartPosition = default;
        if (releasePointerCaptures)
        {
            container?.ReleasePointerCaptures();
            listView?.ReleasePointerCaptures();
        }
    }

    private static bool TryResolveGestureTarget(
        ListView listView,
        object? originalSource,
        out ListViewItem container,
        out object item)
    {
        var current = originalSource as DependencyObject;
        while (current is not null && !ReferenceEquals(current, listView))
        {
            if (current is ListViewItem listViewItem &&
                listView.ItemFromContainer(listViewItem) is { } resolvedItem)
            {
                container = listViewItem;
                item = resolvedItem;
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        container = null!;
        item = null!;
        return false;
    }

    private void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        if (!_canStartDrag() ||
            sender is not ListViewItem container ||
            !_itemsByContainer.TryGetValue(container, out var item))
        {
            args.Cancel = true;
            return;
        }

        var payloadTask = new Lazy<Task<AssetPaneDragExportPayload?>>(
            () => _prepareAsync(item),
            LazyThreadSafetyMode.ExecutionAndPublication);
        args.Data.RequestedOperation = DataPackageOperation.Copy;
        args.Data.SetDataProvider(
            StandardDataFormats.StorageItems,
            request => ProvideStorageItemsAsync(payloadTask, request));
    }

    private async void ProvideStorageItemsAsync(
        Lazy<Task<AssetPaneDragExportPayload?>> payloadTask,
        DataProviderRequest request)
    {
        var deferral = request.GetDeferral();
        AssetPaneDragExportPayload? payload = null;
        try
        {
            payload = await payloadTask.Value;
            if (payload is { StorageItems.Count: > 0 })
            {
                request.SetData(payload.StorageItems);
            }
        }
        catch
        {
            if (payload is not null)
            {
                await _cleanupAsync(payload.StagingSessionPath);
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static bool CanExport(object item) => item switch
    {
        AssetListEntryViewModel asset => asset.CanDragExport,
        SpineAssetGroupEntryViewModel spineGroup => spineGroup.CanDragExport,
        _ => false
    };
}
