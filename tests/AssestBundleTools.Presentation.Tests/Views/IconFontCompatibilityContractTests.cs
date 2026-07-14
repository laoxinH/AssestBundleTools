using System.Text.Json;
using System.Text.RegularExpressions;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class IconFontCompatibilityContractTests
{
    [Fact]
    public void FontIcons_ShouldUsePackagedFluentIconFont()
    {
        var repositoryRoot = FindRepositoryRoot();
        var presentationRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var appXaml = File.ReadAllText(Path.Combine(presentationRoot, "App.xaml"));
        var sourceFiles = Directory.EnumerateFiles(presentationRoot, "*.*", SearchOption.AllDirectories)
            .Where(path =>
                (string.Equals(Path.GetExtension(path), ".xaml", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase)) &&
                !IsBuildOutputPath(path, presentationRoot))
            .ToArray();
        var allSource = string.Join(
            Environment.NewLine,
            sourceFiles
                .Select(File.ReadAllText));
        var fontPath = Path.Combine(
            presentationRoot,
            "Assets",
            "Fonts",
            "FluentSystemIcons-Resizable.ttf");
        var glyphMapPath = Path.Combine(
            presentationRoot,
            "Assets",
            "Fonts",
            "FluentSystemIcons-Resizable.json");
        var licensePath = Path.Combine(
            presentationRoot,
            "Assets",
            "Fonts",
            "FluentSystemIcons-LICENSE.txt");

        Assert.Contains("<Style TargetType=\"FontIcon\">", appXaml);
        Assert.Contains(
            "ms-appx:///Assets/Fonts/FluentSystemIcons-Resizable.ttf#FluentSystemIcons-Resizable",
            appXaml);
        Assert.Contains(
            "<Setter Property=\"FontFamily\" Value=\"{StaticResource AppIconFontFamily}\" />",
            appXaml);
        Assert.DoesNotContain("Segoe MDL2 Assets", allSource);
        Assert.DoesNotContain("Segoe Fluent Icons", allSource);
        Assert.True(new FileInfo(fontPath).Length > 0, "The packaged icon font is missing or empty.");
        Assert.Contains("MIT License", File.ReadAllText(licensePath));

        using var glyphMapDocument = JsonDocument.Parse(File.ReadAllText(glyphMapPath));
        var packagedGlyphs = glyphMapDocument.RootElement
            .EnumerateObject()
            .Select(property => property.Value.GetInt32())
            .ToHashSet();
        var referencedGlyphs = EnumerateReferencedGlyphs(allSource).ToArray();

        Assert.NotEmpty(referencedGlyphs);
        Assert.All(
            referencedGlyphs,
            glyph => Assert.True(
                packagedGlyphs.Contains(glyph),
                $"Icon glyph U+{glyph:X4} is not present in the packaged Fluent icon map."));
    }

    private static IEnumerable<int> EnumerateReferencedGlyphs(string source)
    {
        const string xamlGlyphPattern = "Glyph=\\\"&#x(?<glyph>[0-9A-Fa-f]+);\\\"";
        const string csharpGlyphPattern = "\\\\u(?<glyph>[EF][0-9A-Fa-f]{3})";

        return Regex.Matches(source, $"(?:{xamlGlyphPattern})|(?:{csharpGlyphPattern})")
            .Select(match => Convert.ToInt32(match.Groups["glyph"].Value, 16))
            .Distinct();
    }

    private static bool IsBuildOutputPath(string path, string presentationRoot)
    {
        var relativePath = Path.GetRelativePath(presentationRoot, path);
        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return string.Equals(firstSegment, "bin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(firstSegment, "obj", StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssestBundleTools.Presentation.WinUI")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "AssestBundleTools.Presentation.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }
}
