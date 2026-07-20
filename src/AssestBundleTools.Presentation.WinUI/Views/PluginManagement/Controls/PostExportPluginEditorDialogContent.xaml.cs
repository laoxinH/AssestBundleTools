using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement.Controls;

/// <summary>
/// 承载导出后插件新增与编辑的分步表单内容。
/// </summary>
public sealed partial class PostExportPluginEditorDialogContent : UserControl
{
    private const int BasicStepIndex = 0;
    private const int ExecutionStepIndex = 1;
    private const int ArgumentsStepIndex = 2;

    private string? _previousPluginId;
    private int _currentStepIndex;

    public PostExportPluginEditorDialogContent()
    {
        InitializeComponent();
        ApplyLocalizedText();
        VersionTextBox.Text = "1.0.0";
        ArgumentsTextBox.Text = string.Join(
            Environment.NewLine,
            "--manifest",
            "{manifestPath}",
            "--output-directory",
            "{outputDirectory}");
        UpdateStepState(raiseEvent: false);
    }

    public event EventHandler? StepChanged;

    public bool CanMovePrevious => _currentStepIndex > BasicStepIndex;

    public bool IsLastStep => _currentStepIndex == ArgumentsStepIndex;

    public bool TryMoveNext()
    {
        if (IsLastStep)
        {
            return true;
        }

        if (!ValidateCurrentStep())
        {
            return false;
        }

        SetCurrentStep(_currentStepIndex + 1);
        return true;
    }

    public void MovePrevious()
    {
        if (CanMovePrevious)
        {
            SetCurrentStep(_currentStepIndex - 1);
        }
    }

    public void ApplyEntry(PostExportPluginCatalogEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _previousPluginId = entry.Id;
        IdTextBox.Text = entry.Id;
        DisplayNameTextBox.Text = entry.DisplayName;
        VersionTextBox.Text = string.IsNullOrWhiteSpace(entry.Version) ? "1.0.0" : entry.Version;
        DescriptionTextBox.Text = entry.Description ?? string.Empty;
        ExecutablePathTextBox.Text = entry.ExecutablePath;
        WorkingDirectoryTextBox.Text = entry.WorkingDirectoryPath ?? string.Empty;
        ArgumentsTextBox.Text = string.Join(Environment.NewLine, entry.ProcessArguments);
    }

