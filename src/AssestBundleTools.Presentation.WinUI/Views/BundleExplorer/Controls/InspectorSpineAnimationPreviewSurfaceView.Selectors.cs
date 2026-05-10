using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorSpineAnimationPreviewSurfaceView
{
    private void ReconcileSelectorSelections()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        SelectStringItem(AnimationComboBox, viewModel.SelectedAnimationName);
        SelectStringItem(SkinComboBox, viewModel.SelectedSkinName);
        SelectPlaybackSpeedItem(PlaybackSpeedComboBox, viewModel.PlaybackSpeed);
    }

    private static void SelectStringItem(
        ComboBox comboBox,
        string? selectedName)
    {
        if (string.IsNullOrWhiteSpace(selectedName))
        {
            if (comboBox.SelectedItem is not null)
            {
                comboBox.SelectedItem = null;
            }

            return;
        }

        var item = comboBox.Items
            .OfType<string>()
            .FirstOrDefault(candidate =>
                string.Equals(candidate, selectedName.Trim(), StringComparison.Ordinal));
        if (item is null ||
            string.Equals(comboBox.SelectedItem as string, item, StringComparison.Ordinal))
        {
            return;
        }

        comboBox.SelectedItem = item;
    }

    private static void SelectPlaybackSpeedItem(
        ComboBox comboBox,
        double playbackSpeed)
    {
        double? item = null;
        foreach (var candidate in comboBox.Items.OfType<double>())
        {
            if (AreClose(candidate, playbackSpeed))
            {
                item = candidate;
                break;
            }
        }

        if (!item.HasValue ||
            comboBox.SelectedItem is double selectedItem &&
            AreClose(selectedItem, item.Value))
        {
            return;
        }

        comboBox.SelectedItem = item.Value;
    }

    private static bool AreClose(double left, double right) =>
        Math.Abs(left - right) < 0.0000001d;
}
