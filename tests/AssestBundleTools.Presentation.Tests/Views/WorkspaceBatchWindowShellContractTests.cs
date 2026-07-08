namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspaceBatchWindowShellContractTests
{
    [Fact]
    public void WorkspaceBatchWindows_ShouldUseUnifiedReplacementShellMetrics()
    {
        var repositoryRoot = FindRepositoryRoot();

        AssertUsesUnifiedShellMetrics(
            ReadView(repositoryRoot, "WorkspaceBatchReplacementWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceBatchReplacementWindow.xaml.cs"));
        AssertUsesUnifiedShellMetrics(
            ReadView(repositoryRoot, "WorkspaceBatchExtractionWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceBatchExtractionWindow.xaml.cs"));
        AssertUsesUnifiedShellMetrics(
            ReadView(repositoryRoot, "WorkspaceExportWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceExportWindow.xaml.cs"));
    }

    private static void AssertUsesUnifiedShellMetrics(string xaml, string code)
    {
        Assert.Contains("private const int InitialWidth = 1520;", code);
        Assert.Contains("private const int InitialHeight = 920;", code);
        Assert.Contains("private const int MinimumWidth = 1180;", code);
        Assert.Contains("private const int MinimumHeight = 760;", code);

        Assert.Contains("Padding=\"48,14,48,20\"", xaml);
        Assert.Contains("RowSpacing=\"12\"", xaml);
        Assert.Contains("Width=\"48\"", xaml);
        Assert.Contains("Height=\"48\"", xaml);
        Assert.Contains("Background=\"{ThemeResource AccentFillColorDefaultBrush}\"", xaml);
        Assert.Contains("CornerRadius=\"12\"", xaml);
        Assert.Contains("Glyph=\"&#xE8A5;\"", xaml);
        Assert.Contains("FontSize=\"26\"", xaml);
        Assert.Contains("x:Name=\"WindowTitleText\"", xaml);
        Assert.Contains("FontSize=\"28\"", xaml);
        Assert.Contains("x:Name=\"WindowSubtitleText\"", xaml);
        Assert.Contains("FontSize=\"13\"", xaml);
        Assert.DoesNotContain("FontSize=\"30\"", xaml);
        Assert.DoesNotContain("Width=\"56\"", xaml);
        Assert.DoesNotContain("Height=\"56\"", xaml);
    }

    private static string ReadView(string repositoryRoot, string fileName) =>
        File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            fileName));

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
