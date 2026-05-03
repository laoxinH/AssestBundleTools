namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal readonly record struct CheckerboardTile(
    double X,
    double Y,
    double Width,
    double Height);

internal static class CheckerboardTileLayoutBuilder
{
    public static IReadOnlyList<CheckerboardTile> Build(
        double width,
        double height,
        double cellSize)
    {
        if (width <= 0d || height <= 0d)
        {
            return Array.Empty<CheckerboardTile>();
        }

        var normalizedCellSize = Math.Max(1d, cellSize);
        var columnCount = (int)Math.Ceiling(width / normalizedCellSize);
        var rowCount = (int)Math.Ceiling(height / normalizedCellSize);
        var tiles = new List<CheckerboardTile>((columnCount * rowCount) / 2);

        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                if (((row + column) & 1) == 0)
                {
                    continue;
                }

                var x = column * normalizedCellSize;
                var y = row * normalizedCellSize;
                var tileWidth = Math.Min(normalizedCellSize, width - x);
                var tileHeight = Math.Min(normalizedCellSize, height - y);
                if (tileWidth <= 0d || tileHeight <= 0d)
                {
                    continue;
                }

                tiles.Add(new CheckerboardTile(x, y, tileWidth, tileHeight));
            }
        }

        return tiles;
    }
}
