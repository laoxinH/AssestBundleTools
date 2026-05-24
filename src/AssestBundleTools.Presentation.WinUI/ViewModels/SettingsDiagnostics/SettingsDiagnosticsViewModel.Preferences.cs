using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

public sealed partial class SettingsDiagnosticsViewModel
{
    private bool _restoreLastWorkspaceOnStartup;
    private int _recentWorkspaceOpenBehaviorSelectedIndex;
    private int _assetEditorCloseBehaviorSelectedIndex;
    private int _workspaceImportWorkerCountSelectedIndex;
    private int _previewWorkerCountSelectedIndex;
    private int _operationRecordLimitSelectedIndex;

    public bool RestoreLastWorkspaceOnStartup
    {
        get => _restoreLastWorkspaceOnStartup;
        set
        {
            if (_restoreLastWorkspaceOnStartup == value)
            {
                return;
            }

            if (_appPreferencesService.TrySetRestoreLastWorkspaceOnStartup(value))
            {
                SetProperty(ref _restoreLastWorkspaceOnStartup, value);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsStartupBehaviorSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsStartupBehaviorSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 控制台中“最近工作区”的默认打开策略。
    /// 当前窗口、新建窗口或每次询问。
    /// </summary>
    public int RecentWorkspaceOpenBehaviorSelectedIndex
    {
        get => _recentWorkspaceOpenBehaviorSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.CoerceRecentWorkspaceOpenBehaviorSelectedIndex(value);
            if (_recentWorkspaceOpenBehaviorSelectedIndex == selectedIndex)
            {
                return;
            }

            var openBehavior = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToRecentWorkspaceOpenBehavior(selectedIndex);
            if (_appPreferencesService.TrySetRecentWorkspaceOpenBehavior(openBehavior))
            {
                SetProperty(ref _recentWorkspaceOpenBehaviorSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// 独立资源编辑窗口中关闭标签或窗口时的默认去向。
    /// </summary>
    public int AssetEditorCloseBehaviorSelectedIndex
    {
        get => _assetEditorCloseBehaviorSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.CoerceAssetEditorCloseBehaviorSelectedIndex(value);
            if (_assetEditorCloseBehaviorSelectedIndex == selectedIndex)
            {
                return;
            }

            var closeBehavior = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToAssetEditorCloseBehavior(selectedIndex);
            if (_appPreferencesService.TrySetAssetEditorCloseBehavior(closeBehavior))
            {
                SetProperty(ref _assetEditorCloseBehaviorSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public int WorkspaceImportWorkerCountSelectedIndex
    {
        get => _workspaceImportWorkerCountSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.ConvertWorkspaceImportWorkerCountToSelectedIndex(value);
            if (_workspaceImportWorkerCountSelectedIndex == selectedIndex)
            {
                return;
            }

            var workerCount = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToWorkspaceImportWorkerCount(selectedIndex);
            if (_appPreferencesService.TrySetWorkspaceImportWorkerCount(workerCount))
            {
                SetProperty(ref _workspaceImportWorkerCountSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public int PreviewWorkerCountSelectedIndex
    {
        get => _previewWorkerCountSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.ConvertPreviewWorkerCountToSelectedIndex(value);
            if (_previewWorkerCountSelectedIndex == selectedIndex)
            {
                return;
            }

            var workerCount = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToPreviewWorkerCount(selectedIndex);
            if (_appPreferencesService.TrySetPreviewWorkerCount(workerCount))
            {
                SetProperty(ref _previewWorkerCountSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountSaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public int OperationRecordLimitSelectedIndex
    {
        get => _operationRecordLimitSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.ConvertOperationRecordLimitToSelectedIndex(
                SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToOperationRecordLimit(value));
            if (_operationRecordLimitSelectedIndex == selectedIndex)
            {
                return;
            }

            var operationRecordLimit = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToOperationRecordLimit(selectedIndex);
            if (_appPreferencesService.TrySetOperationRecordLimit(operationRecordLimit))
            {
                SetProperty(ref _operationRecordLimitSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsOperationRecordLimitSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsOperationRecordLimitSaveFailedMessage);
            OnPropertyChanged();
        }
    }
}
