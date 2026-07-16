using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    private readonly Action<object> _elevatedDragAttempted;
    private readonly Dictionary<ListViewItem, object> _itemsByContainer = [];
    private ListViewItem? _elevatedGestureContainer;
    private uint _elevatedGesturePointerId;
    private Point _elevatedGestureStartPosition;

    public AssetPaneDragExportSourceCoordinator(
        ListView assetListView,
        ListView spineGroupListView,
        Func<bool> canStartDrag,
        Func<object, Task<AssetPaneDragExportPayload?>> prepareAsync,
        Func<string, Task> cleanupAsync,
        bool isElevated,
        Action<object> elevatedDragAttempted)
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
            if (_canStartDrag())
            {
                AttachElevatedGestureHandlers(container);
            }

            return;
        }

        container.CanDrag = _canStartDrag();
        container.DragStarting += OnDragStarting;
    }

    private void DetachContainer(ListViewItem container)
    {
        container.CanDrag = false;
        container.DragStarting -= OnDragStarting;
        container.PointerPressed -= OnElevatedPointerPressed;
        container.PointerMoved -= OnElevatedPointerMoved;
        container.PointerExited -= OnElevatedPointerExited;
        container.PointerReleased -= OnElevatedPointerReleased;
        container.PointerCanceled -= OnElevatedPointerCanceled;
        container.PointerCaptureLost -= OnElevatedPointerCanceled;

        if (ReferenceEquals(_elevatedGestureContainer, container))
        {
            ResetElevatedGesture();
        }
    }

    private void AttachElevatedGestureHandlers(ListViewItem container)
    {
        container.PointerPressed += OnElevatedPointerPressed;
        container.PointerMoved += OnElevatedPointerMoved;
        container.PointerExited += OnElevatedPointerExited;
        container.PointerReleased += OnElevatedPointerReleased;
        container.PointerCanceled += OnElevatedPointerCanceled;
        container.PointerCaptureLost += OnElevatedPointerCanceled;
    }

    private void OnElevatedPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (!_canStartDrag() ||
            sender is not ListViewItem container ||
            !_itemsByContainer.ContainsKey(container))
        {
            ResetElevatedGesture();
            return;
        }

        var point = args.GetCurrentPoint(container);
        if (!point.Properties.IsLeftButtonPressed)
        {
            ResetElevatedGesture();
            return;
        }

        _elevatedGestureContainer = container;
        _elevatedGesturePointerId = args.Pointer.PointerId;
        _elevatedGestureStartPosition = point.Position;
    }

    private void OnElevatedPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!IsCurrentElevatedGesture(sender, args, out var container))
        {
            return;
        }

        var point = args.GetCurrentPoint(container);
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

    private void OnElevatedPointerExited(object sender, PointerRoutedEventArgs args)
    {
        if (!IsCurrentElevatedGesture(sender, args, out var container))
        {
            return;
        }

        if (args.GetCurrentPoint(container).Properties.IsLeftButtonPressed)
        {
            ReportElevatedDragAttempt();
        }
        else
        {
            ResetElevatedGesture();
        }
    }

    private void OnElevatedPointerReleased(object sender, PointerRoutedEventArgs args) =>
        ResetElevatedGesture();

    private void OnElevatedPointerCanceled(object sender, PointerRoutedEventArgs args) =>
        ResetElevatedGesture();

    private bool IsCurrentElevatedGesture(
        object sender,
        PointerRoutedEventArgs args,
        out ListViewItem container)
    {
        container = sender as ListViewItem ?? null!;
        return container is not null &&
               ReferenceEquals(container, _elevatedGestureContainer) &&
               args.Pointer.PointerId == _elevatedGesturePointerId;
    }

    private void ReportElevatedDragAttempt()
    {
        if (_elevatedGestureContainer is not { } container ||
            !_itemsByContainer.TryGetValue(container, out var item))
        {
            ResetElevatedGesture();
            return;
        }

        ResetElevatedGesture();
        _elevatedDragAttempted(item);
    }

    private void ResetElevatedGesture()
    {
        _elevatedGestureContainer = null;
        _elevatedGesturePointerId = 0;
        _elevatedGestureStartPosition = default;
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