    public bool TryCreateCommand(out UpsertPostExportPluginCommand? command)
    {
        ClearValidationMessages();

        var hasValidationError = false;
        var firstInvalidStep = -1;
        var pluginId = IdTextBox.Text.Trim();
        var displayName = DisplayNameTextBox.Text.Trim();
        var executablePath = ExecutablePathTextBox.Text.Trim();
        var processArguments = SplitArguments(ArgumentsTextBox.Text);

        if (string.IsNullOrWhiteSpace(pluginId))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                IdTextBox,
                IdValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                DisplayNameTextBox,
                DisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationDisplayNameRequired));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ExecutionStepIndex);
            ShowValidationMessage(
                ExecutablePathTextBox,
                ExecutablePathValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationExecutablePathRequired));
        }

        if (processArguments.Count == 0)
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ArgumentsStepIndex);
            ShowValidationMessage(
                ArgumentsTextBox,
                ArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationArgumentsRequired));
        }
        else if (!ContainsManifestPathArgument(processArguments))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ArgumentsStepIndex);
            ShowValidationMessage(
                ArgumentsTextBox,
                ArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationManifestPathRequired));
        }

        if (hasValidationError)
        {
            if (firstInvalidStep >= 0)
            {
                SetCurrentStep(firstInvalidStep);
            }

            command = null;
            return false;
        }

        command = new UpsertPostExportPluginCommand(
            new PostExportPluginDefinitionDto(
                pluginId,
                displayName,
                string.IsNullOrWhiteSpace(VersionTextBox.Text) ? "1.0.0" : VersionTextBox.Text.Trim(),
                string.IsNullOrWhiteSpace(DescriptionTextBox.Text) ? null : DescriptionTextBox.Text.Trim(),
                executablePath,
                string.IsNullOrWhiteSpace(WorkingDirectoryTextBox.Text) ? null : WorkingDirectoryTextBox.Text.Trim(),
                processArguments),
            _previousPluginId);
        return true;
    }

    private void ApplyLocalizedText()
    {
        BasicStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepBasic);
        ExecutionStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepExecution);
        ArgumentsStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepArguments);
        IdLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorIdLabel);
        IdDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorIdDescription);
        IdTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorIdPlaceholder);
        DisplayNameLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorDisplayNameLabel);
        DisplayNameDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorDisplayNameDescription);
        DisplayNameTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorDisplayNamePlaceholder);
        VersionLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorVersionLabel);
        VersionDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorVersionDescription);
        VersionTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorVersionPlaceholder);
        DescriptionLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorDescriptionLabel);
        DescriptionHintText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorDescriptionDescription);
        DescriptionTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorDescriptionPlaceholder);
        ExecutablePathLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorExecutablePathLabel);
        ExecutablePathDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorExecutablePathDescription);
        ExecutablePathTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorExecutablePathPlaceholder);
        WorkingDirectoryLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorWorkingDirectoryPathLabel);
        WorkingDirectoryDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorWorkingDirectoryPathDescription);
        WorkingDirectoryTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorWorkingDirectoryPathPlaceholder);
        ArgumentsLabelText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorArgumentsLabel);
        ArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.PostExportPluginEditorArgumentsDescription);
        ArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.PostExportPluginEditorArgumentsPlaceholder);
    }

    private bool ValidateCurrentStep()
    {
        ClearValidationMessages();

        return _currentStepIndex switch
        {
            BasicStepIndex => ValidateBasicStep(),
            ExecutionStepIndex => ValidateExecutionStep(),
            ArgumentsStepIndex => ValidateArgumentsStep(),
            _ => true
        };
    }

    private bool ValidateBasicStep()
    {
        var isValid = true;
        if (string.IsNullOrWhiteSpace(IdTextBox.Text))
        {
            isValid = false;
            ShowValidationMessage(
                IdTextBox,
                IdValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            isValid = false;
            ShowValidationMessage(
                DisplayNameTextBox,
                DisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationDisplayNameRequired));
        }

        return isValid;
    }

    private bool ValidateExecutionStep()
    {
        if (!string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text))
        {
            return true;
        }

        ShowValidationMessage(
            ExecutablePathTextBox,
            ExecutablePathValidationTextBlock,
            UiText.Get(ResourceKeys.PostExportPluginEditorValidationExecutablePathRequired));
        return false;
    }

    private bool ValidateArgumentsStep()
    {
        var arguments = SplitArguments(ArgumentsTextBox.Text);
        if (arguments.Count == 0)
        {
            ShowValidationMessage(
                ArgumentsTextBox,
                ArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.PostExportPluginEditorValidationArgumentsRequired));
            return false;
        }

        if (ContainsManifestPathArgument(arguments))
        {
            return true;
        }

        ShowValidationMessage(
            ArgumentsTextBox,
            ArgumentsValidationTextBlock,
            UiText.Get(ResourceKeys.PostExportPluginEditorValidationManifestPathRequired));
        return false;
    }

    private void SetCurrentStep(int stepIndex)
    {
        _currentStepIndex = Math.Clamp(stepIndex, BasicStepIndex, ArgumentsStepIndex);
        UpdateStepState(raiseEvent: true);
    }

    private void UpdateStepState(bool raiseEvent)
    {
        BasicStepPanel.Visibility = _currentStepIndex == BasicStepIndex ? Visibility.Visible : Visibility.Collapsed;
        ExecutionStepPanel.Visibility = _currentStepIndex == ExecutionStepIndex ? Visibility.Visible : Visibility.Collapsed;
        ArgumentsStepPanel.Visibility = _currentStepIndex == ArgumentsStepIndex ? Visibility.Visible : Visibility.Collapsed;

        UpdateStepIndicator(BasicStepIndex, BasicStepIndicatorBorder, BasicStepNumberText, BasicStepTitleText);
        UpdateStepIndicator(ExecutionStepIndex, ExecutionStepIndicatorBorder, ExecutionStepNumberText, ExecutionStepTitleText);
        UpdateStepIndicator(ArgumentsStepIndex, ArgumentsStepIndicatorBorder, ArgumentsStepNumberText, ArgumentsStepTitleText);
        FormScrollViewer.ChangeView(null, 0, null);

        if (raiseEvent)
        {
            StepChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateStepIndicator(int stepIndex, Border border, TextBlock numberText, TextBlock titleText)
    {
        var isActive = _currentStepIndex == stepIndex;
        var accentBrush = GetBrush("AccentFillColorDefaultBrush") ?? new SolidColorBrush(ColorHelper.FromArgb(255, 209, 18, 38));
        border.BorderBrush = isActive ? accentBrush : GetBrush("CardStrokeColorDefaultBrush");
        border.Background = isActive
            ? GetBrush("SubtleFillColorSecondaryBrush")
            : GetBrush("CardBackgroundFillColorDefaultBrush");
        numberText.Foreground = isActive ? accentBrush : GetBrush("TextFillColorSecondaryBrush");
        titleText.Foreground = isActive ? GetBrush("TextFillColorPrimaryBrush") : GetBrush("TextFillColorSecondaryBrush");
    }

    private void ClearValidationMessages()
    {
        ClearValidationMessage(IdTextBox, IdValidationTextBlock);
        ClearValidationMessage(DisplayNameTextBox, DisplayNameValidationTextBlock);
        ClearValidationMessage(ExecutablePathTextBox, ExecutablePathValidationTextBlock);
        ClearValidationMessage(ArgumentsTextBox, ArgumentsValidationTextBlock);
    }

    private static int FirstInvalidStep(int currentStep, int candidateStep)
    {
        return currentStep < 0 ? candidateStep : Math.Min(currentStep, candidateStep);
    }

    private static bool ContainsManifestPathArgument(IReadOnlyList<string> arguments)
    {
        return arguments.Any(static argument => argument.Contains("{manifestPath}", StringComparison.Ordinal));
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
