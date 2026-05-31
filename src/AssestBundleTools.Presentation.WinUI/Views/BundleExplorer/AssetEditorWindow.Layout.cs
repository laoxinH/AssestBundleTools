using System.Runtime.InteropServices;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class AssetEditorWindow
{
    private const int SwShow = 5;
    private const int SwRestore = 9;
    private AppWindow? _appWindow;
    private AssetEditorWindowLayoutSnapshot? _lastPublishedLayout;
    private bool _isApplyingInitialLayout;

    internal event Action<AssetEditorWindow, AssetEditorWindowLayoutSnapshot>? LayoutSnapshotChanged;

    internal bool TryCaptureLayoutSnapshot(out AssetEditorWindowLayoutSnapshot layout) =>
        TryCaptureCurrentLayout(out layout);

    internal void BringToFront()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            if (windowHandle != IntPtr.Zero)
            {
                ShowWindow(windowHandle, IsIconic(windowHandle) ? SwRestore : SwShow);
                SetForegroundWindow(windowHandle);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset editor window foreground activation failed: {ex}");
        }

        Activate();
    }

    internal void ApplyInitialLayout(AssetEditorWindowLayoutSnapshot layout)
    {
        if (_appWindow is null)
        {
            return;
        }

        try
        {
            _isApplyingInitialLayout = true;
            var safeLayout = ResolveInitialLayout(layout);
            _appWindow.MoveAndResize(new RectInt32(
                safeLayout.X,
                safeLayout.Y,
                safeLayout.Width,
                safeLayout.Height));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset editor window layout restore failed: {ex}");
        }
        finally
        {
            _isApplyingInitialLayout = false;
        }
    }

    private static AssetEditorWindowLayoutSnapshot ResolveInitialLayout(
        AssetEditorWindowLayoutSnapshot layout)
    {
        try
        {
            var displayArea = DisplayArea.GetFromRect(
                new RectInt32(layout.X, layout.Y, layout.Width, layout.Height),
                DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            return AssetEditorWindowLayoutSafety.ClampToWorkArea(
                layout,
                new AssetEditorWindowWorkAreaSnapshot(
                    workArea.X,
                    workArea.Y,
                    workArea.Width,
                    workArea.Height));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset editor window layout safety check failed: {ex}");
            return layout;
        }
    }

    private void TryInitializeWindowLayoutTracking()
    {
        try
        {
            var windowHandle = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow is not null)
            {
                _appWindow.Closing += OnAppWindowClosing;
                _appWindow.Changed += OnAppWindowChanged;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Asset editor window layout tracking initialization failed: {ex}");
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_isApplyingInitialLayout ||
            (!args.DidPositionChange && !args.DidSizeChange))
        {
            return;
        }

        PublishCurrentLayout();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowDirectClose || !ShouldHandleUserCloseRequest())
        {
            return;
        }

        args.Cancel = true;
        if (_isProcessingCloseRequest)
        {
            return;
        }

        _ = HandleUserCloseRequestAsync();
    }

    private bool ShouldHandleUserCloseRequest() =>
        ViewModel.Inspector.Preview.Image.IsSpriteEditModeActive ||
        ViewModel.DocumentGroup.ActiveSpineGroupTab?.IsSpineEditModeActive == true ||
        (ShouldReturnDocumentsToMainWindowOnClose() && ViewModel.DocumentGroup.HasDocumentTabs);

    private bool ShouldReturnDocumentsToMainWindowOnClose() =>
        App.CurrentApp.AppPreferencesService.AssetEditorCloseBehavior ==
        AssetEditorCloseBehavior.ReturnToMainWindow;

    private async Task HandleUserCloseRequestAsync()
    {
        if (_isProcessingCloseRequest)
        {
            return;
        }

        _isProcessingCloseRequest = true;
        try
        {
            if (!await TryExitSpriteEditModeAsync())
            {
                return;
            }

            if (!await TryExitSpineEditModeAsync())
            {
                return;
            }

            if (!ViewModel.DocumentGroup.HasDocumentTabs ||
                !ShouldReturnDocumentsToMainWindowOnClose())
            {
                CloseWithoutReturningToMainWindow();
                return;
            }

            var movedToMainWindow = await ViewModel.TryMoveAssetDocumentGroupToMainWindowAsync();
            if (movedToMainWindow || !ViewModel.DocumentGroup.HasDocumentTabs)
            {
                CloseWithoutReturningToMainWindow();
                return;
            }
        }
        finally
        {
            if (!_allowDirectClose)
            {
                _isProcessingCloseRequest = false;
            }
        }
    }

    private void PublishCurrentLayout()
    {
        if (!TryCaptureCurrentLayout(out var layout) ||
            (_lastPublishedLayout is { } lastPublishedLayout && layout.Equals(lastPublishedLayout)))
        {
            return;
        }

        _lastPublishedLayout = layout;
        LayoutSnapshotChanged?.Invoke(this, layout);
    }

    private bool TryCaptureCurrentLayout(out AssetEditorWindowLayoutSnapshot layout)
    {
        if (_appWindow is null)
        {
            layout = default;
            return false;
        }

        var position = _appWindow.Position;
        var size = _appWindow.Size;
        layout = new AssetEditorWindowLayoutSnapshot(
            position.X,
            position.Y,
            size.Width,
            size.Height);
        return true;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);
}
