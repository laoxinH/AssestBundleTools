using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class WorkspaceExportDialogContent : UserControl
{
    public WorkspaceExportDialogContent(WorkspaceExportDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
        ApplyLocalizedText();
    }

    public WorkspaceExportDialogViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        BundleSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleSectionTitle);
        BundleSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleSectionHint);
        BundleSearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleSearchPlaceholder);
        SelectAllBundlesButton.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogSelectAllBundlesButton);
        ClearBundleSelectionButton.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogClearBundleSelectionButton);
        ResourceProcessorSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorSectionTitle);
        ResourceProcessorSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorSectionHint);
        ResourceProcessorHelpText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorHelpText);
        ExportOptionsSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogExportOptionsSectionTitle);
        ExportToWorkspaceDirectoryCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogExportToWorkspaceDirectory);
        OverwriteExistingFilesCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogOverwriteExistingFiles);
        ForceAudioPayloadWriteCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogForceAudioPayloadWrite);
        CompressionModeLabelText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogCompressionModeLabel);
    }

    private void OnSelectAllBundlesButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectAllBundles();

    private void OnClearBundleSelectionButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.ClearBundleSelection();
}
