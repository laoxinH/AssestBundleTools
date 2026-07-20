using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

public sealed partial class AssetEditorWindow
{
    private void ApplyLocalizedText()
    {
        ToolTipService.SetToolTip(
            AssetEditorGroupDragHandle,
            UiText.Get(ResourceKeys.AssetEditorWindowDragGroupHandleToolTip));
        ApplyActionButtonText(ExtractButton, ExtractButtonText, ResourceKeys.InspectorExtractButton);
        ApplyActionButtonText(SaveSpriteEditButton, SaveSpriteEditButtonText, ResourceKeys.InspectorApplySpriteEditButton);
        ApplyActionButtonText(ExitSpriteEditButton, ExitSpriteEditButtonText, ResourceKeys.InspectorExitSpriteEditButton);
        ApplyActionButtonText(UndoSpriteEditButton, UndoSpriteEditButtonText, ResourceKeys.InspectorUndoReplacementButton);
        ApplyActionButtonText(RedoSpriteEditButton, RedoSpriteEditButtonText, ResourceKeys.InspectorRedoReplacementButton);
        ApplyActionButtonText(SaveSpineEditButton, SaveSpineEditButtonText, ResourceKeys.InspectorApplySpineEditButton);
        ApplyActionButtonText(ExitSpineEditButton, ExitSpineEditButtonText, ResourceKeys.InspectorExitSpriteEditButton);
        ApplyActionButtonText(UndoSpineEditButton, UndoSpineEditButtonText, ResourceKeys.InspectorUndoReplacementButton);
        ApplyActionButtonText(RedoSpineEditButton, RedoSpineEditButtonText, ResourceKeys.InspectorRedoReplacementButton);
        ApplySaveTextEditButtonText();
        ApplyActionButtonText(ExitTextEditButton, ExitTextEditButtonText, ResourceKeys.TextAssetEditorExitButton);
    }

    private static void ApplyActionButtonText(Button button, TextBlock textBlock, string resourceKey)
    {
        var text = UiText.Get(resourceKey);
        textBlock.Text = text;
        AutomationProperties.SetName(button, text);
        ToolTipService.SetToolTip(button, text);
    }

    private void ApplySaveTextEditButtonText()
    {
        var text = UiText.Get(ViewModel.SaveTextEditButtonContentKey);
        SaveTextEditButtonText.Text = text;
        AutomationProperties.SetName(SaveTextEditButton, text);
        ToolTipService.SetToolTip(SaveTextEditButton, UiText.Get(ViewModel.SaveTextEditButtonToolTipKey));
    }

    private void UpdateWindowTitle()
    {
        Title = ViewModel.WindowTitle;
        TitleBarText.Text = Title;
    }
}
