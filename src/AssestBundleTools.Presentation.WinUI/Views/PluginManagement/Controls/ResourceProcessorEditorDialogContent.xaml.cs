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
    private const int BasicStepIndex = 0;
    private const int ExecutionStepIndex = 1;
    private const int ArgumentsStepIndex = 2;

    private string? _previousProcessorId;
    private int _currentStepIndex;

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
        UpdateStepState(raiseEvent: false);
    }

    public event EventHandler? StepChanged;

    public string? PreviousProcessorId => _previousProcessorId;

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
        if (!CanMovePrevious)
        {
            return;
        }

        SetCurrentStep(_currentStepIndex - 1);
    }

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
        var firstInvalidStep = -1;
        var processorId = ProcessorIdTextBox.Text.Trim();
        var displayName = ProcessorDisplayNameTextBox.Text.Trim();
        var executablePath = ProcessorExecutablePathTextBox.Text.Trim();
        var timeoutText = ProcessorTimeoutTextBox.Text.Trim();
        var resourceKinds = GetSelectedResourceKinds();
        var processArguments = SplitArguments(ProcessorArgumentsTextBox.Text);

        if (string.IsNullOrWhiteSpace(processorId))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                ProcessorIdTextBox,
                ProcessorIdValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                ProcessorDisplayNameTextBox,
                ProcessorDisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationDisplayNameRequired));
        }

        if (resourceKinds.Count == 0)
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ExecutionStepIndex);
            ProcessorResourceKindsValidationTextBlock.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorValidationResourceKindsRequired);
            ProcessorResourceKindsValidationTextBlock.Visibility = Visibility.Visible;
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ExecutionStepIndex);
            ShowValidationMessage(
                ProcessorExecutablePathTextBox,
                ProcessorExecutablePathValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationExecutablePathRequired));
        }

        if (!int.TryParse(timeoutText, out var timeoutMs) || timeoutMs <= 0)
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                ProcessorTimeoutTextBox,
                ProcessorTimeoutValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationTimeoutInvalid));
        }

        if (processArguments.Count == 0)
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ArgumentsStepIndex);
            ShowValidationMessage(
                ProcessorArgumentsTextBox,
                ProcessorArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationArgumentsRequired));
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
        BasicStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepBasic);
        ExecutionStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepExecution);
        ArgumentsStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepArguments);
        ProcessorIdLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorIdLabel);
        ProcessorIdTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorIdPlaceholder);
        ProcessorIdDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorIdDescription);
        ProcessorDisplayNameLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDisplayNameLabel);
        ProcessorDisplayNameTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorDisplayNamePlaceholder);
        ProcessorDisplayNameDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDisplayNameDescription);
        ProcessorVersionLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorVersionLabel);
        ProcessorVersionTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorVersionPlaceholder);
        ProcessorVersionDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorVersionDescription);
        ProcessorDescriptionLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDescriptionLabel);
        ProcessorDescriptionTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorDescriptionPlaceholder);
        ProcessorDescriptionHintText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorDescriptionDescription);
        ProcessorResourceKindsLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorResourceKindsLabel);
        ProcessorResourceKindsDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorResourceKindsDescription);
        Texture2DCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D);
        AudioCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio);
        AtlasCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas);
        SkelCheckBox.Content = UiText.Get(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel);
        ProcessorExecutablePathLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorExecutablePathLabel);
        ProcessorExecutablePathTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorExecutablePathPlaceholder);
        ProcessorExecutablePathDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorExecutablePathDescription);
        ProcessorWorkingDirectoryPathLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorWorkingDirectoryPathLabel);
        ProcessorWorkingDirectoryPathTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorWorkingDirectoryPathPlaceholder);
        ProcessorWorkingDirectoryPathDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorWorkingDirectoryPathDescription);
        ProcessorTimeoutLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorTimeoutLabel);
        ProcessorTimeoutTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorTimeoutPlaceholder);
        ProcessorTimeoutDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorTimeoutDescription);
        ProcessorArgumentsLabelText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorArgumentsLabel);
        ProcessorArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.ResourceProcessorEditorArgumentsPlaceholder);
        ProcessorArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorArgumentsDescription);
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
        var hasValidationError = false;

        if (string.IsNullOrWhiteSpace(ProcessorIdTextBox.Text))
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorIdTextBox,
                ProcessorIdValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(ProcessorDisplayNameTextBox.Text))
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorDisplayNameTextBox,
                ProcessorDisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationDisplayNameRequired));
        }

        if (!int.TryParse(ProcessorTimeoutTextBox.Text.Trim(), out var timeoutMs) || timeoutMs <= 0)
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorTimeoutTextBox,
                ProcessorTimeoutValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationTimeoutInvalid));
        }

        return !hasValidationError;
    }

    private bool ValidateExecutionStep()
    {
        var hasValidationError = false;

        if (GetSelectedResourceKinds().Count == 0)
        {
            hasValidationError = true;
            ProcessorResourceKindsValidationTextBlock.Text = UiText.Get(ResourceKeys.ResourceProcessorEditorValidationResourceKindsRequired);
            ProcessorResourceKindsValidationTextBlock.Visibility = Visibility.Visible;
        }

        if (string.IsNullOrWhiteSpace(ProcessorExecutablePathTextBox.Text))
        {
            hasValidationError = true;
            ShowValidationMessage(
                ProcessorExecutablePathTextBox,
                ProcessorExecutablePathValidationTextBlock,
                UiText.Get(ResourceKeys.ResourceProcessorEditorValidationExecutablePathRequired));
        }

        return !hasValidationError;
    }

    private bool ValidateArgumentsStep()
    {
        if (SplitArguments(ProcessorArgumentsTextBox.Text).Count > 0)
        {
            return true;
        }

        ShowValidationMessage(
            ProcessorArgumentsTextBox,
            ProcessorArgumentsValidationTextBlock,
            UiText.Get(ResourceKeys.ResourceProcessorEditorValidationArgumentsRequired));
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

    private static int FirstInvalidStep(int currentStep, int candidateStep)
    {
        return currentStep < 0 ? candidateStep : Math.Min(currentStep, candidateStep);
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
