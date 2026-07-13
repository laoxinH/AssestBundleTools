using System.Reflection;
using System.Runtime.InteropServices;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed partial class WindowsUnelevatedProcessRestartService
{
    private const int ShellWindowClassDesktop = 8;
    private const int ShellWindowFindNeedDispatch = 1;

    private static ExplorerShellLaunchResult TryRequestExplorerShellLaunch(
        string executablePath,
        IReadOnlyList<string> arguments)
    {
        object? shell = null;
        object? shellWindows = null;
        object? desktopWindow = null;
        object? desktopDocument = null;
        object? desktopApplication = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application", throwOnError: false);
            if (shellType is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "Shell.Application is not registered for the current desktop.");
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "Shell.Application could not be activated.");
            }

            shellWindows = shellType.InvokeMember(
                "Windows",
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                target: shell,
                args: null);
            if (shellWindows is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "Shell.Application did not expose the current desktop window collection.");
            }

            var findWindowArguments = new object?[]
            {
                0,
                null,
                ShellWindowClassDesktop,
                0,
                ShellWindowFindNeedDispatch
            };
            desktopWindow = shellWindows.GetType().InvokeMember(
                "FindWindowSW",
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                target: shellWindows,
                args: findWindowArguments);
            if (desktopWindow is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "The current Explorer desktop window did not provide a Shell dispatch object.");
            }

            desktopDocument = desktopWindow.GetType().InvokeMember(
                "Document",
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                target: desktopWindow,
                args: null);
            desktopApplication = desktopDocument?.GetType().InvokeMember(
                "Application",
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                target: desktopDocument,
                args: null);
            if (desktopApplication is null)
            {
                return ExplorerShellLaunchResult.Unavailable(
                    "The Explorer desktop document did not expose its hosted Shell application.");
            }

            _ = desktopApplication.GetType().InvokeMember(
                "ShellExecute",
                BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                target: desktopApplication,
                args:
                [
                    executablePath,
                    UnelevatedRestartLaunchArguments.BuildArgumentList(arguments),
                    Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
                    "open",
                    1
                ]);
            return ExplorerShellLaunchResult.Accepted();
        }
        catch (Exception ex)
        {
            return ExplorerShellLaunchResult.Unavailable(
                $"Shell.Application.ShellExecute failed: {ex.GetBaseException().Message}");
        }
        finally
        {
            ReleaseComObject(desktopApplication);
            ReleaseComObject(desktopDocument);
            ReleaseComObject(desktopWindow);
            ReleaseComObject(shellWindows);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null || !Marshal.IsComObject(value))
        {
            return;
        }

        try
        {
            _ = Marshal.ReleaseComObject(value);
        }
        catch
        {
            // COM 引用释放失败不应覆盖启动结果。
        }
    }

    private readonly record struct ExplorerShellLaunchResult(
        bool Requested,
        string DiagnosticDetail)
    {
        public static ExplorerShellLaunchResult Accepted()
        {
            return new ExplorerShellLaunchResult(true, string.Empty);
        }

        public static ExplorerShellLaunchResult Unavailable(string diagnosticDetail)
        {
            return new ExplorerShellLaunchResult(false, diagnosticDetail);
        }
    }
}
