namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示新窗口启动后应进入的目标页面和附带参数。
/// </summary>
public sealed record ShellLaunchRequest(
    string NavigationKey,
    object? NavigationParameter);
