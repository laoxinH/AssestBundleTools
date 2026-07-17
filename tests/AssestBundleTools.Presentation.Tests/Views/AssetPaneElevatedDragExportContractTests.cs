namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetPaneElevatedDragExportContractTests
{
    [Fact]
    public void ElevatedAssetDragExport_ShouldUseGestureFallbackInsteadOfNativeDragSource()
    {
        var projectRoot = GetPresentationProjectRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            projectRoot,
            "Views",
            "BundleExplorer",
            "Controls",
            "AssetPaneDragExportSourceCoordinator.cs"));
        var view = File.ReadAllText(Path.Combine(
            projectRoot,
            "Views",
            "BundleExplorer",
            "Controls",
            "AssetPaneView.DragExport.cs"));
        var listView = File.ReadAllText(Path.Combine(
            projectRoot,
            "Views",
            "BundleExplorer",
            "Controls",
            "AssetDragExportListView.cs"));

        Assert.Contains("if (_isElevated)", coordinator);
        Assert.Contains("container.CanDrag = false", coordinator);
        Assert.Contains("ElevatedDragThreshold", coordinator);
        Assert.Contains("AttachElevatedGestureHandlers(_assetListView)", coordinator);
        Assert.Contains("UIElement.PointerMovedEvent", coordinator);
        Assert.Contains("handledEventsToo: true", coordinator);
        Assert.Contains("container?.ReleasePointerCaptures()", coordinator);
        Assert.Contains("listView?.ReleasePointerCaptures()", coordinator);
        Assert.Contains("container.Opacity = 0.62", coordinator);
        Assert.Contains("await _elevatedDragAttempted(item)", coordinator);
        Assert.Contains("SetElevatedDragFeedback(isActive: true)", coordinator);
        Assert.Contains("SetElevatedDragFeedback(isActive: false)", coordinator);
        Assert.Contains("_elevatedDragAttempted(item)", coordinator);
        Assert.DoesNotContain("StartDragAsync", coordinator);
        Assert.Contains("InputSystemCursorShape.SizeAll", listView);
        Assert.Contains("ProtectedCursor", listView);
        Assert.Contains("ProcessElevationDiagnostics.Capture().IsElevated == true", view);
        Assert.Contains("ElevatedDragExportAttempted", view);
        Assert.Contains("OperationTask ?? Task.CompletedTask", view);
    }

    [Fact]
    public void ElevatedAssetDragExport_ShouldUseBrokerWhenCompatibilityIsEnabled_AndRestartOtherwise()
    {
        var projectRoot = GetPresentationProjectRoot();
        var page = File.ReadAllText(Path.Combine(
            projectRoot,
            "Views",
            "BundleExplorer",
            "BundleExplorerPage.AssetDragExport.cs"));
        var prompt = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "ElevationRestartPromptCoordinator.cs"));

        Assert.Contains("TryGetCurrentWorkspaceNavigationRequest", page);
        Assert.Contains("ShellNavigationKeys.Workbench", page);
        Assert.Contains("ElevatedDragDropCompatibilityEnabled", page);
        Assert.Contains("ElevatedDragExportBrokerService.StartDragAsync", page);
        Assert.Contains("PromptForDragExportRestartAsync", page);
        Assert.Contains("ClearWorkspaceRegistration", page);
        Assert.Contains("ResourceKeys.AppElevationDragExportWarningTitle", prompt);
        Assert.Contains("ResourceKeys.AppElevationDragExportWarningMessage", prompt);

        foreach (var culture in new[] { "zh-CN", "en-US" })
        {
            var resources = File.ReadAllText(Path.Combine(
                projectRoot,
                "Strings",
                culture,
                "Resources.resw"));
            Assert.Contains("App.ElevationDragExportWarning.Title", resources);
            Assert.Contains("App.ElevationDragExportWarning.Message", resources);
        }
    }

    [Fact]
    public void ElevatedAssetDragExportBroker_ShouldRunOleDragInUnelevatedHostMode()
    {
        var projectRoot = GetPresentationProjectRoot();
        var app = File.ReadAllText(Path.Combine(projectRoot, "App.xaml.cs"));
        var prompt = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "ElevationRestartPromptCoordinator.cs"));
        var nativeDrag = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "NativeOleFileDrag.cs"));
        var brokerService = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "WindowsElevatedDragExportBrokerService.cs"));

        Assert.Contains("ElevatedDragExportBrokerHost.TryRunCurrentProcess()", app);
        Assert.Contains("_dragExportBrokerService.EnsureStartedAsync()", prompt);
        Assert.Contains("DoDragDrop(", nativeDrag);
        Assert.Contains("ClipboardFormatFileDrop = 15", nativeDrag);
        Assert.Contains("CreateFileDropHandle", nativeDrag);
        Assert.Contains("TryRestartAsync(", brokerService);
        Assert.Contains("WritePayloadAsync", brokerService);
        Assert.Contains("WaitForCompletionAsync", brokerService);
        Assert.Contains("BrokerReady=", brokerService);
        Assert.Contains("Preparation=", brokerService);
    }

    private static string GetPresentationProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var projectRoot = Path.Combine(directory.FullName, "src", "AssestBundleTools.Presentation.WinUI");
            if (Directory.Exists(projectRoot))
            {
                return projectRoot;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Presentation project root was not found.");
    }
}
