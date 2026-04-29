namespace AssestBundleTools.Localization.Tests.Architecture;

public sealed class PresentationInfrastructureBoundaryTests
{
    [Fact]
    public void PresentationFilesOutsideComposition_ShouldNotReferenceInfrastructureNamespaces()
    {
        var presentationRoot = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var violations = Directory.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
            .Where(static path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Composition{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = line, LineNumber = index + 1 }))
            .Where(static entry => entry.Line.Contains("using AssestBundleTools.Infrastructure", StringComparison.Ordinal))
            .Select(static entry => $"{entry.Path}:{entry.LineNumber}")
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Presentation files outside Composition must not reference Infrastructure directly:" +
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
}
