using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 把普通 WinUI 拖入和管理员 WM_DROPFILES 统一映射为精确的资产行目标。
/// </summary>
internal sealed class AssetPaneDragReplacementTargetCoordinator
{
    private readonly ListView _assetListView;
    private readonly ListView _spineGroupListView;
    private readonly Func<bool> _canAcceptDrop;
    private readonly Func<object, IReadOnlyList<string>, Task> _requestReplacementAsync;

    public AssetPaneDragReplacementTargetCoordinator(
        ListView assetListView,
        ListView spineGroupListView,
        Func<bool> canAcceptDrop,
        Func<object, IReadOnlyList<string>, Task> requestReplacementAsync)
    {
        _assetListView = assetListView ?? throw new ArgumentNullException(nameof(assetListView));
        _spineGroupListView = spineGroupListView ?? throw new ArgumentNullException(nameof(spineGroupListView));
        _canAcceptDrop = canAcceptDrop ?? throw new ArgumentNullException(nameof(canAcceptDrop));
        _requestReplacementAsync = requestReplacementAsync ?? throw new ArgumentNullException(nameof(requestReplacementAsync));

        Attach(_assetListView);
        Attach(_spineGroupListView);
    }

    public bool IsExactTarget(DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return TryResolveVisibleTarget(args, out _, out _);
    }

    public IReadOnlyList<IDisposable> RegisterElevatedTargets(
        Window sourceWindow,
        IElevatedFileDropRouter router)
    {
        ArgumentNullException.ThrowIfNull(sourceWindow);
        ArgumentNullException.ThrowIfNull(router);

        return
        [
            RegisterElevatedTarget(
                "workspace.asset-pane.asset-replacement",
                sourceWindow,
                router,
                _assetListView),
            RegisterElevatedTarget(
                "workspace.asset-pane.spine-replacement",
                sourceWindow,
                router,
                _spineGroupListView)
        ];
    }

    private void Attach(ListView listView)
    {
        listView.AllowDrop = true;
        listView.AddHandler(
            UIElement.DragEnterEvent,
            new DragEventHandler(OnDragOver),
            handledEventsToo: true);
        listView.AddHandler(
            UIElement.DragOverEvent,
            new DragEventHandler(OnDragOver),
            handledEventsToo: true);
        listView.AddHandler(
            UIElement.DropEvent,
            new DragEventHandler(OnDrop),
            handledEventsToo: true);
    }

    private void OnDragOver(object sender, DragEventArgs args)
    {
        if (sender is not ListView listView ||
            !TryResolveTarget(listView, args.GetPosition(listView), out _))
        {
            return;
        }

        // Win10 可能在 DragEnter/DragOver 阶段尚未公开 StorageItems，放下时再读取载荷。
        args.AcceptedOperation = _canAcceptDrop()
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        args.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs args)
    {
        if (sender is not ListView listView ||
            !TryResolveTarget(listView, args.GetPosition(listView), out var item))
        {
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Copy;
        args.Handled = true;
        if (!_canAcceptDrop() ||
            !args.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        try
        {
            var storageItems = await args.DataView.GetStorageItemsAsync();
            var paths = storageItems
                .Select(static storageItem => storageItem.Path)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (paths.Length > 0)
            {
                await _requestReplacementAsync(item, paths);
            }
        }
        catch (Exception exception)
        {
            App.CurrentApp.AppLogService.AppendError(
                App.CurrentApp.LocalizationService.GetFormattedString(
                    Localization.ResourceKeys.AssetPaneDragReplacementReadFailedLogFormat,
                    exception.GetType().Name,
                    $"0x{exception.HResult:X8}"));
        }
    }

    private IDisposable RegisterElevatedTarget(
        string targetId,
        Window sourceWindow,
        IElevatedFileDropRouter router,
        ListView listView) =>
        router.RegisterTarget(
            new ElevatedFileDropTargetRegistration(
                targetId,
                sourceWindow,
                listView,
                ElevatedFileDropTargetPriorities.Item,
                CanHandle: () => IsVisible(listView),
                HandleDropAsync: context => HandleElevatedDropAsync(listView, context),
                HitTest: position => TryResolveTarget(listView, position, out _)));

    private Task HandleElevatedDropAsync(
        ListView listView,
        ElevatedFileDropContext context)
    {
        return _canAcceptDrop() &&
               TryResolveTarget(listView, context.Coordinates.LocalPosition, out var item)
            ? _requestReplacementAsync(item, context.Paths)
            : Task.CompletedTask;
    }

    private bool TryResolveVisibleTarget(
        DragEventArgs args,
        out ListView listView,
        out object item)
    {
        foreach (var candidate in EnumerateVisibleLists())
        {
            if (TryResolveTarget(candidate, args.GetPosition(candidate), out item))
            {
                listView = candidate;
                return true;
            }
        }

        listView = null!;
        item = null!;
        return false;
    }

    private bool TryResolveTarget(ListView listView, Point position, out object item)
    {
        item = null!;
        if (!IsVisible(listView))
        {
            return false;
        }

        item = VisualTreeHelper
            .FindElementsInHostCoordinates(position, listView)
            .OfType<FrameworkElement>()
            .Select(static element => element.Tag)
            .FirstOrDefault(static tag => tag is AssetListEntryViewModel or SpineAssetGroupEntryViewModel)!;
        return item is not null;
    }

    private IEnumerable<ListView> EnumerateVisibleLists()
    {
        if (IsVisible(_assetListView))
        {
            yield return _assetListView;
        }

        if (IsVisible(_spineGroupListView))
        {
            yield return _spineGroupListView;
        }
    }

    private static bool IsVisible(ListView listView) =>
        listView.IsLoaded && listView.Visibility == Visibility.Visible;
}
