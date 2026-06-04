namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetEditorWindowPickerOwnerContractTests
{
    [Fact]
    public void AssetEditorWindowCreation_ShouldRegisterWindowAsFilePickerOwner()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "App.xaml.cs"));
        var coordinatorCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "AssetEditorWindowCoordinator.cs"));

        Assert.Contains("public void RegisterAuxiliaryWindow(Microsoft.UI.Xaml.Window window)", appCode);
        Assert.Contains("ActiveWindowAccessor: ResolveActiveOwnerWindow", appCode);
        Assert.Contains("_activeOwnerWindow = window;", appCode);
        Assert.Contains("App.CurrentApp.RegisterAuxiliaryWindow(window);", coordinatorCode);
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
