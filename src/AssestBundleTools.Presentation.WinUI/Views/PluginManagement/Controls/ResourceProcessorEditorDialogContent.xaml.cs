using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement.Controls;

/// <summary>
/// 承载导出资源处理器新增与编辑的表单内容。
/// </summary>
public sealed partial class ResourceProcessorEditorDialogContent : UserControl
{
    private string? _previousProcessorId;

    public ResourceProcessorEditorDialogContent()
    {
        InitializeComponent();
        ApplyLocalizedText();
        ProcessorVersionTextBox.Text = "1.0.0";
        ProcessorTimeoutTextBox.Text = "15000";
        ProcessorArgumentsTextBox.Text = string.Join(
            Environment.NewLine,
            "--input",
            "{inputPath}",
            "--output",
            "{outputPath}",
            "--result",
            "{resultPath}",
            "--resource-kind",
            "{resourceKind}",
            "--workspace",
            "{workspaceDataRootPath}");
    }

    public string? PreviousProcessorId => _previousProcessorId;

    public void ApplyEntry(ResourceProcessorCatalogEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _previousProcessorId = entry.Id;
        ProcessorIdTextBox.Text = entry.Id;
        ProcessorDisplayNameTextBox.Text = entry.DisplayName;
        ProcessorVersionTextBox.Text = string.IsNullOrWhiteSpace(entry.Version) ? "1.0.0" : entry.Version;
        ProcessorDescriptionTextBox.Text = entry.Description ?? string.Empty;
        ProcessorExecutablePathTextBox.Text = entry.ExecutablePath ?? string.Empty;
        ProcessorWorkingDirectoryPathTextBox.Text = entry.WorkingDirectoryPath ?? string.Empty;
        ProcessorTimeoutTextBox.Text = entry.TimeoutMs.ToString(System.Globalization.CultureInfo.CurrentCulture);
        ProcessorArgumentsTextBox.Text = string.Join(Environment.NewLine, entry.ProcessArguments);

        Texture2DCheckBox.IsChecked = HasResourceKind(entry, "Texture2D");
        AudioCheckBox.IsChecked = HasResourceKind(entry, "Audio");
        AtlasCheckBox.IsChecked = HasResourceKind(entry, "Atlas");
        SkelCheckBox.IsChecked = HasResourceKind(entry, "Skel");
    }

