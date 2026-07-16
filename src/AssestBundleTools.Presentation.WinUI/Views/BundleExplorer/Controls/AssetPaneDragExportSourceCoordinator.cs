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
    private string? _activeStagingSessionPath;

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
            container.DropCompleted -= OnDropCompleted;
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
        container.DropCompleted -= OnDropCompleted;

        if (item is null || !CanExport(item))
        {
            container.CanDrag = false;
            _itemsByContainer.Remove(container);
            return;
        }

        _itemsByContainer[container] = item;
        container.CanDrag = _canStartDrag();
        container.DragStarting += OnDragStarting;
        container.DropCompleted += OnDropCompleted;
    }

    private async void OnDragStarting(UIElement sender, DragStartingEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            if (!_canStartDrag() ||
                sender is not ListViewItem container ||
                !_itemsByContainer.TryGetValue(container, out var item))
            {
                args.Cancel = true;
                return;
            }

            // Windows 资源管理器可能在 DropCompleted 后延迟消费文件，旧会话交给过期清理。
            _activeStagingSessionPath = null;
            var payload = await _prepareAsync(item);
            if (payload is null || payload.StorageItems.Count == 0)
            {
                args.Cancel = true;
                return;
            }

            _activeStagingSessionPath = payload.StagingSessionPath;
            args.Data.RequestedOperation = DataPackageOperation.Copy;
            args.Data.SetStorageItems(payload.StorageItems);
        }
        catch
        {
            args.Cancel = true;
            await CleanupActiveSessionAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        // 这里只释放源端引用，不能删除资源管理器仍可能延迟读取或重试的暂存项。
        _activeStagingSessionPath = null;
    }

    private async Task CleanupActiveSessionAsync()
    {
        var stagingSessionPath = _activeStagingSessionPath;
        _activeStagingSessionPath = null;
        if (!string.IsNullOrWhiteSpace(stagingSessionPath))
        {
            await _cleanupAsync(stagingSessionPath);
        }
    }

    private static bool CanExport(object item) => item switch
    {
        AssetListEntryViewModel asset => asset.CanDragExport,
        SpineAssetGroupEntryViewModel spineGroup => spineGroup.CanDragExport,
        _ => false
    };
}
