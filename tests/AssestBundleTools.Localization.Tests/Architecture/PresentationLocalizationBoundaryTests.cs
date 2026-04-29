namespace AssestBundleTools.Localization.Tests.Architecture;

public sealed class PresentationLocalizationBoundaryTests
{
    private static readonly string[] LiteralResourceKeyCallMarkers =
    [
        "UiText.Get(\"",
        "UiText.GetFormatted(\"",
        "GetRequiredString(\""
    ];

    [Fact]
    public void PresentationFiles_ShouldUseResourceKeysConstants_InsteadOfLiteralResourceKeyStrings()
    {
        var presentationRoot = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var violations = Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.EndsWith(
                $"{Path.DirectorySeparatorChar}Localization{Path.DirectorySeparatorChar}ResourceKeys.cs",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = line, LineNumber = index + 1 }))
            .Where(static entry => ContainsLiteralResourceKeyCall(entry.Line))
            .Select(static entry => $"{entry.Path}:{entry.LineNumber}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Presentation localization calls must reference ResourceKeys constants instead of literal resource-key strings:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, violations));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AssestBundleTools.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test output directory.");
    }

    private static bool ContainsLiteralResourceKeyCall(string line)
    {
        return LiteralResourceKeyCallMarkers.Any(
            marker => line.Contains(marker, StringComparison.Ordinal));
    }
}
