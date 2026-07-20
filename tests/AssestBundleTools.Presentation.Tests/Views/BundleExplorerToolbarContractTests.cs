namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class BundleExplorerToolbarContractTests
{
    [Fact]
    public void WorkspaceToolbar_ShouldUseBorderlessIconsAndHiddenHorizontalScrollbar()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));

        Assert.Contains("x:Key=\"WorkspaceChipIconBadgeStyle\"", pageXaml);
        Assert.Contains("<Setter Property=\"Background\" Value=\"Transparent\" />", pageXaml);
        Assert.Contains("<Setter Property=\"BorderBrush\" Value=\"Transparent\" />", pageXaml);
        Assert.Contains("<Setter Property=\"BorderThickness\" Value=\"0\" />", pageXaml);
        Assert.Contains("HorizontalScrollBarVisibility=\"Hidden\"", pageXaml);
    }

    [Fact]
    public void WorkspaceStatusChips_ShouldMatchToolbarChipHeightAndCenterContent()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.WorkspaceShell.WorkspaceStatusChips, Mode=OneWay}\"", pageXaml);
        Assert.Contains("x:Key=\"WorkspacePrimaryActionChipButtonStyle\"", pageXaml);
        Assert.Contains("x:Key=\"WorkspaceSecondaryActionChipButtonStyle\"", pageXaml);
        Assert.Contains("<Setter Property=\"Height\" Value=\"44\" />", pageXaml);
        Assert.DoesNotContain("<Setter Property=\"MinHeight\" Value=\"40\" />", pageXaml);
        Assert.Contains("MinHeight=\"44\"", pageXaml);
        Assert.Contains("Height=\"44\"", pageXaml);
        Assert.Contains("Padding=\"10,0\"", pageXaml);
        Assert.Contains("VerticalAlignment=\"Center\"", pageXaml);
    }

    [Fact]
    public void WorkspaceToolbarButtons_ShouldCenterIconTextGroups()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pageXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));

        var centerAlignmentCount = CountOccurrences(
            pageXaml,
            "<Setter Property=\"HorizontalContentAlignment\" Value=\"Center\" />");
        var actionsStart = pageXaml.IndexOf("x:Name=\"CreateWorkspaceChipButton\"", StringComparison.Ordinal);
        var actionsEnd = pageXaml.IndexOf("<ItemsControl", actionsStart, StringComparison.Ordinal);
        var actionsXaml = pageXaml[actionsStart..actionsEnd];

        Assert.True(centerAlignmentCount >= 2);
        Assert.Contains("<ColumnDefinition Width=\"Auto\" />", actionsXaml);
        Assert.DoesNotContain("<ColumnDefinition Width=\"*\" />", actionsXaml);
    }

    [Fact]
    public void WorkspacePageReentry_ShouldBindExistingSessionWithoutRepeatedSuccessFeedback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Bindings",
            "BundleExplorerViewModel.cs"));

        var sessionRebindStart = viewModel.IndexOf(
            "if (_workspaceShell.TryGetCurrentWorkspaceSessionContext",
            StringComparison.Ordinal);
        var sessionRebindEnd = viewModel.IndexOf(
            "_workspaceBindingCoordinator.ResetWorkspaceToEmptyState();",
            sessionRebindStart,
            StringComparison.Ordinal);
        var sessionRebind = viewModel[sessionRebindStart..sessionRebindEnd];

        Assert.Contains("clearExistingLogs: false", sessionRebind);
        Assert.Contains("showLoadedNotification: false", sessionRebind);
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

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }
}
