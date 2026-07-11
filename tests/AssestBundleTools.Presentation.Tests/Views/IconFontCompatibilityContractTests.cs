namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class IconFontCompatibilityContractTests
{
    [Fact]
    public void FontIcons_ShouldUseWindows10CompatibleSystemFont()
    {
        var repositoryRoot = FindRepositoryRoot();
        var presentationRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var appXaml = File.ReadAllText(Path.Combine(presentationRoot, "App.xaml"));
        var allXaml = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(presentationRoot, "*.xaml", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutputPath(path, presentationRoot))
                .Select(File.ReadAllText));

        Assert.Contains("<Style TargetType=\"FontIcon\">", appXaml);
        Assert.Contains("<Setter Property=\"FontFamily\" Value=\"Segoe MDL2 Assets\" />", appXaml);
        Assert.DoesNotContain("Segoe Fluent Icons", allXaml);
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
