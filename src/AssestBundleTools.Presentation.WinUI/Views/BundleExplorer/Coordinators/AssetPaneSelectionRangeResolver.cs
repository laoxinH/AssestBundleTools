namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal static class AssetPaneSelectionRangeResolver
{
    public static AssetPaneSelectionRangeMutation<T> Resolve<T>(
        IEnumerable<T> selectedItems,
        IReadOnlyList<T> visibleItems,
        T anchorSelection,
        T targetSelection,
        bool shouldSelect,
        T? currentPrimarySelection)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        ArgumentNullException.ThrowIfNull(visibleItems);
        ArgumentNullException.ThrowIfNull(anchorSelection);
        ArgumentNullException.ThrowIfNull(targetSelection);

        var visibleSet = visibleItems.ToHashSet();
        var selectedSet = selectedItems
            .Where(visibleSet.Contains)
            .ToHashSet();

        foreach (var item in ResolveRange(visibleItems, anchorSelection, targetSelection))
        {
            if (shouldSelect)
            {
                selectedSet.Add(item);
                continue;
            }

            selectedSet.Remove(item);
        }

        var nextSelectedItems = visibleItems
            .Where(selectedSet.Contains)
            .ToArray();
        var primarySelection = ResolvePrimarySelection(
            nextSelectedItems,
            shouldSelect ? targetSelection : currentPrimarySelection);

        return new AssetPaneSelectionRangeMutation<T>(nextSelectedItems, primarySelection);
    }

    public static AssetPaneSelectionRangeMutation<T> ResolveToggleRange<T>(
        IEnumerable<T> selectedItems,
        IReadOnlyList<T> visibleItems,
        T anchorSelection,
        T targetSelection,
        T? currentPrimarySelection)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(selectedItems);
        ArgumentNullException.ThrowIfNull(visibleItems);
        ArgumentNullException.ThrowIfNull(anchorSelection);
        ArgumentNullException.ThrowIfNull(targetSelection);

        var visibleSet = visibleItems.ToHashSet();
        var selectedSet = selectedItems
            .Where(visibleSet.Contains)
            .ToHashSet();
        T? preferredSelection = currentPrimarySelection;

        foreach (var item in ResolveRange(visibleItems, anchorSelection, targetSelection, includeAnchor: false))
        {
            if (selectedSet.Remove(item))
            {
                if (ReferenceEquals(preferredSelection, item) ||
                    EqualityComparer<T>.Default.Equals(preferredSelection, item))
                {
                    preferredSelection = null;
                }

                continue;
            }

            selectedSet.Add(item);
            preferredSelection = item;
        }

        var nextSelectedItems = visibleItems
            .Where(selectedSet.Contains)
            .ToArray();
        var primarySelection = ResolvePrimarySelection(nextSelectedItems, preferredSelection);

        return new AssetPaneSelectionRangeMutation<T>(nextSelectedItems, primarySelection);
    }

    private static IEnumerable<T> ResolveRange<T>(
        IReadOnlyList<T> visibleItems,
        T anchorSelection,
        T targetSelection,
        bool includeAnchor = true)
        where T : class
    {
        var anchorIndex = IndexOf(visibleItems, anchorSelection);
        var targetIndex = IndexOf(visibleItems, targetSelection);
        if (targetIndex < 0)
        {
            yield break;
        }

        if (anchorIndex < 0)
        {
            yield return targetSelection;
            yield break;
        }

        if (anchorIndex == targetIndex)
        {
            if (includeAnchor)
            {
                yield return targetSelection;
            }

            yield break;
        }

        var step = targetIndex > anchorIndex ? 1 : -1;
        var index = includeAnchor ? anchorIndex : anchorIndex + step;
        while (true)
        {
            yield return visibleItems[index];
            if (index == targetIndex)
            {
                yield break;
            }

            index += step;
        }
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, T target)
        where T : class
    {
        for (var index = 0; index < items.Count; index++)
        {
            if (ReferenceEquals(items[index], target) ||
                EqualityComparer<T>.Default.Equals(items[index], target))
            {
                return index;
            }
        }

        return -1;
    }

    private static T? ResolvePrimarySelection<T>(
        IReadOnlyCollection<T> selectedItems,
        T? preferredSelection)
        where T : class
    {
        if (preferredSelection is not null && selectedItems.Contains(preferredSelection))
        {
            return preferredSelection;
        }

        return selectedItems.LastOrDefault();
    }
}

internal sealed record AssetPaneSelectionRangeMutation<T>(
    IReadOnlyCollection<T> SelectedItems,
    T? PrimarySelection)
    where T : class;
