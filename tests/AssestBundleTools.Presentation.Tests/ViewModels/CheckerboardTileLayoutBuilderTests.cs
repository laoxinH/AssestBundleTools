using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class CheckerboardTileLayoutBuilderTests
{
    [Fact]
    public void Build_ShouldCreateFixedSizeAlternatingDarkTiles()
    {
        var tiles = CheckerboardTileLayoutBuilder.Build(25d, 25d, 10d);

        Assert.Equal(4, tiles.Count);
        Assert.Equal(new CheckerboardTile(10d, 0d, 10d, 10d), tiles[0]);
        Assert.Equal(new CheckerboardTile(0d, 10d, 10d, 10d), tiles[1]);
        Assert.Equal(new CheckerboardTile(20d, 10d, 5d, 10d), tiles[2]);
        Assert.Equal(new CheckerboardTile(10d, 20d, 10d, 5d), tiles[3]);
    }

    [Fact]
    public void Build_ShouldReturnEmpty_WhenViewportHasNoArea()
    {
        Assert.Empty(CheckerboardTileLayoutBuilder.Build(0d, 25d, 10d));
        Assert.Empty(CheckerboardTileLayoutBuilder.Build(25d, 0d, 10d));
    }
}
