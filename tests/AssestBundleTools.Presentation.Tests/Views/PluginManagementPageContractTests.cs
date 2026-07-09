namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class PluginManagementPageContractTests
{
    [Fact]
    public void Shell_ShouldExposePluginManagementAsPrimaryNavigationItem()
    {
        var repositoryRoot = FindRepositoryRoot();
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
        var navigationKeys = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "Shell",
            "Contracts",
            "ShellNavigationKeys.cs"));

        Assert.Contains("x:Name=\"PluginManagementNavItem\"", shellXaml);
        Assert.Contains("Tag=\"plugin-management\"", shellXaml);
        Assert.Contains("typeof(PluginManagementPage)", shellCode);
        Assert.Contains("ShellNavigationKeys.PluginManagement", shellCode);
        Assert.Contains("public const string PluginManagement = \"plugin-management\";", navigationKeys);
    }

    [Fact]
    public void PluginManagement_ShouldOwnDecryptorManagementUi()
    {
        var repositoryRoot = FindRepositoryRoot();
        var pluginPage = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "PluginManagement",
            "PluginManagementPage.xaml"));
        var pluginPageCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "PluginManagement",
            "PluginManagementPage.xaml.cs"));
        var settingsPage = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "SettingsDiagnostics",
            "SettingsDiagnosticsPage.xaml"));
        var settingsViewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "SettingsDiagnostics",
            "SettingsDiagnosticsViewModel.cs"));

        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.VisibleDecryptorPlugins", pluginPage);
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.VisibleResourceProcessorPlugins", pluginPage);
        Assert.Contains("PluginManagementToolbarSurfaceStyle", pluginPage);
        Assert.Contains("PluginManagementSummaryBadgeStyle", pluginPage);
        Assert.Contains("Text=\"{x:Bind ViewModel.VisiblePluginSummaryText, Mode=OneWay}\"", pluginPage);
        Assert.Contains("x:Name=\"PluginDirectoryEmptyTitleText\"", pluginPage);
        Assert.Contains("x:Name=\"PluginDetailsEmptyTitleText\"", pluginPage);
        Assert.Contains("Glyph=\"{x:Bind ViewModel.SelectedPluginIconGlyph, Mode=OneWay}\"", pluginPage);
        Assert.Contains("OnAddDecryptorButtonClick", pluginPageCode);
        Assert.Contains("OnAddResourceProcessorButtonClick", pluginPageCode);
        Assert.Contains("AssetBundleDecryptorEditorDialogContent", pluginPageCode);
        Assert.Contains("ResourceProcessorEditorDialogContent", pluginPageCode);
        Assert.Contains("ViewModel.DeleteDecryptorAsync", pluginPageCode);
        Assert.Contains("ViewModel.DeleteResourceProcessorAsync", pluginPageCode);
        Assert.DoesNotContain("DecryptorCatalogTitleText", settingsPage);
        Assert.DoesNotContain("AssetBundleDecryptorCatalogService", settingsViewModel);
        Assert.DoesNotContain("DecryptorEntries", settingsViewModel);
    }

    [Fact]
    public void PluginManagement_ShouldRepresentDecryptorAndResourceProcessorCatalogs()
    {
        var repositoryRoot = FindRepositoryRoot();
        var viewModel = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "PluginManagement",
            "PluginManagementViewModel.cs"));
        var presenter = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "ViewModels",
            "PluginManagement",
            "PluginManagementPluginPresenter.cs"));

        Assert.Contains("IAssetBundleDecryptorCatalogService", viewModel);
        Assert.Contains("IResourceProcessorCatalogService", viewModel);
        Assert.Contains("VisibleDecryptorPlugins", viewModel);
        Assert.Contains("VisibleResourceProcessorPlugins", viewModel);
        Assert.Contains("VisiblePluginSummaryText", viewModel);
        Assert.Contains("SelectedPluginIconGlyph", viewModel);
        Assert.Contains("BuildAutoDetectDecryptor", viewModel);
        Assert.Contains("BuildResourceProcessor", viewModel);
        Assert.Contains("PluginManagementFilterKeys.Texture2D", presenter);
        Assert.Contains("PluginManagementFilterKeys.Audio", presenter);
        Assert.Contains("PluginManagementFilterKeys.Atlas", presenter);
        Assert.Contains("PluginManagementFilterKeys.Skel", presenter);
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
