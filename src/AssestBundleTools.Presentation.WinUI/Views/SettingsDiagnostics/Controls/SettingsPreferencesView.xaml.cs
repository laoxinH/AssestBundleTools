using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics.Controls;

public sealed partial class SettingsPreferencesView : UserControl
{
    public SettingsPreferencesView(SettingsDiagnosticsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        ApplyLocalizedText();
    }

    public SettingsDiagnosticsViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        StartupBehaviorTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsStartupBehaviorTitle);
        StartupBehaviorHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsStartupBehaviorHint);
        RestoreLastWorkspaceOnStartupLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceToggleHeader);
        RestoreLastWorkspaceOnStartupDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceRowDescription);
        RecentWorkspaceOpenBehaviorLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorLabel);
        RecentWorkspaceOpenBehaviorDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorRowDescription);
        RecentWorkspaceOpenBehaviorComboBox.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionCurrentWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionNewWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsRecentWorkspaceOpenBehaviorOptionAskEveryTime)
        };
        AssetEditorCloseBehaviorLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorLabel);
        AssetEditorCloseBehaviorDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorRowDescription);
        AssetEditorCloseBehaviorComboBox.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorOptionReturnToMainWindow),
            UiText.Get(ResourceKeys.SettingsDiagnosticsAssetEditorCloseBehaviorOptionDirectClose)
        };
        OperationRecordsTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordsTitle);
        OperationRecordsHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordsHint);
        OperationRecordLimitLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitLabel);
        OperationRecordLimitDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsOperationRecordLimitRowDescription);
    }
}
