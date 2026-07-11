namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class BundlePaneDragDropCompatibilityContractTests
{
    [Fact]
    public void BundlePaneDragDrop_ShouldAcceptBeforePayloadInspection_AndObserveHandledChildEvents()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pagePath = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "BundleExplorerPage");
        var pageXaml = File.ReadAllText(pagePath + ".xaml");
        var pageCode = File.ReadAllText(pagePath + ".xaml.cs");

        Assert.Contains("x:Name=\"BundlePaneHost\"", pageXaml);
        Assert.Contains("AllowDrop=\"True\"", pageXaml);
        Assert.DoesNotContain("DragOver=\"OnWorkspaceImportDragOver\"", pageXaml);
        Assert.Contains("RegisterWorkspaceImportDropHandlers();", pageCode);
        Assert.Contains("DragEnterEvent", pageCode);
        Assert.Contains("DragOverEvent", pageCode);
        Assert.Contains("DropEvent", pageCode);
        Assert.Contains("handledEventsToo: true", pageCode);

        var dragOverHandler = pageCode[
            pageCode.IndexOf("private void OnWorkspaceImportDragOver", StringComparison.Ordinal)..
            pageCode.IndexOf("private void OnWorkspaceImportDragLeave", StringComparison.Ordinal)];
        Assert.Contains("ViewModel.WorkspaceShell.HasWorkspace", dragOverHandler);
        Assert.Contains("DataPackageOperation.Copy", dragOverHandler);
        Assert.Contains("e.Handled = true;", dragOverHandler);
        Assert.DoesNotContain("StandardDataFormats.StorageItems", dragOverHandler);

        var dropHandler = pageCode[
            pageCode.IndexOf("private async void OnWorkspaceImportDrop", StringComparison.Ordinal)..
            pageCode.IndexOf("private void OnCollapsedBundlePaneActionPointerEntered", StringComparison.Ordinal)];
        Assert.Contains("StandardDataFormats.StorageItems", dropHandler);
        Assert.Contains("GetStorageItemsAsync", dropHandler);
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
