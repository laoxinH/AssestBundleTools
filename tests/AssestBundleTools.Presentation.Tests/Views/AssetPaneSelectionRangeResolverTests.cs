using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetPaneSelectionRangeResolverTests
{
    [Fact]
    public void ResolveToggleRange_ShouldToggleRowsBetweenAnchorAndTarget()
    {
        var first = new TestItem("first");
        var second = new TestItem("second");
        var third = new TestItem("third");
        var fourth = new TestItem("fourth");
        var fifth = new TestItem("fifth");
        var visibleItems = new[] { first, second, third, fourth, fifth };

        var mutation = AssetPaneSelectionRangeResolver.ResolveToggleRange(
            selectedItems: [second, fourth],
            visibleItems,
            anchorSelection: first,
            targetSelection: fourth,
            currentPrimarySelection: fourth);

        Assert.Collection(
            mutation.SelectedItems,
            selected => Assert.Same(third, selected));
        Assert.Same(third, mutation.PrimarySelection);
    }

    [Fact]
    public void ResolveToggleRange_ShouldPreserveTraversalDirectionForPrimarySelection()
    {
        var first = new TestItem("first");
        var second = new TestItem("second");
        var third = new TestItem("third");
        var fourth = new TestItem("fourth");
        var visibleItems = new[] { first, second, third, fourth };

        var mutation = AssetPaneSelectionRangeResolver.ResolveToggleRange(
            selectedItems: [],
            visibleItems,
            anchorSelection: fourth,
            targetSelection: second,
            currentPrimarySelection: null);

        Assert.Collection(
            mutation.SelectedItems,
            selected => Assert.Same(second, selected),
            selected => Assert.Same(third, selected));
        Assert.Same(second, mutation.PrimarySelection);
    }

    [Fact]
    public void ResolveToggleRange_ShouldNotToggleAnchorAgain_WhenTargetIsAnchor()
    {
        var first = new TestItem("first");
        var second = new TestItem("second");
        var visibleItems = new[] { first, second };

        var mutation = AssetPaneSelectionRangeResolver.ResolveToggleRange(
            selectedItems: [first, second],
            visibleItems,
            anchorSelection: second,
            targetSelection: second,
            currentPrimarySelection: second);

        Assert.Collection(
            mutation.SelectedItems,
            selected => Assert.Same(first, selected),
            selected => Assert.Same(second, selected));
        Assert.Same(second, mutation.PrimarySelection);
    }

    private sealed record TestItem(string Name);
}
