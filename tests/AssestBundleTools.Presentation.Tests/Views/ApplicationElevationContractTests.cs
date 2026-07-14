namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class ApplicationElevationContractTests
{
    [Fact]
    public void Application_ShouldRunAsInvoker_AndWarnWhenActuallyElevated()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectRoot = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var manifest = File.ReadAllText(Path.Combine(projectRoot, "app.manifest"));
        var appCode = File.ReadAllText(Path.Combine(projectRoot, "App.xaml.cs"));
        var elevationCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Diagnostics",
            "ProcessElevationDiagnostics.cs"));
        var promptCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "ElevationRestartPromptCoordinator.cs"));
        var restartCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "WindowsUnelevatedProcessRestartService.cs"));
        var compatibilityCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "WindowsElevatedDragDropCompatibilityService.cs"));
        var routerCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "ElevatedFileDropRouter.cs"));
        var coordinateMapperCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "DragDrop",
            "ElevatedDropCoordinateMapper.cs"));
        var explorerShellCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Platform",
            "WindowsUnelevatedProcessRestartService.ExplorerShell.cs"));

        Assert.Contains("requestedExecutionLevel level=\"asInvoker\" uiAccess=\"false\"", manifest);
        Assert.DoesNotContain("requireAdministrator", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("uiAccess=\"true\"", manifest, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("HandleElevationRestartPromptAsync(window, launchRequest)", appCode);
        Assert.Contains("IsUnelevatedRestartLaunch", appCode);
        Assert.Contains("ClearWorkspaceRegistration();", appCode);
        Assert.Contains("ProcessElevationDiagnostics.Capture()", promptCode);
        Assert.Contains("ResourceKeys.AppElevationWarningRestartButton", promptCode);
        Assert.Contains("ContentDialogResult.Primary", promptCode);
        Assert.Contains("WaitForXamlRootAsync(ownerWindow.Content)", promptCode);
        Assert.Contains("contentRoot?.XamlRoot", promptCode);
        Assert.Contains("Shell.Application", explorerShellCode);
        Assert.Contains("FindWindowSW", explorerShellCode);
        Assert.Contains("desktopApplication", explorerShellCode);
        Assert.Contains("WaitForUnelevatedProcessAsync", restartCode);
        Assert.Contains("CreateProcessWithTokenW", restartCode);
        Assert.Contains("GetShellWindow", restartCode);
        Assert.Contains("TokenIsElevated", restartCode);
        Assert.Contains("ResourceKeys.AppElevationRestartStillElevatedMessage", promptCode);
        Assert.Contains("recursive prompt suppressed", promptCode);
        Assert.Contains("ElevatedDragDropCompatibilityEnabled", promptCode);
        Assert.Contains("ShowElevatedDragDropCompatibilityNotification", promptCode);
        Assert.Contains("ChangeWindowMessageFilterEx", compatibilityCode);
        Assert.Contains("DragAcceptFiles", compatibilityCode);
        Assert.Contains("SetWindowSubclass", compatibilityCode);
        Assert.Contains("RemoveWindowSubclass", compatibilityCode);
        Assert.Contains("DragQueryFile", compatibilityCode);
        Assert.Contains("DragQueryPoint", compatibilityCode);
        Assert.Contains("ClientToScreen", compatibilityCode);
        Assert.Contains("GetClientRect", compatibilityCode);
        Assert.Contains("DragFinish", compatibilityCode);
        Assert.Contains("FilesDropped?.Invoke", compatibilityCode);
        Assert.Contains("WM_DROPFILES", compatibilityCode);
        Assert.Contains("WM_COPYDATA", compatibilityCode);
        Assert.Contains("WM_COPYGLOBALDATA", compatibilityCode);
        Assert.Contains("ElementBounds", routerCode);
        Assert.Contains("WindowFallback", routerCode);
        Assert.Contains("FindExactMatch", routerCode);
        Assert.Contains("FindWindowFallback", routerCode);
        Assert.True(
            routerCode.IndexOf("FindExactMatch", StringComparison.Ordinal) <
            routerCode.IndexOf("FindWindowFallback", StringComparison.Ordinal));
        Assert.Contains("ClientWidth / rootWidth", coordinateMapperCode);
        Assert.Contains("ClientHeight / rootHeight", coordinateMapperCode);
        Assert.Contains("TryValidateClientPoint", coordinateMapperCode);
        Assert.Contains("ClientBoundsFallback", coordinateMapperCode);
        Assert.Contains("TransformToVisual(targetElement)", coordinateMapperCode);
        Assert.Contains("WindowsBuiltInRole.Administrator", elevationCode);
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
