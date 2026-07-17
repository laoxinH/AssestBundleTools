using AssestBundleTools.Presentation.WinUI.Services;

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
        var assetPaneXaml = File.ReadAllText(Path.Combine(
            projectRoot,
            "Views",
            "BundleExplorer",
            "Controls",
            "AssetPaneView.xaml"));

        Assert.Contains("if (_isElevated)", coordinator);
        Assert.Contains("container.CanDrag = false", coordinator);
        Assert.Contains("ElevatedDragThreshold", coordinator);
        Assert.Contains("AttachElevatedGestureHandlers(_assetListView)", coordinator);
        Assert.Contains("UIElement.PointerMovedEvent", coordinator);
        Assert.Contains("handledEventsToo: true", coordinator);
        Assert.Contains("ResetElevatedGesture(releasePointerCaptures: false)", coordinator);
        Assert.Contains("if (releasePointerCaptures)", coordinator);
        Assert.Contains("container.Opacity = 0.62", coordinator);
        Assert.Contains("await _elevatedDragAttempted(item)", coordinator);
        Assert.Contains("_elevatedDragAttempted(item)", coordinator);
        Assert.DoesNotContain("StartDragAsync", coordinator);
        Assert.DoesNotContain("InputSystemCursorShape.SizeAll", coordinator);
        Assert.DoesNotContain("ProtectedCursor", coordinator);
        Assert.DoesNotContain("AssetDragExportListView", assetPaneXaml);
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
        var dragImage = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "NativeOleDragImage.cs"));
        var releaseMonitor = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "NativeOlePointerReleaseMonitor.cs"));
        var feedbackWindow = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "NativeOleDragFeedbackWindow.cs"));
        var dataObject = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "NativeOleFileDropDataObject.cs"));
        var protocol = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "ElevatedDragExportBrokerProtocol.cs"));

        Assert.Contains("ElevatedDragExportBrokerHost.TryRunCurrentProcess()", app);
        Assert.Contains("_dragExportBrokerService.EnsureStartedAsync()", prompt);
        Assert.Contains("DoDragDrop(", nativeDrag);
        Assert.DoesNotContain("SetCapture(", nativeDrag);
        Assert.Contains("NativeOleDragImage.TryInitialize", nativeDrag);
        Assert.Contains("NativeOleDragFeedbackWindow.TryCreate", nativeDrag);
        Assert.Contains("NativeOlePointerReleaseMonitor.Start", nativeDrag);
        Assert.Contains("IDragSourceHelper", dragImage);
        Assert.Contains("InitializeFromBitmap", dragImage);
        Assert.Contains("ShellDragImage", dragImage);
        Assert.Contains("CreateDIBSection", dragImage);
        Assert.Contains("ApplyExplicitAlpha", dragImage);
        Assert.Contains("feedbackBitmap", dragImage);
        Assert.Contains("shellBitmap", dragImage);
        Assert.DoesNotContain("GetAsyncKeyState", releaseMonitor);
        Assert.Contains("_pointerReleaseProvider", releaseMonitor);
        Assert.Contains("ScreenToClient", releaseMonitor);
        Assert.Contains("GetGUIThreadInfo", releaseMonitor);
        Assert.Contains("CaptureWindow", releaseMonitor);
        Assert.Contains("NativeOleDragReleaseGate", releaseMonitor);
        Assert.Contains("Pointer release relay requested cancellation", releaseMonitor);
        Assert.DoesNotContain("because the drag started after physical release", releaseMonitor);
        Assert.Contains("packedPosition", releaseMonitor);
        Assert.Contains("_feedbackWindow?.Hide()", releaseMonitor);
        Assert.Contains("WindowMessageLeftButtonUp", releaseMonitor);
        Assert.Contains("PostMessageW", releaseMonitor);
        Assert.Contains("ExtendedStyleNoActivate", feedbackWindow);
        Assert.Contains("ExtendedStyleTransparent", feedbackWindow);
        Assert.Contains("UpdateLayeredWindow", feedbackWindow);
        Assert.Contains("UpdatePosition", feedbackWindow);
        Assert.Contains("ClipboardFormatFileDrop = 15", dataObject);
        Assert.Contains("CreateFileDropHandle", dataObject);
        Assert.Contains("_storedFormats", dataObject);
        Assert.Contains("ReleaseStgMedium", dataObject);
        Assert.Contains("CF_HDROP requested", dataObject);
        Assert.DoesNotContain("SetData is not supported", dataObject);
        Assert.Contains("TryRestartAsync(", brokerService);
        Assert.Contains("WriteCommand(requestPath, descriptor)", brokerService);
        Assert.Contains("RelayPointerReleaseAsync", brokerService);
        Assert.Contains("GetAsyncKeyState", brokerService);
        Assert.Contains("GetCursorPos", brokerService);
        Assert.Contains("new NativePointerReleaseSignal(false, 0, 0)", brokerService);
        Assert.Contains("WritePointerRelease", brokerService);
        Assert.Contains("WritePayloadAsync", brokerService);
        Assert.Contains("WaitForCompletionAsync", brokerService);
        Assert.Contains("BrokerReady=", brokerService);
        Assert.Contains("Preparation=", brokerService);
        Assert.Contains("completion.HResult == 0x00040100 && completion.Effect == 0", brokerService);
        Assert.Contains("_releaseGate.IsCancelRequested", nativeDrag);
        Assert.Contains("writer.Write(SanitizeDisplayName(descriptor.DisplayName))", protocol);
    }

    [Fact]
    public void ElevatedAssetDragExportBrokerCommand_ShouldRoundTripSanitizedVisualMetadata()
    {
        var requestPath = Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools.Presentation.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(requestPath);
        try
        {
            ElevatedDragExportBrokerProtocol.WriteCommand(
                requestPath,
                new ElevatedDragExportDescriptor("  sample\r\nasset.png  ", 3));

            var command = ElevatedDragExportBrokerProtocol.ReadCommand(Path.Combine(
                requestPath,
                ElevatedDragExportBrokerProtocol.CommandFileName));

            Assert.NotNull(command);
            Assert.Equal("sample  asset.png", command.DisplayName);
            Assert.Equal(3, command.ItemCount);
        }
        finally
        {
            Directory.Delete(requestPath, recursive: true);
        }
    }

    [Fact]
    public void ElevatedAssetDragExportPointerRelease_ShouldRoundTripDropCoordinates()
    {
        var requestPath = Path.Combine(
            Path.GetTempPath(),
            "AssestBundleTools.Presentation.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(requestPath);
        try
        {
            ElevatedDragExportBrokerProtocol.WritePointerRelease(
                requestPath,
                new NativePointerReleaseSignal(true, 1720, 840));

            var signal = ElevatedDragExportBrokerProtocol.ReadPointerRelease(requestPath);

            Assert.NotNull(signal);
            Assert.True(signal.Value.DropRequested);
            Assert.Equal(1720, signal.Value.ScreenX);
            Assert.Equal(840, signal.Value.ScreenY);
        }
        finally
        {
            Directory.Delete(requestPath, recursive: true);
        }
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
