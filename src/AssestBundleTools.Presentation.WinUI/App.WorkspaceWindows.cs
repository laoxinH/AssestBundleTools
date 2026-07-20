using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;

namespace AssestBundleTools.Presentation.WinUI;

public partial class App
{
    public bool TryActivateOpenedWorkspace(WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);
        if (navigationRequest.ForceNewWorkspaceSession)
        {
            return false;
        }

        var workspaceKey = CreateWorkspaceKey(navigationRequest);
        if (!string.IsNullOrWhiteSpace(workspaceKey) &&
            FindWindowByOwnedWorkspaceKey(workspaceKey) is { } targetWindow)
        {
            return TryActivateWorkspaceWindow(targetWindow, navigationRequest);
        }

        return WorkspaceProcessCoordinatorService.TryActivateExistingWorkspace(navigationRequest);
    }

    public bool ShouldReuseCurrentWindowForFirstWorkspace(MainWindow? window) =>
        window is not null &&
        string.IsNullOrWhiteSpace(window.RegisteredWorkspaceKey) &&
        string.IsNullOrWhiteSpace(window.PendingWorkspaceKey);

    internal bool TryGetOwnedAuxiliaryWindow<TWindow>(
        MainWindow ownerWindow,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TWindow? auxiliaryWindow)
        where TWindow : Microsoft.UI.Xaml.Window
    {
        ArgumentNullException.ThrowIfNull(ownerWindow);

        auxiliaryWindow = _auxiliaryWindowOwners
            .Where(pair => ReferenceEquals(pair.Value, ownerWindow))
            .Select(static pair => pair.Key)
            .OfType<TWindow>()
            .LastOrDefault();
        return auxiliaryWindow is not null;
    }

    public bool DoesWindowHostWorkspace(
        MainWindow? window,
        WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(navigationRequest);
        if (window is null || string.IsNullOrWhiteSpace(window.RegisteredWorkspaceKey))
        {
            return false;
        }

        var workspaceKey = CreateWorkspaceKey(navigationRequest);
        return !string.IsNullOrWhiteSpace(workspaceKey) && window.OwnsWorkspace(workspaceKey);
    }

    public bool TryBeginWindowWorkspaceLoad(
        MainWindow window,
        WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(navigationRequest);
        var normalizedRequest = navigationRequest with { OpenTarget = WorkspaceOpenTarget.CurrentWindow };
        if (string.IsNullOrWhiteSpace(CreateWorkspaceKey(normalizedRequest)))
        {
            WorkspaceProcessCoordinatorService.ClearPendingWorkspaceRegistration();
            return false;
        }

        _activeWindow = window;
        return WorkspaceProcessCoordinatorService.TryBeginPendingWorkspaceRegistration(normalizedRequest);
    }

    public string RegisterWindowWorkspace(
        MainWindow window,
        WorkspaceNavigationRequest navigationRequest)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(navigationRequest);
        var normalizedRequest = navigationRequest with { OpenTarget = WorkspaceOpenTarget.CurrentWindow };
        var workspaceKey = CreateWorkspaceKey(normalizedRequest);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            WorkspaceProcessCoordinatorService.ClearWorkspaceRegistration();
            return string.Empty;
        }

        _activeWindow = window;
        WorkspaceProcessCoordinatorService.RegisterWorkspace(normalizedRequest);
        return workspaceKey;
    }

    public void UnregisterWindowWorkspace(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        WorkspaceProcessCoordinatorService.ClearWorkspaceRegistration();
    }

    private void RegisterWindow(MainWindow window)
    {
        _windows.Add(window);
        _activeWindow = window;
        RegisterOwnerWindow(window);
    }

    private void RegisterOwnerWindow(Microsoft.UI.Xaml.Window window)
    {
        if (!_activeOwnerWindows.Contains(window))
        {
            _activeOwnerWindows.Add(window);
            window.Activated += OnWindowActivated;
            window.Closed += OnWindowClosed;
        }

        PromoteOwnerWindow(window);
    }

    private void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated ||
            sender is not Microsoft.UI.Xaml.Window window)
        {
            return;
        }

        PromoteOwnerWindow(window);
        if (window is MainWindow mainWindow)
        {
            _activeWindow = mainWindow;
        }
    }

    private void OnWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (sender is not Microsoft.UI.Xaml.Window window)
        {
            return;
        }

        window.Activated -= OnWindowActivated;
        window.Closed -= OnWindowClosed;
        _activeOwnerWindows.Remove(window);
        if (ReferenceEquals(_activeOwnerWindow, window))
        {
            _activeOwnerWindow = _activeOwnerWindows.LastOrDefault();
        }

        if (window is not MainWindow mainWindow)
        {
            _auxiliaryWindowOwners.Remove(window);
            return;
        }

        CloseAuxiliaryWindowsOwnedBy(mainWindow);
        UnregisterWindowWorkspace(mainWindow);
        _windows.Remove(mainWindow);
        if (ReferenceEquals(_activeWindow, mainWindow))
        {
            _activeWindow = _windows.LastOrDefault();
        }
    }

    private void CloseAuxiliaryWindowsOwnedBy(MainWindow ownerWindow)
    {
        var ownedWindows = _auxiliaryWindowOwners
            .Where(pair => ReferenceEquals(pair.Value, ownerWindow))
            .Select(static pair => pair.Key)
            .ToArray();
        foreach (var ownedWindow in ownedWindows)
        {
            try
            {
                ownedWindow.Close();
            }
            catch (Exception exception)
            {
                StartupDiagnostics.Log($"Closing owned auxiliary window failed: {exception}");
            }
        }
    }

    private Microsoft.UI.Xaml.Window? ResolveActiveOwnerWindow() =>
        _activeOwnerWindow ?? _activeOwnerWindows.LastOrDefault() ?? _activeWindow;

    private void PromoteOwnerWindow(Microsoft.UI.Xaml.Window window)
    {
        _activeOwnerWindows.Remove(window);
        _activeOwnerWindows.Add(window);
        _activeOwnerWindow = window;
    }

    private static string CreateWorkspaceKey(WorkspaceNavigationRequest navigationRequest) =>
        WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            navigationRequest.SourcePath,
            navigationRequest.WorkspaceDataRootPath);

    private ShellLaunchRequest? ResolveInitialLaunchRequest()
    {
        if (ShellLaunchArgumentCodec.TryParseCurrentProcessLaunchRequest(out var launchRequest))
        {
            return launchRequest;
        }

        if (AppPreferencesService.RestoreLastWorkspaceOnStartup &&
            WorkspaceContextService.TryGetLastActiveWorkspaceNavigationRequest(out var workspaceRequest))
        {
            return new ShellLaunchRequest(ShellNavigationKeys.Workbench, workspaceRequest);
        }

        return null;
    }

    private void OnWorkspaceProcessActivationRequested(
        object? sender,
        WorkspaceProcessActivationRequestedEventArgs args)
    {
        var workspaceKey = CreateWorkspaceKey(args.NavigationRequest);
        var targetWindow = !string.IsNullOrWhiteSpace(workspaceKey)
            ? FindWindowByOwnedWorkspaceKey(workspaceKey)
            : null;
        targetWindow ??= _activeWindow ?? _windows.LastOrDefault();
        if (targetWindow is null)
        {
            return;
        }

        void ActivateWindow() => ActivateWorkspaceInCurrentProcess(targetWindow, args.NavigationRequest);
        if (targetWindow.DispatcherQueue.HasThreadAccess)
        {
            ActivateWindow();
        }
        else if (!targetWindow.DispatcherQueue.TryEnqueue(ActivateWindow))
        {
            StartupDiagnostics.Log("Failed to enqueue cross-process workspace activation to the UI thread.");
        }
    }

    private void ActivateWorkspaceInCurrentProcess(
        MainWindow window,
        WorkspaceNavigationRequest navigationRequest)
    {
        _activeWindow = window;
        window.ActivateFromExternalWorkspaceRequest(navigationRequest);
    }

    private MainWindow? FindWindowByOwnedWorkspaceKey(string workspaceKey)
    {
        if (_activeWindow?.OwnsWorkspace(workspaceKey) == true)
        {
            return _activeWindow;
        }

        for (var index = _windows.Count - 1; index >= 0; index--)
        {
            if (_windows[index].OwnsWorkspace(workspaceKey))
            {
                return _windows[index];
            }
        }

        return null;
    }

    private bool TryActivateWorkspaceWindow(
        MainWindow window,
        WorkspaceNavigationRequest navigationRequest)
    {
        void ActivateWindow() => ActivateWorkspaceInCurrentProcess(window, navigationRequest);
        if (window.DispatcherQueue.HasThreadAccess)
        {
            ActivateWindow();
            return true;
        }

        var enqueued = window.DispatcherQueue.TryEnqueue(ActivateWindow);
        if (!enqueued)
        {
            StartupDiagnostics.Log("Failed to enqueue same-process workspace activation to the UI thread.");
        }

        return enqueued;
    }
}
