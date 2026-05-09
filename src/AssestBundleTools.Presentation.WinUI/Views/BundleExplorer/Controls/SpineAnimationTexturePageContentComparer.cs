using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class SpineAnimationTexturePageContentComparer
{
    public static bool ReferenceEqualsByEntry(
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> left,
        IReadOnlyDictionary<string, SpineAnimationTexturePageContent> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightContent) ||
                !ReferenceEquals(pair.Value.ColorContent, rightContent.ColorContent) ||
                !ReferenceEquals(pair.Value.AlphaContent, rightContent.AlphaContent))
            {
                return false;
            }
        }

        return true;
    }
}
