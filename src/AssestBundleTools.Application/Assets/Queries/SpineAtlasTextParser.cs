using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal static class SpineAtlasTextParser
{
    public static IReadOnlyList<SpineAtlasPageDto> Parse(string? atlasText)
    {
        if (string.IsNullOrWhiteSpace(atlasText))
        {
            return Array.Empty<SpineAtlasPageDto>();
        }

        var pageBuilders = new List<PageBuilder>();
        PageBuilder? currentPage = null;
        RegionBuilder? currentRegion = null;
        var expectingPage = true;

        foreach (var rawLine in EnumerateLines(atlasText))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                currentRegion = null;
                expectingPage = true;
                continue;
            }

            if (TrySplitKeyValue(line, out var key, out var value))
            {
                ApplyKeyValue(currentPage, currentRegion, key, value);
                continue;
            }

            if (expectingPage || currentPage is null)
            {
                currentPage = new PageBuilder(line);
                pageBuilders.Add(currentPage);
                currentRegion = null;
                expectingPage = false;
                continue;
            }

            currentRegion = new RegionBuilder(line, currentPage.Name);
            currentPage.Regions.Add(currentRegion);
        }

        return pageBuilders
            .Select(static page => page.Build())
            .ToArray();
    }

    private static void ApplyKeyValue(
        PageBuilder? currentPage,
        RegionBuilder? currentRegion,
        string key,
        string value)
    {
        if (currentRegion is not null)
        {
            ApplyRegionKeyValue(currentRegion, key, value);
            return;
        }

        if (currentPage is not null &&
            key.Equals("size", StringComparison.OrdinalIgnoreCase) &&
            TryParseIntPair(value, out var width, out var height))
        {
            currentPage.Width = width;
            currentPage.Height = height;
        }
    }

    private static void ApplyRegionKeyValue(RegionBuilder region, string key, string value)
    {
        if (key.Equals("xy", StringComparison.OrdinalIgnoreCase) &&
            TryParseIntPair(value, out var x, out var y))
        {
            region.X = x;
            region.Y = y;
            return;
        }

        if (key.Equals("size", StringComparison.OrdinalIgnoreCase) &&
            TryParseIntPair(value, out var width, out var height))
        {
            region.Width = width;
            region.Height = height;
            return;
        }

        if (key.Equals("bounds", StringComparison.OrdinalIgnoreCase) &&
            TryParseIntQuad(value, out var boundsX, out var boundsY, out var boundsWidth, out var boundsHeight))
        {
            region.X = boundsX;
            region.Y = boundsY;
            region.Width = boundsWidth;
            region.Height = boundsHeight;
            return;
        }

        if (key.Equals("rotate", StringComparison.OrdinalIgnoreCase))
        {
            region.IsRotated = ResolveRotateValue(value);
        }
    }

    private static IEnumerable<string> EnumerateLines(string value)
    {
        using var reader = new StringReader(value);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static bool TrySplitKeyValue(
        string line,
        out string key,
        out string value)
    {
        var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = line[..separatorIndex].Trim();
        value = line[(separatorIndex + 1)..].Trim();
        return key.Length > 0;
    }

    private static bool TryParseIntPair(
        string value,
        out int first,
        out int second)
    {
        var values = SplitNumbers(value);
        if (values.Length < 2)
        {
            first = 0;
            second = 0;
            return false;
        }

        first = values[0];
        second = values[1];
        return true;
    }

    private static bool TryParseIntQuad(
        string value,
        out int first,
        out int second,
        out int third,
        out int fourth)
    {
        var values = SplitNumbers(value);
        if (values.Length < 4)
        {
            first = 0;
            second = 0;
            third = 0;
            fourth = 0;
            return false;
        }

        first = values[0];
        second = values[1];
        third = values[2];
        fourth = values[3];
        return true;
    }

    private static int[] SplitNumbers(string value)
    {
        return value
            .Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => int.TryParse(part, out var parsed) ? parsed : (int?)null)
            .Where(static parsed => parsed.HasValue)
            .Select(static parsed => parsed!.Value)
            .ToArray();
    }

    private static bool ResolveRotateValue(string value)
    {
        if (bool.TryParse(value, out var parsedBoolean))
        {
            return parsedBoolean;
        }

        return int.TryParse(value, out var parsedInt) && parsedInt != 0;
    }

    private sealed class PageBuilder
    {
        public PageBuilder(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public List<RegionBuilder> Regions { get; } = [];

        public SpineAtlasPageDto Build() =>
            new(
                Name,
                Width,
                Height,
                Regions.Select(static region => region.Build()).ToArray());
    }

    private sealed class RegionBuilder
    {
        public RegionBuilder(string name, string pageName)
        {
            Name = name;
            PageName = pageName;
        }

        public string Name { get; }

        public string PageName { get; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public bool IsRotated { get; set; }

        public SpineAtlasRegionDto Build() =>
            new(Name, PageName, X, Y, Width, Height, IsRotated);
    }
}
