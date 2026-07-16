using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 把虚拟化列表容器适配为 Windows 文件拖出源，不参与资产提取决策。
/// </summary>
internal sealed class AssetPaneDragExportSourceCoordinator
{
    private readonly ListView _assetListView;
    private readonly ListView _spineGroupListView;
    private readonly Func<bool> _canStartDrag;
    private readonly Func<object, Task<AssetPaneDragExportPayload?>> _prepareAsync;
    private readonly Func<string, Task> _cleanupAsync;
    private readonly Dictionary<ListViewItem, object> _itemsByContainer = [];

    public AssetPaneDragExportSourceCoordinator(
        ListView assetListView,
        ListView spineGroupListView,
        Func<bool> canStartDrag,
        Func<object, Task<AssetPaneDragExportPayload?>> prepareAsync,
        Func<string, Task> cleanupAsync)
    {
        _assetListView = assetListView ?? throw new ArgumentNullException(nameof(assetListView));
        _spineGroupListView = spineGroupListView ?? throw new ArgumentNullException(nameof(spineGroupListView));
        _canStartDrag = canStartDrag ?? throw new ArgumentNullException(nameof(canStartDrag));
        _prepareAsync = prepareAsync ?? throw new ArgumentNullException(nameof(prepareAsync));
        _cleanupAsync = cleanupAsync ?? throw new ArgumentNullException(nameof(cleanupAsync));

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
            container.CanDrag = false;
            container.DragStarting -= OnDragStarting;
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
        container.DragStarting -= OnDragStarting;

        if (item is null || !CanExport(item))
        {
            container.CanDrag = false;
            _itemsByContainer.Remove(container);
            return;
        }

        _itemsByContainer[container] = item;
        container.CanDrag = _canStartDrag();
        container.DragStarting += OnDragStarting;
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
