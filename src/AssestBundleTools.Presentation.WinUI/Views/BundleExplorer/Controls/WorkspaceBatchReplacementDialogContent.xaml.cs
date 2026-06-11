using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 工作区级批量替换窗口内容。
/// </summary>
public sealed partial class WorkspaceBatchReplacementDialogContent : UserControl
{
    public WorkspaceBatchReplacementDialogContent(WorkspaceBatchReplacementDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
        ApplyLocalizedText();
    }

    public event EventHandler? SourceDirectoryRequested;

    public event EventHandler? RefreshRequested;

    public WorkspaceBatchReplacementDialogViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        SourceSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogSourceSectionTitle);
        SourceSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogSourceSectionHint);
        ChooseSourceDirectoryButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogChooseSourceDirectoryButton);
        RefreshPlanButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogRefreshButton);
        BundleSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogBundleSectionTitle);
        BundleSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogBundleSectionHint);
        SelectAllBundlesButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogSelectAllBundlesButton);
        ClearBundleSelectionButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogClearBundleSelectionButton);
        ChangedBundlesOnlyToggleButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogChangedBundlesOnlyButton);
        CandidateSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogCandidateSectionTitle);
        CandidateSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogCandidateSectionHint);
        CandidateSearchBox.PlaceholderText = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogCandidateSearchPlaceholder);
        SourceEmptyStateText.Text = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogSourceEmptyState);
        TypeFilterListView.ItemsSource = ViewModel.TypeFilters;
    }

    private void OnChooseSourceDirectoryButtonClick(object sender, RoutedEventArgs e) =>
        SourceDirectoryRequested?.Invoke(this, EventArgs.Empty);

    private void OnRefreshPlanButtonClick(object sender, RoutedEventArgs e) =>
        RefreshRequested?.Invoke(this, EventArgs.Empty);

    private void OnSelectAllBundlesButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectAllBundles();

    private void OnClearBundleSelectionButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.ClearBundleSelection();
}
