using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

/// <summary>
/// 统一独立窗口与主窗口的系统材质和标题栏按钮外观。
/// </summary>
internal static class ModernWindowAppearanceService
{
    private const string DisableSystemBackdropEnvironmentVariable = "ABT_DISABLE_SYSTEM_BACKDROP";

    internal static void TryApplySystemBackdrop(
        Window window,
        Action<string>? diagnosticLogger = null)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (string.Equals(
                Environment.GetEnvironmentVariable(DisableSystemBackdropEnvironmentVariable),
                "1",
                StringComparison.Ordinal))
        {
            Log("System backdrop initialization skipped by environment variable.", diagnosticLogger);
            return;
        }

        try
        {
            window.SystemBackdrop = new MicaBackdrop();
        }
        catch (COMException ex)
        {
            Log($"System backdrop initialization failed with COM exception: {ex}", diagnosticLogger);
        }
        catch (Exception ex)
        {
            Log($"System backdrop initialization failed with unexpected exception: {ex}", diagnosticLogger);
        }
    }

    internal static void ConfigureTitleBar(AppWindowTitleBar titleBar)
    {
        ArgumentNullException.ThrowIfNull(titleBar);

        titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = ColorHelper.FromArgb(24, 128, 128, 128);
        titleBar.ButtonPressedBackgroundColor = ColorHelper.FromArgb(38, 128, 128, 128);
    }

    internal static void ApplyTitleBarTheme(Window window, ElementTheme actualTheme)
    {
        ArgumentNullException.ThrowIfNull(window);

        try
        {
            var titleBar = window.AppWindow?.TitleBar;
            if (titleBar is null)
            {
                return;
            }

            var foreground = actualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
            var inactiveForeground = actualTheme == ElementTheme.Dark
                ? ColorHelper.FromArgb(153, 255, 255, 255)
                : ColorHelper.FromArgb(153, 0, 0, 0);
            titleBar.ButtonForegroundColor = foreground;
            titleBar.ButtonHoverForegroundColor = foreground;
            titleBar.ButtonPressedForegroundColor = foreground;
            titleBar.ButtonInactiveForegroundColor = inactiveForeground;
        }
        catch (Exception ex)
        {
            Log($"Title bar theme update failed: {ex}", diagnosticLogger: null);
        }
    }

    private static void Log(string message, Action<string>? diagnosticLogger)
    {
        diagnosticLogger?.Invoke(message);
        Debug.WriteLine(message);
    }
}
