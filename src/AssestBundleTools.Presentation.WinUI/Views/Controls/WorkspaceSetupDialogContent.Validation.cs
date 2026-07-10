using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.Views.Controls;

public sealed partial class WorkspaceSetupDialogContent
{
    private void SetDataRootValidationMessage(string validationMessage, bool announce)
    {
        ApplyFieldValidationState(DataRootPathTextBox, DataRootValidationTextBlock, validationMessage);

        if (!announce)
        {
            return;
        }

        FocusValidationTarget(DataRootPathTextBox);
        PlayValidationFeedback();
    }

    private void ShowWorkspaceNameValidationMessage(
        string validationMessage,
        bool announce,
        string? placeholderResourceKey = null)
    {
        ApplyFieldValidationState(WorkspaceDisplayNameTextBox, WorkspaceNameValidationTextBlock, validationMessage);

        if (!string.IsNullOrWhiteSpace(placeholderResourceKey))
        {
            WorkspaceDisplayNameTextBox.PlaceholderText = UiText.Get(placeholderResourceKey);
        }

        if (!announce)
        {
            return;
        }

        FocusValidationTarget(_dialogMode == WorkspaceDialogMode.Open
            ? DataRootPathTextBox
            : WorkspaceDisplayNameTextBox);
        PlayValidationFeedback();
    }

    private void ClearDataRootValidationMessage()
    {
        ApplyFieldValidationState(DataRootPathTextBox, DataRootValidationTextBlock, null);
    }

    private void ClearWorkspaceNameValidationMessage()
    {
        ApplyFieldValidationState(WorkspaceDisplayNameTextBox, WorkspaceNameValidationTextBlock, null);
        RestoreWorkspaceNamePlaceholder();
    }

    private void ApplyFieldValidationState(
        TextBox textBox,
        TextBlock validationTextBlock,
        string? validationMessage)
    {
        var hasValidationError = !string.IsNullOrWhiteSpace(validationMessage);
        validationTextBlock.Text = validationMessage ?? string.Empty;
        validationTextBlock.Visibility = hasValidationError
            ? Visibility.Visible
            : Visibility.Collapsed;

        ApplyTextBoxValidationStyle(textBox, hasValidationError);
    }

    private static void ApplyTextBoxValidationStyle(TextBox textBox, bool hasValidationError)
    {
        textBox.BorderBrush = hasValidationError
            ? GetBrush("SystemFillColorCriticalBrush") ?? new SolidColorBrush(ColorHelper.FromArgb(255, 196, 43, 28))
            : GetBrush("ControlStrokeColorDefaultBrush");

        UpdateTextBoxResource(textBox, "TextControlBorderBrush", hasValidationError
            ? textBox.BorderBrush
            : null);
        UpdateTextBoxResource(textBox, "TextControlBorderBrushFocused", hasValidationError
            ? textBox.BorderBrush
            : null);
        UpdateTextBoxResource(textBox, "TextControlBorderBrushPointerOver", hasValidationError
            ? textBox.BorderBrush
            : null);
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

    private void RestoreWorkspaceNamePlaceholder()
    {
        WorkspaceDisplayNameTextBox.PlaceholderText = UiText.Get(
            _dialogMode == WorkspaceDialogMode.New
                ? ResourceKeys.WorkspaceDialogSetupWorkspaceNamePlaceholder
                : ResourceKeys.WorkspaceDialogSetupWorkspaceNameReadOnlyPlaceholder);
    }

    private void FocusValidationTarget(Control focusTarget)
    {
        FormScrollViewer.ChangeView(null, 0d, null, disableAnimation: false);
        _ = focusTarget.Focus(FocusState.Programmatic);
    }

    private void PlayValidationFeedback() =>
        _appInteractionFeedbackService.Play(AppInteractionFeedbackKind.ValidationError);
}
