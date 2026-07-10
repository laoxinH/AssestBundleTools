using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class WorkspaceExportDialogContent : UserControl
{
    private ToolTip? _resourceProcessorHelpToolTip;
    private ToolTip? _forceAudioPayloadWriteToolTip;

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
        BundleEmptyStateTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleEmptyTitle);
        BundleEmptyStateDetailText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogBundleEmptyDetail);
        ResourceProcessorSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorSectionTitle);
        ResourceProcessorSectionHintText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorSectionHint);
        ResourceProcessorHelpText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorHelpText);
        var resourceProcessorHelpToolTipText = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorHelpToolTip);
        _resourceProcessorHelpToolTip = CreateImmediateInfoToolTip(resourceProcessorHelpToolTipText);
        ToolTipService.SetToolTip(ResourceProcessorHelpHost, _resourceProcessorHelpToolTip);
        AutomationProperties.SetName(ResourceProcessorHelpHost, ResourceProcessorHelpText.Text);
        AutomationProperties.SetHelpText(ResourceProcessorHelpHost, resourceProcessorHelpToolTipText);
        ExportOptionsSectionTitleText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogExportOptionsSectionTitle);
        UseSourceCodecCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogUseSourceCodec);
        ExportToWorkspaceDirectoryCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogExportToWorkspaceDirectory);
        OverwriteExistingFilesCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogOverwriteExistingFiles);
        ForceAudioPayloadWriteText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogForceAudioPayloadWrite);
        var forceAudioPayloadWriteToolTipText = UiText.Get(ResourceKeys.WorkspaceExportDialogForceAudioPayloadWriteWarning);
        _forceAudioPayloadWriteToolTip = CreateImmediateInfoToolTip(forceAudioPayloadWriteToolTipText);
        ToolTipService.SetToolTip(ForceAudioPayloadWriteHelpHost, _forceAudioPayloadWriteToolTip);
        AutomationProperties.SetName(ForceAudioPayloadWriteHelpHost, ForceAudioPayloadWriteText.Text);
        AutomationProperties.SetHelpText(ForceAudioPayloadWriteHelpHost, forceAudioPayloadWriteToolTipText);
        CompressionModeLabelText.Text = UiText.Get(ResourceKeys.WorkspaceExportDialogCompressionModeLabel);
    }

    private void OnSelectAllBundlesButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.SelectAllBundles();

    private void OnClearBundleSelectionButtonClick(object sender, RoutedEventArgs e) =>
        ViewModel.ClearBundleSelection();

    private static ToolTip CreateImmediateInfoToolTip(string text) =>
        new()
        {
            MaxWidth = 360,
            Placement = PlacementMode.Right,
            Content = new TextBlock
            {
                MaxWidth = 320,
                FontSize = 12,
                LineHeight = 18,
                Text = text,
                TextWrapping = TextWrapping.WrapWholeWords
            }
        };

    private void OnResourceProcessorHelpPointerEntered(object sender, PointerRoutedEventArgs e) =>
        SetImmediateToolTipOpen(_resourceProcessorHelpToolTip, true);

    private void OnResourceProcessorHelpPointerExited(object sender, PointerRoutedEventArgs e) =>
        SetImmediateToolTipOpen(_resourceProcessorHelpToolTip, false);

    private void OnForceAudioPayloadWriteHelpPointerEntered(object sender, PointerRoutedEventArgs e) =>
        SetImmediateToolTipOpen(_forceAudioPayloadWriteToolTip, true);

    private void OnForceAudioPayloadWriteHelpPointerExited(object sender, PointerRoutedEventArgs e) =>
        SetImmediateToolTipOpen(_forceAudioPayloadWriteToolTip, false);

    private static void SetImmediateToolTipOpen(ToolTip? toolTip, bool isOpen)
    {
        if (toolTip is not null)
        {
            toolTip.IsOpen = isOpen;
        }
    }
}
