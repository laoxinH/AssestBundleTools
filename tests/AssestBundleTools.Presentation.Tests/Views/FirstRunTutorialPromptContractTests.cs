namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class FirstRunTutorialPromptContractTests
{
    [Fact]
    public void Startup_ShouldPromptOnceBeforeCheckingForUpdates()
    {
        var projectRoot = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "AssestBundleTools.Presentation.WinUI");
        var appCode = File.ReadAllText(Path.Combine(projectRoot, "App.xaml.cs"));
        var promptCode = File.ReadAllText(Path.Combine(
            projectRoot,
            "Services",
            "Onboarding",
            "FirstRunTutorialPromptCoordinator.cs"));

        Assert.Contains("https://www.modwu.com/10635.html", promptCode);
        Assert.Contains("FirstRunTutorialPromptHandled", promptCode);
        Assert.Contains("TryMarkFirstRunTutorialPromptHandled", promptCode);
        Assert.Contains("Launcher.LaunchUriAsync", promptCode);
        Assert.Contains("PromptAndRestartIfRequestedAsync", appCode);
        Assert.Contains("FirstRunTutorialPromptCoordinator.PromptIfNeededAsync", appCode);
        Assert.Contains("ApplicationUpdateCoordinator.CheckAtStartupAsync", appCode);
        Assert.True(
            appCode.IndexOf("PromptAndRestartIfRequestedAsync", StringComparison.Ordinal) <
            appCode.IndexOf("FirstRunTutorialPromptCoordinator.PromptIfNeededAsync", StringComparison.Ordinal));
        Assert.True(
            appCode.IndexOf("FirstRunTutorialPromptCoordinator.PromptIfNeededAsync", StringComparison.Ordinal) <
            appCode.IndexOf("ApplicationUpdateCoordinator.CheckAtStartupAsync", StringComparison.Ordinal));
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
