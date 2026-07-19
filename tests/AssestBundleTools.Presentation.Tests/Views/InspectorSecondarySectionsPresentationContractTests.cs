namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class InspectorSecondarySectionsPresentationContractTests
{
    [Fact]
    public void ChangesSection_ShouldUseBorderlessEmptyStateAndHideEmptySummary()
    {
        var repositoryRoot = FindRepositoryRoot();
        var inspectorXaml = ReadInspectorControl(repositoryRoot, "InspectorPanelView.xaml");
        var xaml = ReadInspectorControl(repositoryRoot, "InspectorChangesView.xaml");
        var code = ReadInspectorControl(repositoryRoot, "InspectorChangesView.xaml.cs");

        Assert.Contains("<controls:InspectorChangesView", inspectorXaml);
        Assert.Contains("x:Name=\"ChangesEmptyState\"", xaml);
        Assert.Contains("x:Name=\"ChangesEmptyStateTitleText\"", xaml);
        Assert.Contains("Glyph=\"&#xEA2E;\"", xaml);
        Assert.Contains(
            "Visibility=\"{Binding ChangeRecordsListVisibility}\"",
            xaml);
        Assert.DoesNotContain("ChangesEmptyStateCard", xaml);
        Assert.DoesNotContain("ChangeSummaryTitleText", xaml);
        Assert.Contains(
            "ChangesEmptyStateTitleText.Text = UiText.Get(ResourceKeys.InspectorChangesEmptyStateTitle);",
            code);
    }

    [Fact]
    public void SpineReplacementSection_ShouldUseModernPartCardsAndExistingCommands()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = ReadInspectorControl(repositoryRoot, "SpineEditPartsPanelView.xaml");

        Assert.Contains("x:Key=\"SpineEditPartCardStyle\"", xaml);
        Assert.Contains("Style=\"{StaticResource SpineEditPartCardStyle}\"", xaml);
        Assert.Contains("Glyph=\"&#xE75D;\"", xaml);
        Assert.Contains("Glyph=\"&#xE8F1;\"", xaml);
        Assert.Contains("Click=\"OnBatchMatchButtonClick\"", xaml);
        Assert.Contains("Click=\"OnSelectPartFileButtonClick\"", xaml);
        Assert.Contains("Text=\"{x:Bind SourceText, Mode=OneWay}\"", xaml);
        Assert.DoesNotContain("CardBackgroundFillColorSecondaryBrush", xaml);
    }

    private static string ReadInspectorControl(string repositoryRoot, string fileName) =>
        File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
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
