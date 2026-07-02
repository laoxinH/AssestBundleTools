namespace AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Contracts;

/// <summary>
/// 集中定义应用外壳中的导航标识。
/// </summary>
/// <remarks>
/// 这些值不会直接展示给用户，因此可以作为稳定路由键使用。
/// </remarks>
public static class ShellNavigationKeys
{
    public const string LaunchGuide = "launch-guide";
    public const string Workbench = "workbench";
    public const string PluginManagement = "plugin-management";
    public const string SettingsDiagnostics = "settings-diagnostics";
}
