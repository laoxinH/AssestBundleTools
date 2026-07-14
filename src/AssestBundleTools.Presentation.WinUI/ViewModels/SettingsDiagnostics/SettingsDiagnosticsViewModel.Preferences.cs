using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

public sealed partial class SettingsDiagnosticsViewModel
{
    private bool _restoreLastWorkspaceOnStartup;
    private bool _elevatedDragDropCompatibilityEnabled;
    private int _recentWorkspaceOpenBehaviorSelectedIndex;
    private int _assetEditorCloseBehaviorSelectedIndex;
    private int _displayLanguageSelectedIndex;
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

    public bool ElevatedDragDropCompatibilityEnabled
    {
        get => _elevatedDragDropCompatibilityEnabled;
        set
        {
            if (_elevatedDragDropCompatibilityEnabled == value)
            {
                return;
            }

            if (_appPreferencesService.TrySetElevatedDragDropCompatibilityEnabled(value))
            {
                SetProperty(ref _elevatedDragDropCompatibilityEnabled, value);
                return;
            }

            _appLogService.AppendError(GetString(
                ResourceKeys.SettingsDiagnosticsElevatedDragDropCompatibilitySaveFailedMessage));
            ShowErrorNotification(
                ResourceKeys.SettingsDiagnosticsElevatedDragDropCompatibilitySaveFailedMessage);
            OnPropertyChanged();
        }
    }

    public int DisplayLanguageSelectedIndex
    {
        get => _displayLanguageSelectedIndex;
        set
        {
            var selectedIndex = SettingsDiagnosticsPreferenceOptionMapper.CoerceDisplayLanguageSelectedIndex(value);
            if (_displayLanguageSelectedIndex == selectedIndex)
            {
                return;
            }

            var displayLanguage = SettingsDiagnosticsPreferenceOptionMapper.ConvertSelectedIndexToDisplayLanguage(
                selectedIndex);
            if (_appPreferencesService.TrySetDisplayLanguage(displayLanguage))
            {
                SetProperty(ref _displayLanguageSelectedIndex, selectedIndex);
                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsDisplayLanguageSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsDisplayLanguageSaveFailedMessage);
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
                if (SetProperty(ref _workspaceImportWorkerCountSelectedIndex, selectedIndex))
                {
                    OnPropertyChanged(nameof(WorkspaceImportWorkerCountSliderValue));
                    OnPropertyChanged(nameof(WorkspaceImportWorkerCountSelectedText));
                }

                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountSaveFailedMessage);
            OnPropertyChanged();
            OnPropertyChanged(nameof(WorkspaceImportWorkerCountSliderValue));
            OnPropertyChanged(nameof(WorkspaceImportWorkerCountSelectedText));
        }
    }

    public double WorkspaceImportWorkerCountSliderValue
    {
        get => WorkspaceImportWorkerCountSelectedIndex;
        set => WorkspaceImportWorkerCountSelectedIndex = CoerceSliderIndex(value);
    }

    public string WorkspaceImportWorkerCountSelectedText => GetString(GetWorkspaceImportWorkerCountOptionKey(
        WorkspaceImportWorkerCountSelectedIndex));

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
                if (SetProperty(ref _previewWorkerCountSelectedIndex, selectedIndex))
                {
                    OnPropertyChanged(nameof(PreviewWorkerCountSliderValue));
                    OnPropertyChanged(nameof(PreviewWorkerCountSelectedText));
                }

                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountSaveFailedMessage);
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewWorkerCountSliderValue));
            OnPropertyChanged(nameof(PreviewWorkerCountSelectedText));
        }
    }

    public double PreviewWorkerCountSliderValue
    {
        get => PreviewWorkerCountSelectedIndex;
        set => PreviewWorkerCountSelectedIndex = CoerceSliderIndex(value);
    }

    public string PreviewWorkerCountSelectedText => GetString(GetPreviewWorkerCountOptionKey(
        PreviewWorkerCountSelectedIndex));

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
                if (SetProperty(ref _operationRecordLimitSelectedIndex, selectedIndex))
                {
                    OnPropertyChanged(nameof(OperationRecordLimitSliderValue));
                    OnPropertyChanged(nameof(OperationRecordLimitSelectedText));
                }

                return;
            }

            _appLogService.AppendError(GetString(ResourceKeys.SettingsDiagnosticsOperationRecordLimitSaveFailedMessage));
            ShowErrorNotification(ResourceKeys.SettingsDiagnosticsOperationRecordLimitSaveFailedMessage);
            OnPropertyChanged();
            OnPropertyChanged(nameof(OperationRecordLimitSliderValue));
            OnPropertyChanged(nameof(OperationRecordLimitSelectedText));
        }
    }

    public double OperationRecordLimitSliderValue
    {
        get => OperationRecordLimitSelectedIndex;
        set => OperationRecordLimitSelectedIndex = CoerceSliderIndex(value);
    }

    public string OperationRecordLimitSelectedText => GetString(GetOperationRecordLimitOptionKey(
        OperationRecordLimitSelectedIndex));

    private static int CoerceSliderIndex(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    private static string GetWorkspaceImportWorkerCountOptionKey(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionOne,
            2 => ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionTwo,
            3 => ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionThree,
            4 => ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionFour,
            _ => ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountOptionAutomatic
        };
    }

    private static string GetPreviewWorkerCountOptionKey(int selectedIndex)
    {
        return selectedIndex switch
        {
            1 => ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionOne,
            2 => ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionTwo,
            3 => ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionThree,
            4 => ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionFour,
            _ => ResourceKeys.SettingsDiagnosticsPreviewWorkerCountOptionAutomatic
        };
    }

    private static string GetOperationRecordLimitOptionKey(int selectedIndex)
    {
        return selectedIndex switch
        {
            0 => ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionOneHundred,
            2 => ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionFiveHundred,
            3 => ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionOneThousand,
            _ => ResourceKeys.SettingsDiagnosticsOperationRecordLimitOptionTwoHundredFifty
        };
    }
}
