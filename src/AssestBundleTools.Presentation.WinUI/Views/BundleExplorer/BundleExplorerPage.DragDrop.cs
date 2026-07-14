using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private const int MaximumLoggedDropItems = 12;
    private const string ElevatedWorkspaceImportTargetId = "workspace.bundle-pane.import";
    private const string ElevatedWorkspaceImportFallbackTargetId = "workspace.window.import-fallback";
    private int _workspaceImportDragSessionSequence;
    private int _workspaceImportDragOverEventCount;
    private string? _workspaceImportDragSessionId;
    private bool _workspaceImportFirstDragOverLogged;
    private IDisposable? _elevatedWorkspaceImportTargetRegistration;
    private IDisposable? _elevatedWorkspaceImportFallbackRegistration;

    private void RegisterWorkspaceImportDropHandlers()
    {
        BundlePaneHost.AddHandler(
            DragEnterEvent,
            new DragEventHandler(OnWorkspaceImportDragEnter),
            handledEventsToo: true);
        BundlePaneHost.AddHandler(
            DragOverEvent,
            new DragEventHandler(OnWorkspaceImportDragOver),
            handledEventsToo: true);
        BundlePaneHost.AddHandler(
            DragLeaveEvent,
            new DragEventHandler(OnWorkspaceImportDragLeave),
            handledEventsToo: true);
        BundlePaneHost.AddHandler(
            DropEvent,
            new DragEventHandler(OnWorkspaceImportDrop),
            handledEventsToo: true);
        AppendWorkspaceImportDropEnvironmentDiagnostic();
    }

    private void RegisterElevatedWorkspaceImportDropTargets()
    {
        if (_elevatedWorkspaceImportTargetRegistration is not null ||
            _elevatedWorkspaceImportFallbackRegistration is not null ||
            !TryGetHostWindow(out var hostWindow))
        {
            return;
        }

        var router = App.CurrentApp.ElevatedFileDropRouter;
        _elevatedWorkspaceImportTargetRegistration = router.RegisterTarget(
            new ElevatedFileDropTargetRegistration(
                ElevatedWorkspaceImportTargetId,
                hostWindow,
                BundlePaneHost,
                ElevatedFileDropTargetPriorities.Surface,
                ElevatedFileDropTargetHitTestMode.ElementBounds,
                CanHandle: () => IsLoaded,
                HandleDropAsync: HandleElevatedWorkspaceImportDropAsync));
        _elevatedWorkspaceImportFallbackRegistration = router.RegisterTarget(
            new ElevatedFileDropTargetRegistration(
                ElevatedWorkspaceImportFallbackTargetId,
                hostWindow,
                this,
                ElevatedFileDropTargetPriorities.WindowFallback,
                ElevatedFileDropTargetHitTestMode.WindowFallback,
                CanHandle: () => IsLoaded,
                HandleDropAsync: HandleElevatedWorkspaceImportDropAsync));
    }

    private void UnregisterElevatedWorkspaceImportDropTargets()
    {
        _elevatedWorkspaceImportTargetRegistration?.Dispose();
        _elevatedWorkspaceImportTargetRegistration = null;
        _elevatedWorkspaceImportFallbackRegistration?.Dispose();
        _elevatedWorkspaceImportFallbackRegistration = null;
    }

    private async Task HandleElevatedWorkspaceImportDropAsync(ElevatedFileDropContext context)
    {
        var nativePosition = context.NativePosition;
        var isExactBundlePaneTarget =
            context.HitTestMode == ElevatedFileDropTargetHitTestMode.ElementBounds;

        if (string.IsNullOrWhiteSpace(_workspaceImportDragSessionId))
        {
            BeginWorkspaceImportDragSession();
        }

        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(false);
        AppendDragDropInfo(
            ResourceKeys.WorkspaceImportLogElevatedDropReceivedFormat,
            ResolveWorkspaceImportDragSessionId(),
            context.Paths.Count,
            nativePosition.ClientX,
            nativePosition.ClientY,
            nativePosition.IsClientArea,
            isExactBundlePaneTarget,
            DescribeFileSystemPaths(context.Paths));

        try
        {
            if (!isExactBundlePaneTarget)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogElevatedDropWindowFallbackFormat,
                    ResolveWorkspaceImportDragSessionId());
            }

            if (!ViewModel.WorkspaceShell.HasWorkspace)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogDragDropRejectedNoWorkspaceFormat,
                    ResolveWorkspaceImportDragSessionId());
                return;
            }

            var droppedPaths = context.Paths
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (droppedPaths.Length == 0)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogElevatedDropNoPathsFormat,
                    ResolveWorkspaceImportDragSessionId());
                return;
            }

            await ViewModel.RequestDragDropImportAsync(droppedPaths);
            AppendDragDropInfo(
                ResourceKeys.WorkspaceImportLogDragDropDispatchResultFormat,
                ResolveWorkspaceImportDragSessionId(),
                droppedPaths.Length);
        }
        catch (Exception exception)
        {
            AppendDragDropError(
                ResourceKeys.WorkspaceImportLogDragDropUnhandledFailureFormat,
                ResolveWorkspaceImportDragSessionId(),
                exception.GetType().Name,
                $"0x{exception.HResult:X8}",
                exception.Message);
        }
        finally
        {
            EndWorkspaceImportDragSession();
        }
    }

    private void AppendWorkspaceImportDropEnvironmentDiagnostic()
    {
        AppendDragDropInfo(
            ResourceKeys.WorkspaceImportLogDragDropHandlerRegisteredFormat,
            BundlePaneHost.Name,
            BundlePaneHost.AllowDrop,
            Environment.OSVersion.VersionString,
            RuntimeInformation.ProcessArchitecture,
            ProcessElevationDiagnostics.Capture().DiagnosticValue,
            ViewModel.WorkspaceShell.HasWorkspace);
    }

    private void OnWorkspaceImportDragEnter(object sender, DragEventArgs e)
    {
        BeginWorkspaceImportDragSession();
        ApplyWorkspaceImportDragAcceptance(e);
        AppendDragDropEventDiagnostic("DragEnter", e);
    }

    private void OnWorkspaceImportDragOver(object sender, DragEventArgs e)
    {
        var startedWithoutDragEnter = string.IsNullOrWhiteSpace(_workspaceImportDragSessionId);
        if (startedWithoutDragEnter)
        {
            BeginWorkspaceImportDragSession();
        }

        _workspaceImportDragOverEventCount++;
        ApplyWorkspaceImportDragAcceptance(e);
        if (startedWithoutDragEnter || !_workspaceImportFirstDragOverLogged)
        {
            AppendDragDropEventDiagnostic(
                startedWithoutDragEnter ? "DragOverWithoutDragEnter" : "DragOver",
                e);
            _workspaceImportFirstDragOverLogged = true;
        }
    }

    private void OnWorkspaceImportDragLeave(object sender, DragEventArgs e)
    {
        e.Handled = true;
        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(false);
        AppendDragDropInfo(
            ResourceKeys.WorkspaceImportLogDragDropSessionEndedFormat,
            ResolveWorkspaceImportDragSessionId(),
            "DragLeave",
            _workspaceImportDragOverEventCount);
        EndWorkspaceImportDragSession();
    }

    private async void OnWorkspaceImportDrop(object sender, DragEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_workspaceImportDragSessionId))
        {
            BeginWorkspaceImportDragSession();
        }

        e.Handled = true;
        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(false);
        AppendDragDropEventDiagnostic("Drop", e);

        try
        {
            if (!ViewModel.WorkspaceShell.HasWorkspace)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogDragDropRejectedNoWorkspaceFormat,
                    ResolveWorkspaceImportDragSessionId());
                return;
            }

            if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogDragDropStorageItemsUnavailableFormat,
                    ResolveWorkspaceImportDragSessionId(),
                    DescribeAvailableFormats(e.DataView));
                return;
            }

            IReadOnlyList<IStorageItem> storageItems;
            try
            {
                storageItems = await e.DataView.GetStorageItemsAsync();
            }
            catch (Exception exception)
            {
                AppendDragDropError(
                    ResourceKeys.WorkspaceImportLogDragDropStorageItemsReadFailedFormat,
                    ResolveWorkspaceImportDragSessionId(),
                    exception.GetType().Name,
                    $"0x{exception.HResult:X8}",
                    exception.Message);
                return;
            }

            var droppedPaths = storageItems
                .Select(static item => item.Path)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            AppendDragDropInfo(
                ResourceKeys.WorkspaceImportLogDragDropStorageItemsReadFormat,
                ResolveWorkspaceImportDragSessionId(),
                storageItems.Count,
                droppedPaths.Length,
                DescribeStorageItems(storageItems));

            if (droppedPaths.Length == 0)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogDragDropNoPathsFormat,
                    ResolveWorkspaceImportDragSessionId());
                return;
            }

            await ViewModel.RequestDragDropImportAsync(droppedPaths);
            AppendDragDropInfo(
                ResourceKeys.WorkspaceImportLogDragDropDispatchResultFormat,
                ResolveWorkspaceImportDragSessionId(),
                droppedPaths.Length);
        }
        catch (Exception exception)
        {
            AppendDragDropError(
                ResourceKeys.WorkspaceImportLogDragDropUnhandledFailureFormat,
                ResolveWorkspaceImportDragSessionId(),
                exception.GetType().Name,
                $"0x{exception.HResult:X8}",
                exception.Message);
        }
        finally
        {
            EndWorkspaceImportDragSession();
        }
    }

    private void ApplyWorkspaceImportDragAcceptance(DragEventArgs e)
    {
        // Win10 可能在 DragEnter/DragOver 阶段尚未公开 StorageItems，放下时再校验载荷。
        var canAcceptImport = ViewModel.WorkspaceShell.HasWorkspace;
        e.AcceptedOperation = canAcceptImport
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
        e.Handled = true;
        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(canAcceptImport);
    }

    private void AppendDragDropEventDiagnostic(string stage, DragEventArgs e)
    {
        AppendDragDropInfo(
            ResourceKeys.WorkspaceImportLogDragDropEventFormat,
            ResolveWorkspaceImportDragSessionId(),
            stage,
            ViewModel.WorkspaceShell.HasWorkspace,
            e.OriginalSource?.GetType().Name ?? "-",
            DescribeAvailableFormats(e.DataView),
            e.AllowedOperations,
            e.AcceptedOperation,
            e.Handled);
    }

    private void BeginWorkspaceImportDragSession()
    {
        _workspaceImportDragSessionSequence++;
        _workspaceImportDragSessionId = _workspaceImportDragSessionSequence.ToString("D4");
        _workspaceImportDragOverEventCount = 0;
        _workspaceImportFirstDragOverLogged = false;
    }

    private void EndWorkspaceImportDragSession()
    {
        _workspaceImportDragSessionId = null;
        _workspaceImportDragOverEventCount = 0;
        _workspaceImportFirstDragOverLogged = false;
    }

    private string ResolveWorkspaceImportDragSessionId() =>
        string.IsNullOrWhiteSpace(_workspaceImportDragSessionId)
            ? "-"
            : _workspaceImportDragSessionId;

    private static string DescribeAvailableFormats(DataPackageView dataView)
    {
        try
        {
            return dataView.AvailableFormats.Count == 0
                ? "-"
                : string.Join(" | ", dataView.AvailableFormats);
        }
        catch (Exception exception)
        {
            return $"{exception.GetType().Name}:0x{exception.HResult:X8}";
        }
    }

    private static string DescribeStorageItems(IReadOnlyList<IStorageItem> storageItems)
    {
        var descriptions = storageItems
            .Take(MaximumLoggedDropItems)
            .Select(static item =>
            {
                var kind = item.IsOfType(StorageItemTypes.Folder) ? "Directory" : "File";
                var path = string.IsNullOrWhiteSpace(item.Path) ? item.Name : item.Path;
                return $"{kind}:{path}";
            })
            .ToList();
        if (storageItems.Count > MaximumLoggedDropItems)
        {
            descriptions.Add($"+{storageItems.Count - MaximumLoggedDropItems}");
        }

        return descriptions.Count == 0 ? "-" : string.Join(" | ", descriptions);
    }

    private static string DescribeFileSystemPaths(IReadOnlyList<string> paths)
    {
        var descriptions = paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Take(MaximumLoggedDropItems)
            .ToList();
        if (paths.Count > MaximumLoggedDropItems)
        {
            descriptions.Add($"+{paths.Count - MaximumLoggedDropItems}");
        }

        return descriptions.Count == 0 ? "-" : string.Join(" | ", descriptions);
    }

    private static void AppendDragDropInfo(string resourceKey, params object[] arguments) =>
        App.CurrentApp.AppLogService.AppendInfo(
            App.CurrentApp.LocalizationService.GetFormattedString(resourceKey, arguments));

    private static void AppendDragDropWarning(string resourceKey, params object[] arguments) =>
        App.CurrentApp.AppLogService.AppendWarning(
            App.CurrentApp.LocalizationService.GetFormattedString(resourceKey, arguments));

    private static void AppendDragDropError(string resourceKey, params object[] arguments) =>
        App.CurrentApp.AppLogService.AppendError(
            App.CurrentApp.LocalizationService.GetFormattedString(resourceKey, arguments));
}
