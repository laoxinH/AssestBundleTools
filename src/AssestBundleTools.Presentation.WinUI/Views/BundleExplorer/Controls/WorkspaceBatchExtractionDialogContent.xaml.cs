using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 工作区级批量提取对话框内容。
/// </summary>
public sealed partial class WorkspaceBatchExtractionDialogContent : UserControl
{
    public WorkspaceBatchExtractionDialogContent(
        WorkspaceBatchExtractionDialogViewModel viewModel)
    {
        ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = ViewModel;
        ApplyLocalizedText();
    }

    public WorkspaceBatchExtractionDialogViewModel ViewModel { get; }

    private void ApplyLocalizedText()
    {
        BundleSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogBundleSectionTitle);
        BundleSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogBundleSectionHint);
        BundleSearchTextBox.PlaceholderText = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogBundleSearchPlaceholder);
        SelectAllBundlesButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogSelectAllBundlesButton);
        ClearBundleSelectionButton.Content = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogClearBundleSelectionButton);
        BundleEmptyStateText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogBundleEmptyState);

        CategorySectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogCategorySectionTitle);
        CategorySectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceBatchExtractionDialogCategorySectionHint);
    }

    private void OnBundleSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel.BundleSearchKeyword = BundleSearchTextBox.Text;
    }

    private void OnSelectAllBundlesButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAllBundles();
    }

    private void OnClearBundleSelectionButtonClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearBundleSelection();
    }
}
