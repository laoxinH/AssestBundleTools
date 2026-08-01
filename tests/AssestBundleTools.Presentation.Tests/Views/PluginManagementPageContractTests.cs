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
        var pluginPackageImportCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "PluginManagement",
            "PluginManagementPage.PackageImport.cs"));
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
        Assert.Contains("ItemsSource=\"{x:Bind ViewModel.VisiblePostExportPlugins", pluginPage);
        Assert.Contains("PluginManagementToolbarSurfaceStyle", pluginPage);
        Assert.Contains("PluginManagementCatalogStatChipStyle", pluginPage);
        Assert.Contains("PluginManagementListViewItemStyle", pluginPage);
        Assert.Contains("x:Name=\"HoverOverlay\"", pluginPage);
        Assert.Contains("x:Name=\"SelectedOverlay\"", pluginPage);
        Assert.Contains("x:Name=\"SelectedAccent\"", pluginPage);
        Assert.Contains("PluginManagementHeaderIconButtonStyle", pluginPage);
        Assert.Contains("x:Name=\"AddPluginMenuButton\"", pluginPage);
        Assert.Contains("x:Name=\"AddDecryptorMenuFlyoutItem\"", pluginPage);
        Assert.Contains("x:Name=\"AddResourceProcessorMenuFlyoutItem\"", pluginPage);
        Assert.Contains("x:Name=\"AddPostExportPluginMenuFlyoutItem\"", pluginPage);
        Assert.Contains("x:Name=\"ImportPluginFolderMenuFlyoutItem\"", pluginPage);
        Assert.Contains("x:Name=\"ImportPluginArchiveMenuFlyoutItem\"", pluginPage);
        Assert.Contains("x:Name=\"PluginDirectoryHelpHost\"", pluginPage);
        Assert.Contains("PointerEntered=\"OnPluginDirectoryHelpPointerEntered\"", pluginPage);
        Assert.Contains("Text=\"{x:Bind ViewModel.VisiblePluginCountText, Mode=OneWay}\"", pluginPage);
        Assert.Contains("Text=\"{x:Bind ViewModel.VisibleDecryptorPluginCountText, Mode=OneWay}\"", pluginPage);
        Assert.Contains("Text=\"{x:Bind ViewModel.VisibleResourceProcessorPluginCountText, Mode=OneWay}\"", pluginPage);
        Assert.Contains("CreateImmediateInfoToolTip", pluginPageCode);
        Assert.Contains("ToolTipService.SetToolTip(PluginDirectoryHelpHost", pluginPageCode);
        Assert.Contains("x:Name=\"PluginDirectoryEmptyTitleText\"", pluginPage);
        Assert.Contains("x:Name=\"PluginDetailsEmptyTitleText\"", pluginPage);
        Assert.Contains("Glyph=\"{x:Bind ViewModel.SelectedPluginIconGlyph, Mode=OneWay}\"", pluginPage);
        Assert.Contains("ToolTipService.SetToolTip(AddPluginMenuButton", pluginPageCode);
        Assert.Contains("OnAddDecryptorMenuFlyoutItemClick", pluginPageCode);
        Assert.Contains("OnAddResourceProcessorMenuFlyoutItemClick", pluginPageCode);
        Assert.Contains("OnAddPostExportPluginMenuFlyoutItemClick", pluginPageCode);
        Assert.Contains("PickPluginPackageDirectoryAsync", pluginPackageImportCode);
        Assert.Contains("PickPluginPackageArchiveAsync", pluginPackageImportCode);
        Assert.Contains("OverwriteConfirmationRequired", pluginPackageImportCode);
        Assert.Contains("replaceExisting: true", pluginPackageImportCode);
        Assert.Contains("AssetBundleDecryptorEditorDialogContent", pluginPageCode);
        Assert.Contains("ResourceProcessorEditorDialogContent", pluginPageCode);
        Assert.Contains("PostExportPluginEditorDialogContent", pluginPageCode);
        Assert.Contains("ViewModel.DeleteDecryptorAsync", pluginPageCode);
        Assert.Contains("ViewModel.DeleteResourceProcessorAsync", pluginPageCode);
        Assert.Contains("ViewModel.DeletePostExportPluginAsync", pluginPageCode);
        Assert.DoesNotContain("DecryptorCatalogTitleText", settingsPage);
        Assert.DoesNotContain("AssetBundleDecryptorCatalogService", settingsViewModel);
        Assert.DoesNotContain("DecryptorEntries", settingsViewModel);
    }

    [Fact]
    public void PluginManagement_ShouldRepresentAllSupportedPluginCatalogs()
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
        Assert.Contains("IPostExportPluginCatalogService", viewModel);
        Assert.Contains("VisibleDecryptorPlugins", viewModel);
        Assert.Contains("VisibleResourceProcessorPlugins", viewModel);
        Assert.Contains("VisiblePostExportPlugins", viewModel);
        Assert.Contains("VisiblePluginCountText", viewModel);
        Assert.Contains("VisibleDecryptorPluginCountText", viewModel);
        Assert.Contains("VisibleResourceProcessorPluginCountText", viewModel);
        Assert.Contains("SelectedPluginIconGlyph", viewModel);
        Assert.Contains("BuildAutoDetectDecryptor", viewModel);
        Assert.Contains("BuildResourceProcessor", viewModel);
        Assert.Contains("BuildPostExportPlugin", viewModel);
        Assert.Contains("PluginManagementFilterKeys.Texture2D", presenter);
        Assert.Contains("PluginManagementFilterKeys.Audio", presenter);
        Assert.Contains("PluginManagementFilterKeys.Atlas", presenter);
        Assert.Contains("PluginManagementFilterKeys.Skel", presenter);
    }

    [Fact]
    public void PostExportPluginEditor_ShouldUseTheSharedThreeStepEditorInteraction()
    {
        var repositoryRoot = FindRepositoryRoot();
        var editorDirectory = Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "PluginManagement",
            "Controls");
        var editorXaml = File.ReadAllText(Path.Combine(
            editorDirectory,
            "PostExportPluginEditorDialogContent.xaml"));
        var editorCode = File.ReadAllText(Path.Combine(
            editorDirectory,
            "PostExportPluginEditorDialogContent.xaml.cs"));
        var pluginPageCode = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "PluginManagement",
            "PluginManagementPage.xaml.cs"));
        Assert.Contains("x:Name=\"BasicStepIndicatorBorder\"", editorXaml);
        Assert.Contains("x:Name=\"ExecutionStepIndicatorBorder\"", editorXaml);
        Assert.Contains("x:Name=\"ArgumentsStepIndicatorBorder\"", editorXaml);
        Assert.Contains("x:Name=\"BasicStepPanel\"", editorXaml);
        Assert.Contains("x:Name=\"ExecutionStepPanel\"", editorXaml);
        Assert.Contains("x:Name=\"ArgumentsStepPanel\"", editorXaml);
        Assert.Contains("PluginEditorCardStyle", editorXaml);
        Assert.Contains("public event EventHandler? StepChanged", editorCode);
        Assert.Contains("public bool TryMoveNext()", editorCode);
        Assert.Contains("public void MovePrevious()", editorCode);
        Assert.Contains("PostExportPluginEditorValidationManifestPathRequired", editorCode);
        Assert.Contains("editor.StepChanged +=", pluginPageCode);
        Assert.Contains("editor.MovePrevious();", pluginPageCode);
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