    public bool TryCreateCommand(out UpsertResourceProcessorCommand? command)
    {
        ClearValidationMessages();

        var hasValidationError = false;
        var processorId = ProcessorIdTextBox.Text.Trim();
        var displayName = ProcessorDisplayNameTextBox.Text.Trim();
        var executablePath = ProcessorExecutablePathTextBox.Text.Trim();
        var timeoutText = ProcessorTimeoutTextBox.Text.Trim();
        var resourceKinds = GetSelectedResourceKinds();
        var processArguments = SplitArguments(ProcessorArgumentsTextBox.Text);

        if (string.IsNullOrWhiteSpace(processorId))
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorIdTextBox,
                ProcessorIdValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorDisplayNameTextBox,
                ProcessorDisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationDisplayNameRequired));
        }

        if (resourceKinds.Count == 0)
        {
            hasValidationError = true;
            ProcessorResourceKindsValidationTextBlock.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorValidationResourceKindsRequired);
            ProcessorResourceKindsValidationTextBlock.Visibility = Visibility.Visible;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorExecutablePathTextBox,
                ProcessorExecutablePathValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationExecutablePathRequired));
        }

        if (!int.TryParse(timeoutText, out var timeoutMs) || timeoutMs <= 0)
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorTimeoutTextBox,
                ProcessorTimeoutValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationTimeoutInvalid));
        }

        if (processArguments.Count == 0)
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorArgumentsTextBox,
                ProcessorArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationArgumentsRequired));
        }

        if (hasValidationError)
        {
            command = null;
            return false;
        }

        var definition = new ResourceProcessorDefinitionDto(
            Id: processorId,
            DisplayName: displayName,
            Version: string.IsNullOrWhiteSpace(ProcessorVersionTextBox.Text) ? "1.0.0" : ProcessorVersionTextBox.Text.Trim(),
            Description: string.IsNullOrWhiteSpace(ProcessorDescriptionTextBox.Text) ? null : ProcessorDescriptionTextBox.Text.Trim(),
            ResourceKinds: resourceKinds,
            ExecutablePath: executablePath,
            WorkingDirectoryPath: string.IsNullOrWhiteSpace(ProcessorWorkingDirectoryPathTextBox.Text)
                ? null
                : ProcessorWorkingDirectoryPathTextBox.Text.Trim(),
            TimeoutMs: timeoutMs,
            ProcessArguments: processArguments);

        command = new UpsertResourceProcessorCommand(
            Definition: definition,
            PreviousProcessorId: _previousProcessorId);
        return true;
    }

    private void ApplyLocalizedText()
    {
        ProcessorIdLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorIdLabel);
        ProcessorIdTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorIdPlaceholder);
        ProcessorDisplayNameLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDisplayNameLabel);
        ProcessorDisplayNameTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorDisplayNamePlaceholder);
        ProcessorVersionLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorVersionLabel);
        ProcessorVersionTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorVersionPlaceholder);
        ProcessorDescriptionLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDescriptionLabel);
        ProcessorDescriptionTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorDescriptionPlaceholder);
        ProcessorResourceKindsLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorResourceKindsLabel);
        Texture2DCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D);
        AudioCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio);
        AtlasCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas);
        SkelCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel);
        ProcessorExecutablePathLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorExecutablePathLabel);
        ProcessorExecutablePathTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorExecutablePathPlaceholder);
        ProcessorWorkingDirectoryPathLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorWorkingDirectoryPathLabel);
        ProcessorWorkingDirectoryPathTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorWorkingDirectoryPathPlaceholder);
        ProcessorTimeoutLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorTimeoutLabel);
        ProcessorTimeoutTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorTimeoutPlaceholder);
        ProcessorArgumentsLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorArgumentsLabel);
        ProcessorArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorArgumentsPlaceholder);
    }

    private IReadOnlyList<string> GetSelectedResourceKinds()
    {
        var selected = new List<string>();
        AddIfChecked(selected, Texture2DCheckBox, "Texture2D");
        AddIfChecked(selected, AudioCheckBox, "Audio");
        AddIfChecked(selected, AtlasCheckBox, "Atlas");
        AddIfChecked(selected, SkelCheckBox, "Skel");
        return selected;
    }

    private static void AddIfChecked(ICollection<string> values, CheckBox checkBox, string value)
    {
        if (checkBox.IsChecked == true)
        {
            values.Add(value);
        }
    }

    private static bool HasResourceKind(ResourceProcessorCatalogEntryDto entry, string resourceKind)
    {
        return entry.ResourceKinds.Any(kind =>
            string.Equals(kind.Trim(), resourceKind, StringComparison.OrdinalIgnoreCase));
    }

    private void ClearValidationMessages()
    {
        ClearValidationMessage(ProcessorIdTextBox, ProcessorIdValidationTextBlock);
        ClearValidationMessage(ProcessorDisplayNameTextBox, ProcessorDisplayNameValidationTextBlock);
        ProcessorResourceKindsValidationTextBlock.Text = string.Empty;
        ProcessorResourceKindsValidationTextBlock.Visibility = Visibility.Collapsed;
        ClearValidationMessage(ProcessorExecutablePathTextBox, ProcessorExecutablePathValidationTextBlock);
        ClearValidationMessage(ProcessorTimeoutTextBox, ProcessorTimeoutValidationTextBlock);
        ClearValidationMessage(ProcessorArgumentsTextBox, ProcessorArgumentsValidationTextBlock);
    }

    private void ShowValidationMessage(TextBox textBox, TextBlock validationTextBlock, string message)
    {
        validationTextBlock.Text = message;
        validationTextBlock.Visibility = Visibility.Visible;
        ApplyTextBoxValidationStyle(textBox, hasValidationError: true);
    }

    private void ClearValidationMessage(TextBox textBox, TextBlock validationTextBlock)
    {
        validationTextBlock.Text = string.Empty;
        validationTextBlock.Visibility = Visibility.Collapsed;
        ApplyTextBoxValidationStyle(textBox, hasValidationError: false);
    }

    private static IReadOnlyList<string> SplitArguments(string argumentsText)
    {
        return argumentsText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static void ApplyTextBoxValidationStyle(TextBox textBox, bool hasValidationError)
    {
        textBox.BorderBrush = hasValidationError
            ? GetBrush("SystemFillColorCriticalBrush") ?? new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28))
            : GetBrush("ControlStrokeColorDefaultBrush");

        UpdateTextBoxResource(textBox, "TextControlBorderBrush", hasValidationError ? textBox.BorderBrush : null);
        UpdateTextBoxResource(textBox, "TextControlBorderBrushFocused", hasValidationError ? textBox.BorderBrush : null);
        UpdateTextBoxResource(textBox, "TextControlBorderBrushPointerOver", hasValidationError ? textBox.BorderBrush : null);
    }

    private static Brush? GetBrush(string resourceKey)
    {
        return Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as Brush
            : null;
    }

    private static void UpdateTextBoxResource(TextBox textBox, string resourceKey, object? resourceValue)
    {
        if (resourceValue is null)
        {
            if (textBox.Resources.ContainsKey(resourceKey))
            {
                textBox.Resources.Remove(resourceKey);
            }

            return;
        }

        textBox.Resources[resourceKey] = resourceValue;
    }
}
