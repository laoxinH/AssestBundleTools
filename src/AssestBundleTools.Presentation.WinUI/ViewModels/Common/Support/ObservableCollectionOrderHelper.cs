using System.Collections.ObjectModel;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;

/// <summary>
/// 为展示层的可观察集合提供稳定的顺序回写能力。
/// 主要用于把控件内拖拽后的最终顺序安全同步回 ViewModel 集合。
/// </summary>
internal static class ObservableCollectionOrderHelper
{
    public static bool Reorder<TItem>(
        ObservableCollection<TItem> collection,
        IReadOnlyList<TItem> orderedItems)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(orderedItems);

        if (orderedItems.Count != collection.Count)
        {
            return false;
        }

        if (orderedItems.Distinct().Count() != collection.Count)
        {
            return false;
        }

        if (orderedItems.Any(item => !collection.Contains(item)))
        {
            return false;
        }

        var moved = false;
        for (var targetIndex = 0; targetIndex < orderedItems.Count; targetIndex++)
        {
            var currentIndex = collection.IndexOf(orderedItems[targetIndex]);
            if (currentIndex < 0 || currentIndex == targetIndex)
            {
                continue;
            }

            collection.Move(currentIndex, targetIndex);
            moved = true;
        }

        return moved;
    }
}
