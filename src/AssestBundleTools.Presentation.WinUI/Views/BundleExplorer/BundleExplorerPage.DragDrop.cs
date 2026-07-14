using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private const int MaximumLoggedDropItems = 12;
    private int _workspaceImportDragSessionSequence;
    private int _workspaceImportDragOverEventCount;
    private string? _workspaceImportDragSessionId;
    private bool _workspaceImportFirstDragOverLogged;

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
        App.CurrentApp.ElevatedDragDropCompatibilityService.FilesDropped +=
            OnElevatedCompatibilityFilesDropped;

        AppendWorkspaceImportDropEnvironmentDiagnostic();
    }

    private async void OnElevatedCompatibilityFilesDropped(
        object? sender,
        ElevatedFileDropReceivedEventArgs e)
    {
        if (!IsLoaded ||
            !TryGetHostWindow(out var hostWindow) ||
            !ReferenceEquals(hostWindow, e.SourceWindow))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_workspaceImportDragSessionId))
        {
            BeginWorkspaceImportDragSession();
        }

        _chromeCoordinator.SetWorkspaceImportDropOverlayVisible(false);
        var isWithinBundlePane = IsElevatedCompatibilityDropWithinBundlePane(e);
        AppendDragDropInfo(
            ResourceKeys.WorkspaceImportLogElevatedDropReceivedFormat,
            ResolveWorkspaceImportDragSessionId(),
            e.Paths.Count,
            e.ClientX,
            e.ClientY,
            e.IsClientArea,
            isWithinBundlePane,
            DescribeFileSystemPaths(e.Paths));

        try
        {
            if (!isWithinBundlePane)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogElevatedDropOutsideTargetFormat,
                    ResolveWorkspaceImportDragSessionId());
                return;
            }

            if (!ViewModel.WorkspaceShell.HasWorkspace)
            {
                AppendDragDropWarning(
                    ResourceKeys.WorkspaceImportLogDragDropRejectedNoWorkspaceFormat,
                    ResolveWorkspaceImportDragSessionId());
                return;
            }

            var droppedPaths = e.Paths
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

    private bool IsElevatedCompatibilityDropWithinBundlePane(
        ElevatedFileDropReceivedEventArgs e)
    {
        if (!e.IsClientArea ||
            BundlePaneHost.Visibility != Visibility.Visible ||
            XamlRoot is null ||
            BundlePaneHost.ActualWidth <= 0d ||
            BundlePaneHost.ActualHeight <= 0d)
        {
            return false;
        }

        var rasterizationScale = Math.Max(0.01d, XamlRoot.RasterizationScale);
        var dropPoint = new Point(
            e.ClientX / rasterizationScale,
            e.ClientY / rasterizationScale);
        var bundlePaneOrigin = BundlePaneHost
            .TransformToVisual(null)
            .TransformPoint(new Point());
        return dropPoint.X >= bundlePaneOrigin.X &&
               dropPoint.X <= bundlePaneOrigin.X + BundlePaneHost.ActualWidth &&
               dropPoint.Y >= bundlePaneOrigin.Y &&
               dropPoint.Y <= bundlePaneOrigin.Y + BundlePaneHost.ActualHeight;
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
