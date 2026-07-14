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

    internal void SetContentColumnMaxWidth(double maxWidth)
    {
        PreferenceSettingsContentColumn.MaxWidth = maxWidth;
    }

    private void ApplyLocalizedText()
    {
        AppearanceLanguageTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAppearanceLanguageTitle);
        AppearanceLanguageHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsAppearanceLanguageHint);
        DisplayLanguageLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDisplayLanguageLabel);
        DisplayLanguageDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsDisplayLanguageRowDescription);
        DisplayLanguageComboBox.ItemsSource = new[]
        {
            UiText.Get(ResourceKeys.SettingsDiagnosticsDisplayLanguageOptionSystemDefault),
            UiText.Get(ResourceKeys.SettingsDiagnosticsDisplayLanguageOptionSimplifiedChinese),
            UiText.Get(ResourceKeys.SettingsDiagnosticsDisplayLanguageOptionEnglish)
        };
        StartupBehaviorTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsStartupBehaviorTitle);
        StartupBehaviorHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsStartupBehaviorHint);
        RestoreLastWorkspaceOnStartupLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceToggleHeader);
        RestoreLastWorkspaceOnStartupDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsRestoreLastWorkspaceRowDescription);
        ElevatedDragDropCompatibilityLabelText.Text = UiText.Get(
            ResourceKeys.SettingsDiagnosticsElevatedDragDropCompatibilityLabel);
        ElevatedDragDropCompatibilityDescriptionText.Text = UiText.Get(
            ResourceKeys.SettingsDiagnosticsElevatedDragDropCompatibilityDescription);
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
