namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class ApplicationUpdatePromptContractTests
{
    [Fact]
    public void UpdatePrompt_ShouldDisplayBoundedSelectableReleaseNotes()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Services",
            "Updates",
            "ApplicationUpdateCoordinator.cs"));

        Assert.Contains("ConfirmUpdateAsync(update)", source);
        Assert.Contains("ApplicationUpdateReleaseNotesFormatter.Format(", source);
        Assert.Contains("ResourceKeys.ApplicationUpdatePromptReleaseNotesTitle", source);
        Assert.Contains("IsTextSelectionEnabled = true", source);
        Assert.Contains("MaxHeight = 280", source);
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
