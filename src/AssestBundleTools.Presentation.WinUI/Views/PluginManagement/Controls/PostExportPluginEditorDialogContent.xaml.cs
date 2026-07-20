using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement.Controls;

public sealed partial class PostExportPluginEditorDialogContent : UserControl
{
    private string? _previousPluginId;

    public PostExportPluginEditorDialogContent()
    {
        InitializeComponent();
        ApplyLocalizedText();
        ArgumentsTextBox.Text = string.Join(Environment.NewLine,
        [
            "--manifest",
            "{manifestPath}",
            "--output-directory",
            "{outputDirectory}"
        ]);
    }

    public void ApplyEntry(PostExportPluginCatalogEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _previousPluginId = entry.Id;
        IdTextBox.Text = entry.Id;
        DisplayNameTextBox.Text = entry.DisplayName;
        VersionTextBox.Text = entry.Version;
        DescriptionTextBox.Text = entry.Description;
        ExecutablePathTextBox.Text = entry.ExecutablePath;
        WorkingDirectoryTextBox.Text = entry.WorkingDirectoryPath ?? string.Empty;
        ArgumentsTextBox.Text = string.Join(Environment.NewLine, entry.ProcessArguments);
    }

    public bool TryCreateCommand(out UpsertPostExportPluginCommand? command)
    {
        command = null;
        ValidationText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(IdTextBox.Text) ||
            string.IsNullOrWhiteSpace(DisplayNameTextBox.Text) ||
            string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text))
        {
            ValidationText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorValidationRequired);
            return false;
        }

        var arguments = ArgumentsTextBox.Text
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!arguments.Any(static argument => argument.Contains("{manifestPath}", StringComparison.Ordinal)))
        {
            ValidationText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorValidationManifestPathRequired);
            return false;
        }

        command = new UpsertPostExportPluginCommand(
            new PostExportPluginDefinitionDto(
                IdTextBox.Text.Trim(),
                DisplayNameTextBox.Text.Trim(),
                string.IsNullOrWhiteSpace(VersionTextBox.Text) ? "1.0.0" : VersionTextBox.Text.Trim(),
                DescriptionTextBox.Text,
                ExecutablePathTextBox.Text.Trim(),
                WorkingDirectoryTextBox.Text,
                arguments),
            _previousPluginId);
        return true;
    }

    private void ApplyLocalizedText()
    {
        IdLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorIdLabel);
        DisplayNameLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDisplayNameLabel);
        VersionLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorVersionLabel);
        DescriptionLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDescriptionLabel);
        ExecutablePathLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorExecutablePathLabel);
        WorkingDirectoryLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorWorkingDirectoryPathLabel);
        ArgumentsLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorArgumentsLabel);
        ArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorArgumentsDescription);
        IdTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorIdPlaceholder);
        DisplayNameTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorDisplayNamePlaceholder);
        ExecutablePathTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorExecutablePathPlaceholder);
    }
}
