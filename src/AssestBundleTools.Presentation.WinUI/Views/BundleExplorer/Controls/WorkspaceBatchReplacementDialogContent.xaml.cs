using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

/// <summary>
/// 工作区级批量替换窗口内容。
/// </summary>
public sealed partial class WorkspaceBatchReplacementDialogContent : UserControl
{
    private ToolTip? _sourceSectionHelpToolTip;

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
        var sourceSectionHelpText = UiText.Get(ResourceKeys.WorkspaceBatchReplacementDialogSourceSectionHelp);
        _sourceSectionHelpToolTip = new ToolTip
        {
            MaxWidth = 360,
            Placement = Microsoft.UI.Xaml.Controls.Primitives.PlacementMode.Right,
            Content = new TextBlock
            {
                MaxWidth = 320,
                FontSize = 12,
                LineHeight = 18,
                Text = sourceSectionHelpText,
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };
        ToolTipService.SetToolTip(SourceSectionHelpButton, _sourceSectionHelpToolTip);
        AutomationProperties.SetName(SourceSectionHelpButton, sourceSectionHelpText);
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

    private void OnSourceSectionHelpButtonPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_sourceSectionHelpToolTip is not null)
        {
            _sourceSectionHelpToolTip.IsOpen = true;
        }
    }

    private void OnSourceSectionHelpButtonPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (_sourceSectionHelpToolTip is not null)
        {
            _sourceSectionHelpToolTip.IsOpen = false;
        }
    }

    private void OnSelectAllBundlesButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectAllBundles();

    private void OnClearBundleSelectionButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.ClearBundleSelection();
}
