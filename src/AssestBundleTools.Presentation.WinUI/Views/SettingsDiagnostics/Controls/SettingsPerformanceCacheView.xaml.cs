using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics.Controls;

public sealed partial class SettingsPerformanceCacheView : UserControl
{
    public SettingsPerformanceCacheView(SettingsDiagnosticsViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        ApplyLocalizedText();
    }

    public SettingsDiagnosticsViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        WorkspaceImportPerformanceTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceTitle);
        WorkspaceImportPerformanceHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportPerformanceHint);
        WorkspaceImportWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountLabel);
        WorkspaceImportWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsWorkspaceImportWorkerCountRowDescription);
        PreviewWorkerCountLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountLabel);
        PreviewWorkerCountDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsPreviewWorkerCountRowDescription);
        BundleArtifactCacheTitleText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheTitle);
        BundleArtifactCacheHintText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheHint);
        BundleArtifactCacheAutoPruneLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneHeader);
        BundleArtifactCacheAutoPruneDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheAutoPruneRowDescription);
        BundleArtifactCacheMaximumSizeLabelText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeLabel);
        BundleArtifactCacheMaximumSizeDescriptionText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheMaximumSizeRowDescription);
        RefreshBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheRefreshButton);
        ClearBundleArtifactCacheButtonText.Text = UiText.Get(ResourceKeys.SettingsDiagnosticsBundleArtifactCacheClearButton);
    }
}
