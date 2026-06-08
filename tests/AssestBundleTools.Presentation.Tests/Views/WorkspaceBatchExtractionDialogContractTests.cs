namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspaceBatchExtractionDialogContractTests
{
    [Fact]
    public void BundleExplorerTopBatchExtractCommand_ShouldOpenWorkspaceDialog()
    {
        var repositoryRoot = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.xaml.cs"));

        Assert.Contains("Click=\"OnWorkspaceBatchExtractChipButtonClick\"", xaml);
        Assert.Contains("private async void OnWorkspaceBatchExtractChipButtonClick", codeBehind);
        Assert.Contains("_workspaceBatchExtractionCoordinator.ShowAsync()", codeBehind);
    }

    [Fact]
    public void WorkspaceBatchExtraction_ShouldUseIndependentWindow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchExtractionCoordinator.cs"));
        var windowXamlPath = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml");

        Assert.True(File.Exists(windowXamlPath));
        Assert.Contains("new WorkspaceBatchExtractionWindow(viewModel)", coordinator);
        Assert.Contains("ShowForSubmissionAsync()", coordinator);
        Assert.DoesNotContain("ContentDialog", coordinator);
    }

    [Fact]
    public void WorkspaceBatchExtractionCancellation_ShouldUseSilentNonFailureFeedback()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "BundleExplorerWorkspaceBatchExtractionCoordinator.cs"));
        var workspaceCancellationDoc = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "docs",
            "design",
            "workspace-batch-extraction-dialog.md"));

        var executionStart = coordinator.IndexOf(
            "private async Task ExecuteSubmittedExtractionAsync",
            StringComparison.Ordinal);
        var cancellationCatch = coordinator.IndexOf(
            "catch (OperationCanceledException)",
            executionStart,
            StringComparison.Ordinal);
        var unexpectedCatch = coordinator.IndexOf(
            "CommonUnexpectedFailure",
            executionStart,
            StringComparison.Ordinal);
        var resultCancellation = coordinator.IndexOf(
            "if (result.IsCancelled)",
            executionStart,
            StringComparison.Ordinal);
        var successMessage = coordinator.IndexOf(
            "WorkspaceBatchExtractionDialogCompletedMessageFormat",
            executionStart,
            StringComparison.Ordinal);

        Assert.True(cancellationCatch > executionStart);
        Assert.True(unexpectedCatch > cancellationCatch);
        Assert.True(resultCancellation > unexpectedCatch);
        Assert.True(successMessage > resultCancellation);
        Assert.DoesNotContain("HandleExtractionCancelled", coordinator);
        Assert.DoesNotContain("WorkspaceBatchExtractionDialogCancelledMessage", coordinator);
        Assert.Contains("不额外弹出成功 / 失败 / 信息 toast", workspaceCancellationDoc);
    }

    [Fact]
    public void WindowContent_ShouldExposeBundleCategoryAndDeferredOutputSelections()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contentXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Controls",
            "WorkspaceBatchExtractionDialogContent.xaml"));
        var windowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml"));

        Assert.Contains("ItemsSource=\"{Binding VisibleBundles}\"", contentXaml);
        Assert.Contains("IsChecked=\"{Binding IsSelected, Mode=TwoWay}\"", contentXaml);
        Assert.Contains("ItemsSource=\"{Binding CategoryOptions}\"", contentXaml);
        Assert.Contains("Text=\"{Binding CandidateCountText}\"", contentXaml);
        Assert.DoesNotContain("OutputDirectoryText", contentXaml);
        Assert.DoesNotContain("ItemsWrapGrid", contentXaml);
        Assert.Contains("UseWorkspaceExtractionDirectoryCheckBox", windowXaml);
        Assert.Contains("UseWorkspaceExtractionDirectory", windowXaml);
        Assert.Contains("Text=\"{Binding HeaderSummaryText}\"", windowXaml);
        Assert.Contains("Text=\"{Binding PlanSummaryText}\"", windowXaml);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", contentXaml);
        Assert.Contains("HorizontalContentAlignment=\"Stretch\"", windowXaml);
        Assert.Contains("SetTitleBar(AppTitleBar)", File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "WorkspaceBatchExtractionWindow.xaml.cs")));
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
