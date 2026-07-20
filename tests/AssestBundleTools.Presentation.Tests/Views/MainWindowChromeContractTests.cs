namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class MainWindowChromeContractTests
{
    [Fact]
    public void MainWindowChrome_ShouldUseStandardSingleLineTitleAndHeaderlessShellPane()
    {
        var repositoryRoot = FindRepositoryRoot();
        var mainWindowXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "MainWindow.xaml"));
        var mainWindowCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "MainWindow.xaml.cs"));
        var windowAppearanceCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Services",
            "Platform",
            "Windowing",
            "ModernWindowAppearanceService.cs"));
        var presentationProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "AssestBundleTools.Presentation.WinUI.csproj"));
        var shellXaml = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "ShellPage.xaml"));
        var shellCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "ShellPage.xaml.cs"));
        var resourceKeys = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Localization",
            "ResourceKeys.cs"));
        var localizedResources = string.Concat(
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Strings",
                "zh-CN",
                "Resources.resw")),
            File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Strings",
                "en-US",
                "Resources.resw")));

        Assert.Contains("<RowDefinition Height=\"32\" />", mainWindowXaml);
        Assert.DoesNotContain("Height=\"48\"", mainWindowXaml);
        Assert.Contains("ModernWindowAppearanceService.ConfigureTitleBar(titleBar)", mainWindowCode);
        Assert.Contains("TitleBarHeightOption.Standard", windowAppearanceCode);
        Assert.DoesNotContain("TitleBarHeightOption.Tall", windowAppearanceCode);
        Assert.DoesNotContain("WindowSubtitleText", string.Concat(mainWindowXaml, mainWindowCode));
        Assert.Contains("x:Name=\"WindowTitleIcon\"", mainWindowXaml);
        Assert.Contains("Source=\"ms-appx:///Assets/AppLogo.png\"", mainWindowXaml);
        Assert.Contains("TryApplyWindowIcon();", mainWindowCode);
        Assert.Contains("_appWindow.SetIcon(iconPath);", mainWindowCode);
        Assert.Contains("<ApplicationIcon>Assets\\AppIcon.ico</ApplicationIcon>", presentationProject);
        Assert.Contains("<PublishTrimmed>False</PublishTrimmed>", presentationProject);
        Assert.Contains("Name=\"CopyBuiltInExtensionsToPublish\" AfterTargets=\"Publish\"", presentationProject);
        Assert.Contains("$(PublishDir)codecs\\%(RecursiveDir)%(Filename)%(Extension)", presentationProject);
        Assert.Contains("Name=\"AddBuiltInExtensionsToAppxPackage\"", presentationProject);
        Assert.Contains("BeforeTargets=\"_ComputeAppxPackagePayload\"", presentationProject);
        Assert.Contains("<TargetPath>codecs\\%(RecursiveDir)%(Filename)%(Extension)</TargetPath>", presentationProject);
        Assert.True(File.Exists(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Assets",
            "AppIcon.ico")));
        Assert.DoesNotContain("<NavigationView.PaneHeader>", shellXaml);
        Assert.Contains("CompactPaneLength=\"48\"", shellXaml);
        Assert.Contains("IsPaneToggleButtonVisible=\"True\"", shellXaml);
        Assert.Contains("PaneDisplayMode=\"Left\"", shellXaml);
        Assert.DoesNotContain("PaneDisplayMode=\"LeftCompact\"", shellXaml);
        Assert.DoesNotContain("PaneTitleText", string.Concat(shellXaml, shellCode));
        Assert.DoesNotContain("PaneSubtitleText", string.Concat(shellXaml, shellCode));
        Assert.DoesNotContain("MainWindowSubtitle", resourceKeys);
        Assert.DoesNotContain("ShellPaneTitle", resourceKeys);
        Assert.DoesNotContain("ShellPaneSubtitle", resourceKeys);
        Assert.DoesNotContain("MainWindow.Subtitle", localizedResources);
        Assert.DoesNotContain("Shell.Pane.Title", localizedResources);
        Assert.DoesNotContain("Shell.Pane.Subtitle", localizedResources);
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
