namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class InspectorPanelReplacementSectionContractTests
{
    [Fact]
    public void InspectorReplacementSection_ShouldBeReservedForSpineEditMode()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = ReadInspectorControl(repositoryRoot, "InspectorPanelView.xaml");
        var code = ReadInspectorControl(repositoryRoot, "InspectorPanelView.xaml.cs");
        var viewModel = ReadInspectorViewModel(repositoryRoot, "InspectorPanelViewModel.Sections.cs");
        var spineTabViewModel = ReadInspectorViewModel(repositoryRoot, "SpineAssetGroupDocumentTabViewModel.cs");

        Assert.Contains(
            "Visibility=\"{x:Bind ViewModel.ReplacementSectionTabVisibility, Mode=OneWay}\"",
            xaml);
        Assert.Contains("StandardReplacementContentVisibility => Visibility.Collapsed", code);
        Assert.Contains("public bool IsReplacementSectionAvailable", viewModel);
        Assert.Contains("2 when !IsReplacementSectionAvailable => 0", viewModel);
        Assert.Contains("Inspector.SetReplacementSectionAvailability(value is not null);", spineTabViewModel);
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

    private static string ReadInspectorViewModel(string repositoryRoot, string fileName) =>
        File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Bindings",
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
