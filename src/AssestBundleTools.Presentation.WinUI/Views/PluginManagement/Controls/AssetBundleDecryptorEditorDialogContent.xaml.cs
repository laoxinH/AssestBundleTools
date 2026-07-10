using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement.Controls;

/// <summary>
/// 承载解密器新增与编辑的表单内容。
/// </summary>
public sealed partial class AssetBundleDecryptorEditorDialogContent : UserControl
{
    private const int BasicStepIndex = 0;
    private const int ExecutionStepIndex = 1;
    private const int ArgumentsStepIndex = 2;

    private string? _previousDecryptorId;
    private int _currentStepIndex;

    public AssetBundleDecryptorEditorDialogContent()
    {
        InitializeComponent();
        ApplyLocalizedText();
        DecryptorVersionTextBox.Text = "1.0.0";
        DecryptorTimeoutTextBox.Text = "15000";
        UpdateStepState(raiseEvent: false);
    }

    public event EventHandler? StepChanged;

    public string? PreviousDecryptorId => _previousDecryptorId;

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

    public void ApplyEntry(AssetBundleDecryptorCatalogEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _previousDecryptorId = entry.Definition.Id;
        DecryptorIdTextBox.Text = entry.Definition.Id;
        DecryptorDisplayNameTextBox.Text = entry.Definition.DisplayName;
        DecryptorVersionTextBox.Text = string.IsNullOrWhiteSpace(entry.Definition.Version) ? "1.0.0" : entry.Definition.Version;
        DecryptorDescriptionTextBox.Text = entry.Definition.Description ?? string.Empty;
        DecryptorExecutablePathTextBox.Text = entry.Definition.ExecutablePath;
        DecryptorWorkingDirectoryPathTextBox.Text = entry.Definition.WorkingDirectoryPath ?? string.Empty;
        DecryptorTimeoutTextBox.Text = entry.Definition.TimeoutMs.ToString(System.Globalization.CultureInfo.CurrentCulture);
        SupportsAutoDetectionCheckBox.IsChecked = entry.Definition.SupportsAutoDetection;
        IsFallbackCheckBox.IsChecked = entry.Definition.IsFallback;
        DecryptorProbeArgumentsTextBox.Text = string.Join(Environment.NewLine, entry.Definition.ProbeArguments);
        DecryptorDecryptArgumentsTextBox.Text = string.Join(Environment.NewLine, entry.Definition.DecryptArguments);
    }

