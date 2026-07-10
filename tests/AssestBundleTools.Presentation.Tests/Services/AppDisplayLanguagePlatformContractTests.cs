namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class AppDisplayLanguagePlatformContractTests
{
    [Fact]
    public void WindowsOverride_ShouldUseWindowsAppSdkGlobalizationApi()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Composition",
            "WindowsAppDisplayLanguageOverride.cs"));

        Assert.Contains("using Microsoft.Windows.Globalization;", source);
        Assert.DoesNotContain("using Windows.Globalization;", source);
        Assert.Contains("ApplicationLanguages.PrimaryLanguageOverride", source);
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
