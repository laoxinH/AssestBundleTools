using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.SettingsDiagnostics.Controls;

/// <summary>
/// 承载解密器新增与编辑的表单内容。
/// </summary>
public sealed partial class AssetBundleDecryptorEditorDialogContent : UserControl
{
    private string? _previousDecryptorId;

    public AssetBundleDecryptorEditorDialogContent()
    {
        InitializeComponent();
        ApplyLocalizedText();
        DecryptorVersionTextBox.Text = "1.0.0";
        DecryptorTimeoutTextBox.Text = "15000";
    }

    public string? PreviousDecryptorId => _previousDecryptorId;

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
        var decryptorId = DecryptorIdTextBox.Text.Trim();
        var displayName = DecryptorDisplayNameTextBox.Text.Trim();
        var executablePath = DecryptorExecutablePathTextBox.Text.Trim();
        var timeoutText = DecryptorTimeoutTextBox.Text.Trim();
        var decryptArguments = SplitArguments(DecryptorDecryptArgumentsTextBox.Text);

        if (string.IsNullOrWhiteSpace(decryptorId))
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorIdTextBox,
                DecryptorIdValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationIdRequired));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorDisplayNameTextBox,
                DecryptorDisplayNameValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationDisplayNameRequired));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorExecutablePathTextBox,
                DecryptorExecutablePathValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationExecutablePathRequired));
        }

        if (!int.TryParse(timeoutText, out var timeoutMs) || timeoutMs <= 0)
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorTimeoutTextBox,
                DecryptorTimeoutValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationTimeoutInvalid));
        }

        if (decryptArguments.Count == 0)
        {
            hasValidationError = true;
            ShowValidationMessage(
                DecryptorDecryptArgumentsTextBox,
                DecryptorDecryptArgumentsValidationTextBlock,
                UiText.Get(ResourceKeys.AssetBundleDecryptorEditorValidationDecryptArgumentsRequired));
        }

        if (hasValidationError)
        {
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
        DecryptorIdLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIdLabel);
        DecryptorIdTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIdPlaceholder);
        DecryptorDisplayNameLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDisplayNameLabel);
        DecryptorDisplayNameTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDisplayNamePlaceholder);
        DecryptorVersionLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorVersionLabel);
        DecryptorVersionTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorVersionPlaceholder);
        DecryptorDescriptionLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDescriptionLabel);
        DecryptorDescriptionTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDescriptionPlaceholder);
        DecryptorExecutablePathLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorExecutablePathLabel);
        DecryptorExecutablePathTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorExecutablePathPlaceholder);
        DecryptorWorkingDirectoryPathLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorWorkingDirectoryPathLabel);
        DecryptorWorkingDirectoryPathTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorWorkingDirectoryPathPlaceholder);
        DecryptorTimeoutLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorTimeoutLabel);
        DecryptorTimeoutTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorTimeoutPlaceholder);
        DecryptorProbeArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsLabel);
        DecryptorProbeArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsPlaceholder);
        DecryptorDecryptArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsLabel);
        DecryptorDecryptArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsPlaceholder);
        SupportsAutoDetectionCheckBox.Content = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorSupportsAutoDetectionLabel);
        IsFallbackCheckBox.Content = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorIsFallbackLabel);
    }

    private void ClearValidationMessages()
    {
        ClearValidationMessage(DecryptorIdTextBox, DecryptorIdValidationTextBlock);
        ClearValidationMessage(DecryptorDisplayNameTextBox, DecryptorDisplayNameValidationTextBlock);
        ClearValidationMessage(DecryptorExecutablePathTextBox, DecryptorExecutablePathValidationTextBlock);
        ClearValidationMessage(DecryptorTimeoutTextBox, DecryptorTimeoutValidationTextBlock);
        ClearValidationMessage(DecryptorDecryptArgumentsTextBox, DecryptorDecryptArgumentsValidationTextBlock);
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
