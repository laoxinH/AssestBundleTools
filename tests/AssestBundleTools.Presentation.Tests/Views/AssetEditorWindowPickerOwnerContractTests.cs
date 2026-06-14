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

    [Fact]
    public void AssetEditorWindowCreation_ShouldUseLocalNotificationPresenter()
    {
        var repositoryRoot = FindRepositoryRoot();
        var coordinatorCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "Coordinators",
            "AssetEditorWindowCoordinator.cs"));
        var windowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            "AssetEditorWindow.xaml"));
        var viewModelCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "BundleExplorer",
            "Bindings",
            "AssetEditorWindowViewModel.cs"));

        Assert.Contains("var editorNotificationService = new AppNotificationService();", coordinatorCode);
        Assert.Contains("editorNotificationService,", coordinatorCode);
        Assert.DoesNotContain("App.CurrentApp.AppNotificationService,\r\n                App.CurrentApp.LocalizationService", coordinatorCode);
        Assert.Contains("<shared:AppNotificationPresenter", windowXaml);
        Assert.Contains("NotificationService=\"{x:Bind ViewModel.NotificationService, Mode=OneWay}\"", windowXaml);
        Assert.Contains("public IAppNotificationService NotificationService => _notificationService;", viewModelCode);
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
