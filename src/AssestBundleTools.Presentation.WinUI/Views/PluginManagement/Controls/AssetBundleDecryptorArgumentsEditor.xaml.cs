using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.PluginManagement.Controls;

public sealed partial class AssetBundleDecryptorArgumentsEditor : UserControl
{
    public AssetBundleDecryptorArgumentsEditor()
    {
        InitializeComponent();
        ApplyLocalizedText();
    }

    public string ProbeArguments
    {
        get => ProbeArgumentsTextBox.Text;
        set => ProbeArgumentsTextBox.Text = value ?? string.Empty;
    }

    public string DecryptArguments
    {
        get => DecryptArgumentsTextBox.Text;
        set => DecryptArgumentsTextBox.Text = value ?? string.Empty;
    }

    public string EncryptArguments
    {
        get => EncryptArgumentsTextBox.Text;
        set => EncryptArgumentsTextBox.Text = value ?? string.Empty;
    }

    public void ShowDecryptValidation(string message)
    {
        DecryptArgumentsValidationTextBlock.Text = message;
        DecryptArgumentsValidationTextBlock.Visibility = Visibility.Visible;
        ApplyValidationStyle(hasValidationError: true);
    }

    public void ClearDecryptValidation()
    {
        DecryptArgumentsValidationTextBlock.Text = string.Empty;
        DecryptArgumentsValidationTextBlock.Visibility = Visibility.Collapsed;
        ApplyValidationStyle(hasValidationError: false);
    }

    private void ApplyLocalizedText()
    {
        ProbeArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsLabel);
        ProbeArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsPlaceholder);
        ProbeArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorProbeArgumentsDescription);
        DecryptArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsLabel);
        DecryptArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsPlaceholder);
        DecryptArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorDecryptArgumentsDescription);
        EncryptArgumentsLabelText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorEncryptArgumentsLabel);
        EncryptArgumentsTextBox.PlaceholderText = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorEncryptArgumentsPlaceholder);
        EncryptArgumentsDescriptionText.Text = UiText.Get(ResourceKeys.AssetBundleDecryptorEditorEncryptArgumentsDescription);
    }

    private void ApplyValidationStyle(bool hasValidationError)
    {
        var borderBrush = hasValidationError
            ? GetBrush("SystemFillColorCriticalBrush")
            : GetBrush("ControlStrokeColorDefaultBrush");
        DecryptArgumentsTextBox.BorderBrush = borderBrush;
        foreach (var resourceKey in new[]
                 {
                     "TextControlBorderBrush",
                     "TextControlBorderBrushFocused",
                     "TextControlBorderBrushPointerOver"
                 })
        {
            if (hasValidationError && borderBrush is not null)
            {
                DecryptArgumentsTextBox.Resources[resourceKey] = borderBrush;
            }
            else if (DecryptArgumentsTextBox.Resources.ContainsKey(resourceKey))
            {
                DecryptArgumentsTextBox.Resources.Remove(resourceKey);
            }
        }
    }

    private static Brush? GetBrush(string resourceKey) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var resource)
            ? resource as Brush
            : null;
}
