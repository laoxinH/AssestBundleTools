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
        SourceSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogSourceSectionTitle);
        ProcessorBadgeText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogProcessorBadge);
        BundleSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleSectionTitle);
        BundleSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleSectionHint);
        SelectAllBundlesButton.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogSelectAllBundlesButton);
        ClearBundleSelectionButton.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogClearBundleSelectionButton);
        RecentSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogRecentSectionTitle);
        OutputSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogOutputSectionTitle);
        OutputSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogOutputSectionHint);
    }

    private void OnSelectAllBundlesButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectAllBundles();

    private void OnClearBundleSelectionButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.ClearBundleSelection();
}
