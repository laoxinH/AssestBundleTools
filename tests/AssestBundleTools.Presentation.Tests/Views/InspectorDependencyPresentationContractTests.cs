namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class InspectorDependencyPresentationContractTests
{
    [Fact]
    public void DependencySection_ShouldUseFlatThemedCardsAndCenteredEmptyState()
    {
        var repositoryRoot = FindRepositoryRoot();
        var inspectorXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "InspectorPanelView.xaml"));
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "InspectorDependenciesView.xaml"));
        var code = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "InspectorDependenciesView.xaml.cs"));

        Assert.Contains("<controls:InspectorDependenciesView", inspectorXaml);
        Assert.Contains("x:Key=\"DependencyCardStyle\"", xaml);
        Assert.Contains("x:Key=\"AssetDependencyItemTemplate\"", xaml);
        Assert.Contains("x:Key=\"ExternalDependencyItemTemplate\"", xaml);
        Assert.Equal(
            3,
            CountOccurrences(
                xaml,
                "ItemTemplate=\"{StaticResource AssetDependencyItemTemplate}\""));
        Assert.Contains("x:Name=\"DependenciesEmptyStateCard\"", xaml);
        Assert.Contains("x:Name=\"DependenciesEmptyStateTitleText\"", xaml);
        Assert.Contains("Style=\"{StaticResource DependencyEmptyStateCardStyle}\"", xaml);
        Assert.Contains("FontFamily=\"{StaticResource AppIconFontFamily}\"", xaml);
        Assert.Contains(
            "DependenciesEmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorDependenciesEmptyStateTitle);",
            code);
    }

    private static int CountOccurrences(string value, string token)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += token.Length;
        }

        return count;
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
