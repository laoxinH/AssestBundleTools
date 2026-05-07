using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetEditorWindowRestoreDialogContentBuilder
{
    public static UIElement Create(AssetEditorWindowRestorePromptViewModel restorePrompt)
    {
        ArgumentNullException.ThrowIfNull(restorePrompt);

        var panel = new StackPanel
        {
            Spacing = 12
        };
        panel.Children.Add(
            new TextBlock
            {
                Text = restorePrompt.RestoreDialogDescription,
                TextWrapping = TextWrapping.WrapWholeWords
            });

        if (restorePrompt.HasDraftRestoreOption)
        {
            var restoreDraftMarkersCheckBox = new CheckBox
            {
                Content = new TextBlock
                {
                    Text = restorePrompt.RestoreDraftMarkersOptionText,
                    TextWrapping = TextWrapping.WrapWholeWords
                },
                IsChecked = restorePrompt.RestoreDraftMarkers
            };
            restoreDraftMarkersCheckBox.Checked += (_, _) => restorePrompt.SetRestoreDraftMarkers(true);
            restoreDraftMarkersCheckBox.Unchecked += (_, _) => restorePrompt.SetRestoreDraftMarkers(false);
            panel.Children.Add(restoreDraftMarkersCheckBox);
        }

        if (restorePrompt.HasDraftRestoreDiagnostics)
        {
            panel.Children.Add(
                new TextBlock
                {
                    Text = restorePrompt.DraftRestoreDiagnosticsText,
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Opacity = 0.8
                });
        }

        return panel;
    }
}
