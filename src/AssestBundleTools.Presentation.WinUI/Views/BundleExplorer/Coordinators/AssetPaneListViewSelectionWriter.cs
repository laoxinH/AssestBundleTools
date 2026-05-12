using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetPaneListViewSelectionWriter
{
    public static T? ResolvePrimarySelection<T>(
        IReadOnlyCollection<T> selectedItems,
        T? primarySelection)
        where T : class
    {
        if (primarySelection is not null && selectedItems.Contains(primarySelection))
        {
            return primarySelection;
        }

        return selectedItems.LastOrDefault();
    }

    public static void ApplySelection<T>(
        ListView listView,
        IReadOnlyCollection<T> selectedItems,
        T? primarySelection)
        where T : class
    {
        if (listView.SelectedItems.Count == selectedItems.Count &&
            selectedItems.All(item => listView.SelectedItems.Contains(item)) &&
            ReferenceEquals(listView.SelectedItem, primarySelection))
        {
            return;
        }

        listView.SelectedItems.Clear();
        foreach (var selectedItem in selectedItems)
        {
            listView.SelectedItems.Add(selectedItem);
        }

        listView.SelectedItem = primarySelection;
    }
}
