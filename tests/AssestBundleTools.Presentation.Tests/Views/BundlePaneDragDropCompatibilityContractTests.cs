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
        var dragDropCode = File.ReadAllText(pagePath + ".DragDrop.cs");

        Assert.Contains("x:Name=\"BundlePaneHost\"", pageXaml);
        Assert.Contains("AllowDrop=\"True\"", pageXaml);
        Assert.DoesNotContain("DragOver=\"OnWorkspaceImportDragOver\"", pageXaml);
        Assert.Contains("RegisterWorkspaceImportDropHandlers();", pageCode);
        Assert.Contains("DragEnterEvent", dragDropCode);
        Assert.Contains("DragOverEvent", dragDropCode);
        Assert.Contains("DropEvent", dragDropCode);
        Assert.Contains("handledEventsToo: true", dragDropCode);

        var dragOverHandler = dragDropCode[
            dragDropCode.IndexOf("private void OnWorkspaceImportDragOver", StringComparison.Ordinal)..
            dragDropCode.IndexOf("private void OnWorkspaceImportDragLeave", StringComparison.Ordinal)];
        Assert.Contains("ApplyWorkspaceImportDragAcceptance(e);", dragOverHandler);
        Assert.DoesNotContain("StandardDataFormats.StorageItems", dragOverHandler);

        var acceptanceHandler = dragDropCode[
            dragDropCode.IndexOf("private void ApplyWorkspaceImportDragAcceptance", StringComparison.Ordinal)..
            dragDropCode.IndexOf("private void AppendDragDropEventDiagnostic", StringComparison.Ordinal)];
        Assert.Contains("ViewModel.WorkspaceShell.HasWorkspace", acceptanceHandler);
        Assert.Contains("DataPackageOperation.Copy", acceptanceHandler);
        Assert.Contains("e.Handled = true;", acceptanceHandler);
        Assert.DoesNotContain("StandardDataFormats.StorageItems", acceptanceHandler);

        var dropHandler = dragDropCode[
            dragDropCode.IndexOf("private async void OnWorkspaceImportDrop", StringComparison.Ordinal)..
            dragDropCode.IndexOf("private void ApplyWorkspaceImportDragAcceptance", StringComparison.Ordinal)];
        Assert.Contains("StandardDataFormats.StorageItems", dropHandler);
        Assert.Contains("GetStorageItemsAsync", dropHandler);
        Assert.Contains("WorkspaceImportLogDragDropStorageItemsReadFailedFormat", dropHandler);
        Assert.Contains("WorkspaceImportLogDragDropDispatchResultFormat", dropHandler);
        Assert.Contains("finally", dropHandler);

        Assert.Contains("ProcessElevationDiagnostics.Capture()", dragDropCode);
        Assert.Contains("WorkspaceImportLogDragDropHandlerRegisteredFormat", dragDropCode);
        Assert.Contains("WorkspaceImportLogDragDropEventFormat", dragDropCode);
        Assert.Contains("_workspaceImportFirstDragOverLogged", dragDropCode);
        Assert.Contains(
            "ElevatedDragDropCompatibilityService.FilesDropped +=",
            dragDropCode);
        Assert.Contains("OnElevatedCompatibilityFilesDropped", dragDropCode);
        Assert.Contains("IsElevatedCompatibilityDropWithinBundlePane", dragDropCode);
        Assert.Contains("RequestDragDropImportAsync(droppedPaths)", dragDropCode);
        Assert.Contains("WorkspaceImportLogElevatedDropReceivedFormat", dragDropCode);
        Assert.Contains("WorkspaceImportLogElevatedDropWindowFallbackFormat", dragDropCode);
        var elevatedDropHandler = dragDropCode[
            dragDropCode.IndexOf("private async void OnElevatedCompatibilityFilesDropped", StringComparison.Ordinal)..
            dragDropCode.IndexOf("private void AppendWorkspaceImportDropEnvironmentDiagnostic", StringComparison.Ordinal)];
        var windowFallbackBlock = elevatedDropHandler[
            elevatedDropHandler.IndexOf("WorkspaceImportLogElevatedDropWindowFallbackFormat", StringComparison.Ordinal)..
            elevatedDropHandler.IndexOf("if (!ViewModel.WorkspaceShell.HasWorkspace)", StringComparison.Ordinal)];
        Assert.DoesNotContain("return;", windowFallbackBlock);
        Assert.Contains(
            "ElevatedDragDropCompatibilityService.FilesDropped -=",
            pageCode);
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
