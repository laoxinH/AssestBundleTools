using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;
using AssestBundleTools.Presentation.WinUI.Views.Home;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class BundleExplorerPage
{
    private sealed class BundleExplorerPageNavigationCoordinator
    {
        private readonly BundleExplorerPage _owner;

        public BundleExplorerPageNavigationCoordinator(BundleExplorerPage owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public async Task HandleLoadedAsync(WorkspaceNavigationRequest? navigationRequest)
        {
            if (navigationRequest is not null &&
                TryGetHostWindow(out var hostWindow) &&
                !hostWindow.PrepareWorkspaceLoad(navigationRequest))
            {
                // 目标工作区在当前页真正开始加载前，已经被其他进程或窗口命中；
                // 这里直接回退到当前已知会话，避免重复进入一轮新的加载与清空流程。
                await _owner.ViewModel.InitializeAsync();
                SynchronizeWindowWorkspaceRegistration();
                return;
            }

            await _owner.ViewModel.InitializeAsync(navigationRequest);
            SynchronizeWindowWorkspaceRegistration();
        }

        public void OpenWorkspace(WorkspaceNavigationRequest? request)
        {
            if (request is null)
            {
                return;
            }

            if (App.CurrentApp.TryActivateOpenedWorkspace(request))
            {
                return;
            }

            // 工作台页在“尚未绑定任何工作区”的空态下，也遵循与控制台一致的首次复用规则。
            // 避免第一份工作区因为偏好是“新建窗口”而额外派生新进程。
            if (request.OpenTarget == WorkspaceOpenTarget.NewWindow &&
                TryGetHostWindow(out var hostWindow) &&
                App.CurrentApp.ShouldReuseCurrentWindowForFirstWorkspace(hostWindow))
            {
                request = request with { OpenTarget = WorkspaceOpenTarget.CurrentWindow };
            }

            if (request.OpenTarget == WorkspaceOpenTarget.NewWindow)
            {
                App.CurrentApp.WorkspaceWindowService.OpenWorkspaceInNewWindow(request);
                return;
            }

            if (TryNavigateFromShell(ShellNavigationKeys.Workbench, request))
            {
                return;
            }

            _owner.Frame?.Navigate(typeof(BundleExplorerPage), request);
        }

        public void NavigateToConsole()
        {
            if (TryNavigateFromShell(ShellNavigationKeys.LaunchGuide))
            {
                return;
            }

            _owner.Frame?.Navigate(typeof(HomePage));
        }

        public void HandleWorkspaceShellPageStateChanged(
            BundleExplorerPageStateChangedEventArgs<BundleExplorerWorkspaceShellPageStateChange> e)
        {
            if (!e.ChangeFlags.HasFlag(BundleExplorerWorkspaceShellPageStateChange.WorkspaceAvailability))
            {
                return;
            }

            Enqueue(SynchronizeWindowWorkspaceRegistration);
        }

        private void SynchronizeWindowWorkspaceRegistration()
        {
            if (!TryGetHostWindow(out var hostWindow))
            {
                return;
            }

            if (_owner.ViewModel.TryGetCurrentWorkspaceNavigationRequest(out var navigationRequest))
            {
                hostWindow.RegisterWorkspace(navigationRequest);
                return;
            }

            hostWindow.ClearRegisteredWorkspace();
        }

        private bool TryNavigateFromShell(string navigationKey, object? parameter = null)
        {
            DependencyObject? current = _owner;

            while (current is not null)
            {
                if (current is ShellPage shellPage)
                {
                    shellPage.NavigateToSection(navigationKey, parameter);
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private bool TryGetHostWindow([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MainWindow? hostWindow)
        {
            hostWindow = null;
            DependencyObject? current = _owner;

            while (current is not null)
            {
                if (current is ShellPage shellPage && shellPage.HostWindow is not null)
                {
                    hostWindow = shellPage.HostWindow;
                    return true;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void Enqueue(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);

            if (_owner.DispatcherQueue.HasThreadAccess)
            {
                action();
                return;
            }

            _owner.DispatcherQueue.TryEnqueue(() => action());
        }
    }
}
