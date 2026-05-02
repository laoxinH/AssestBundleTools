using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

internal static class SettingsDiagnosticsPreferenceOptionMapper
{
    public static int ConvertRecentWorkspaceOpenBehaviorToSelectedIndex(RecentWorkspaceOpenBehavior behavior)
    {
        return behavior switch
        {
            RecentWorkspaceOpenBehavior.NewWindow => 1,
            RecentWorkspaceOpenBehavior.AskEveryTime => 2,
            _ => 0
        };
    }

    public static RecentWorkspaceOpenBehavior ConvertSelectedIndexToRecentWorkspaceOpenBehavior(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => RecentWorkspaceOpenBehavior.NewWindow,
            2 => RecentWorkspaceOpenBehavior.AskEveryTime,
            _ => RecentWorkspaceOpenBehavior.CurrentWindow
        };
    }

    public static int CoerceRecentWorkspaceOpenBehaviorSelectedIndex(int selectedIndex)
    {
        return selectedIndex is >= 0 and <= 2
            ? selectedIndex
            : 0;
    }

    public static int ConvertWorkspaceImportWorkerCountToSelectedIndex(int workerCount)
    {
        return AppPreferencesService.CoerceWorkspaceImportWorkerCount(workerCount);
    }

    public static int ConvertSelectedIndexToWorkspaceImportWorkerCount(int selectedIndex)
    {
        return AppPreferencesService.CoerceWorkspaceImportWorkerCount(selectedIndex);
    }
}
