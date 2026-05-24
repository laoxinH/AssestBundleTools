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

    public static int ConvertAssetEditorCloseBehaviorToSelectedIndex(AssetEditorCloseBehavior behavior)
    {
        return behavior switch
        {
            AssetEditorCloseBehavior.DirectClose => 1,
            _ => 0
        };
    }

    public static AssetEditorCloseBehavior ConvertSelectedIndexToAssetEditorCloseBehavior(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => AssetEditorCloseBehavior.DirectClose,
            _ => AssetEditorCloseBehavior.ReturnToMainWindow
        };
    }

    public static int CoerceAssetEditorCloseBehaviorSelectedIndex(int selectedIndex)
    {
        return selectedIndex is >= 0 and <= 1
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

    public static int ConvertPreviewWorkerCountToSelectedIndex(int workerCount)
    {
        return AppPreferencesService.CoercePreviewWorkerCount(workerCount);
    }

    public static int ConvertSelectedIndexToPreviewWorkerCount(int selectedIndex)
    {
        return AppPreferencesService.CoercePreviewWorkerCount(selectedIndex);
    }

    public static int ConvertBundleArtifactCacheMaximumSizeToSelectedIndex(long maximumSizeInBytes)
    {
        return AppPreferencesService.CoerceBundleArtifactCacheMaximumSizeInBytes(maximumSizeInBytes) switch
        {
            2L * 1024L * 1024L * 1024L => 1,
            8L * 1024L * 1024L * 1024L => 3,
            16L * 1024L * 1024L * 1024L => 4,
            _ => 2
        };
    }

    public static long ConvertSelectedIndexToBundleArtifactCacheMaximumSize(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => 1L * 1024L * 1024L * 1024L,
            1 => 2L * 1024L * 1024L * 1024L,
            3 => 8L * 1024L * 1024L * 1024L,
            4 => 16L * 1024L * 1024L * 1024L,
            _ => AppPreferencesService.DefaultBundleArtifactCacheMaximumSizeInBytes
        };
    }

    public static int ConvertOperationRecordLimitToSelectedIndex(int operationRecordLimit)
    {
        return AppPreferencesService.CoerceOperationRecordLimit(operationRecordLimit) switch
        {
            100 => 0,
            500 => 2,
            1000 => 3,
            _ => 1
        };
    }

    public static int ConvertSelectedIndexToOperationRecordLimit(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => 100,
            2 => 500,
            3 => 1000,
            _ => AppPreferencesService.DefaultOperationRecordLimit
        };
    }
}