    public bool TryCreateCommand(out UpsertAssetBundleDecryptorCommand? command)
    {
        ClearValidationMessages();

        var hasValidationError = false;
        var firstInvalidStep = -1;
        var decryptorId = DecryptorIdTextBox.Text.Trim();
        var displayName = DecryptorDisplayNameTextBox.Text.Trim();
        var executablePath = DecryptorExecutablePathTextBox.Text.Trim();
        var timeoutText = DecryptorTimeoutTextBox.Text.Trim();
        var decryptArguments = SplitArguments(DecryptorDecryptArgumentsTextBox.Text);

        if (string.IsNullOrWhiteSpace(decryptorId))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                DecryptorIdTextBox,
                DecryptorIdValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                DecryptorDisplayNameTextBox,
                DecryptorDisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationDisplayNameRequired));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ExecutionStepIndex);
            ShowValidationMessage(
                DecryptorExecutablePathTextBox,
                DecryptorExecutablePathValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationExecutablePathRequired));
        }

        if (!int.TryParse(timeoutText, out var timeoutMs) || timeoutMs <= 0)
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, BasicStepIndex);
            ShowValidationMessage(
                DecryptorTimeoutTextBox,
                DecryptorTimeoutValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationTimeoutInvalid));
        }

        if (decryptArguments.Count == 0)
        {
            hasValidationError = true;
            firstInvalidStep = FirstInvalidStep(firstInvalidStep, ArgumentsStepIndex);
            ShowValidationMessage(
                DecryptorDecryptArgumentsTextBox,
                DecryptorDecryptArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationDecryptArgumentsRequired));
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

        var definition = new AssetBundleDecryptorDefinitionDto(
            Id: decryptorId,
            DisplayName: displayName,
            Version: string.IsNullOrWhiteSpace(DecryptorVersionTextBox.Text) ? "1.0.0" : DecryptorVersionTextBox.Text.Trim(),
            Description: string.IsNullOrWhiteSpace(DecryptorDescriptionTextBox.Text) ? null : DecryptorDescriptionTextBox.Text.Trim(),
            SupportsAutoDetection: SupportsAutoDetectionCheckBox.IsChecked == true,
            IsFallback: IsFallbackCheckBox.IsChecked == true,
            ExecutablePath: executablePath,
            WorkingDirectoryPath: string.IsNullOrWhiteSpace(DecryptorWorkingDirectoryPathTextBox.Text)
                ? null
                : DecryptorWorkingDirectoryPathTextBox.Text.Trim(),
            TimeoutMs: timeoutMs,
            ProbeArguments: SplitArguments(DecryptorProbeArgumentsTextBox.Text),
            DecryptArguments: decryptArguments);

        command = new UpsertAssetBundleDecryptorCommand(
            Definition: definition,
            PreviousDecryptorId: _previousDecryptorId);
        return true;
    }

    private void ApplyLocalizedText()
    {
        BasicStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepBasic);
        ExecutionStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepExecution);
        ArgumentsStepTitleText.Text = UiText.Get(ResourceKeys.PluginEditorStepArguments);
        DecryptorIdLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIdLabel);
        DecryptorIdTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIdPlaceholder);
        DecryptorIdDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIdDescription);
        DecryptorDisplayNameLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDisplayNameLabel);
        DecryptorDisplayNameTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDisplayNamePlaceholder);
        DecryptorDisplayNameDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDisplayNameDescription);
        DecryptorVersionLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorVersionLabel);
        DecryptorVersionTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorVersionPlaceholder);
        DecryptorVersionDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorVersionDescription);
        DecryptorDescriptionLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDescriptionLabel);
        DecryptorDescriptionTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDescriptionPlaceholder);
        DecryptorDescriptionHintText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDescriptionDescription);
        DecryptorExecutablePathLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorExecutablePathLabel);
        DecryptorExecutablePathTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorExecutablePathPlaceholder);
        DecryptorExecutablePathDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorExecutablePathDescription);
        DecryptorWorkingDirectoryPathLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorWorkingDirectoryPathLabel);
        DecryptorWorkingDirectoryPathTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorWorkingDirectoryPathPlaceholder);
        DecryptorWorkingDirectoryPathDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorWorkingDirectoryPathDescription);
        DecryptorTimeoutLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorTimeoutLabel);
        DecryptorTimeoutTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorTimeoutPlaceholder);
        DecryptorTimeoutDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorTimeoutDescription);
        DecryptorProbeArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsLabel);
        DecryptorProbeArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsPlaceholder);
        DecryptorProbeArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsDescription);
        DecryptorDecryptArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsLabel);
        DecryptorDecryptArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsPlaceholder);
        DecryptorDecryptArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsDescription);
        SupportsAutoDetectionLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorSupportsAutoDetectionLabel);
        SupportsAutoDetectionDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorSupportsAutoDetectionDescription);
        IsFallbackLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIsFallbackLabel);
        IsFallbackDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIsFallbackDescription);
    }

    private void ClearValidationMessages()
    {
        ClearValidationMessage(DecryptorIdTextBox, DecryptorIdValidationTextBlock);
        ClearValidationMessage(DecryptorDisplayNameTextBox, DecryptorDisplayNameValidationTextBlock);
        ClearValidationMessage(DecryptorExecutablePathTextBox, DecryptorExecutablePathValidationTextBlock);
        ClearValidationMessage(DecryptorTimeoutTextBox, DecryptorTimeoutValidationTextBlock);
        ClearValidationMessage(DecryptorDecryptArgumentsTextBox, DecryptorDecryptArgumentsValidationTextBlock);
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

        if (string.IsNullOrWhiteSpace(DecryptorIdTextBox.Text))
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorIdTextBox,
                DecryptorIdValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(DecryptorDisplayNameTextBox.Text))
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorDisplayNameTextBox,
                DecryptorDisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationDisplayNameRequired));
        }

        if (!int.TryParse(DecryptorTimeoutTextBox.Text.Trim(), out var timeoutMs) || timeoutMs <= 0)
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorTimeoutTextBox,
                DecryptorTimeoutValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationTimeoutInvalid));
        }

        return !hasValidationError;
    }

    private bool ValidateExecutionStep()
    {
        if (!string.IsNullOrWhiteSpace(DecryptorExecutablePathTextBox.Text))
        {
            return true;
        }

        ShowValidationMessage(
            DecryptorExecutablePathTextBox,
            DecryptorExecutablePathValidationTextBlock,
            UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationExecutablePathRequired));
        return false;
    }

    private bool ValidateArgumentsStep()
    {
        if (SplitArguments(DecryptorDecryptArgumentsTextBox.Text).Count > 0)
        {
            return true;
        }

        ShowValidationMessage(
            DecryptorDecryptArgumentsTextBox,
            DecryptorDecryptArgumentsValidationTextBlock,
            UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationDecryptArgumentsRequired));
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
