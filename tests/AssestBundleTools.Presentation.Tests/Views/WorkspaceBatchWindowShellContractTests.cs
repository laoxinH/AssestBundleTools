namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WorkspaceBatchWindowShellContractTests
{
    [Fact]
    public void WorkspaceBatchWindows_ShouldUseUnifiedReplacementShellMetrics()
    {
        var repositoryRoot = FindRepositoryRoot();

        AssertUsesUnifiedShellMetrics(
            ReadView(repositoryRoot, "WorkspaceBatchReplacementWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceBatchReplacementWindow.xaml.cs"));
        AssertUsesUnifiedShellMetrics(
            ReadView(repositoryRoot, "WorkspaceBatchExtractionWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceBatchExtractionWindow.xaml.cs"));
        AssertUsesUnifiedShellMetrics(
            ReadView(repositoryRoot, "WorkspaceExportWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceExportWindow.xaml.cs"));
    }

    [Fact]
    public void WorkspaceOperationAndAssetEditorWindows_ShouldUseModernTransparentChrome()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appearanceService = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Services",
            "Platform",
            "Windowing",
            "ModernWindowAppearanceService.cs"));

        Assert.Contains("window.SystemBackdrop = new MicaBackdrop()", appearanceService);
        Assert.Contains("ABT_DISABLE_SYSTEM_BACKDROP", appearanceService);
        Assert.Contains("catch (COMException ex)", appearanceService);

        AssertUsesModernTransparentChrome(
            ReadView(repositoryRoot, "WorkspaceBatchReplacementWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceBatchReplacementWindow.xaml.cs"));
        AssertUsesModernTransparentChrome(
            ReadView(repositoryRoot, "WorkspaceBatchExtractionWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceBatchExtractionWindow.xaml.cs"));
        AssertUsesModernTransparentChrome(
            ReadView(repositoryRoot, "WorkspaceExportWindow.xaml"),
            ReadView(repositoryRoot, "WorkspaceExportWindow.xaml.cs"));

        var assetEditorXaml = ReadView(repositoryRoot, "AssetEditorWindow.xaml");
        var assetEditorCode = string.Concat(
            ReadView(repositoryRoot, "AssetEditorWindow.xaml.cs"),
            ReadView(repositoryRoot, "AssetEditorWindow.Layout.cs"),
            ReadView(repositoryRoot, "AssetEditorWindow.Localization.cs"));
        AssertUsesModernTransparentChrome(assetEditorXaml, assetEditorCode);
        Assert.Contains("<RowDefinition Height=\"40\" />", assetEditorXaml);
        Assert.Contains("x:Name=\"TitleBarText\"", assetEditorXaml);
        Assert.Contains("TitleBarText.Text = Title", assetEditorCode);
        Assert.Contains("DpiAwareWindowLayoutProfile.AssetEditorWindow", assetEditorCode);
    }

    private static void AssertUsesUnifiedShellMetrics(string xaml, string code)
    {
        Assert.Contains("DpiAwareWindowLayoutService.Apply(", code);
        Assert.Contains("DpiAwareWindowLayoutProfile.WorkspaceOperationWindow", code);
        Assert.DoesNotContain("_appWindow.Resize(", code);

        Assert.Contains("Padding=\"48,14,48,20\"", xaml);
        Assert.Contains("RowSpacing=\"12\"", xaml);
        Assert.Contains("Width=\"48\"", xaml);
        Assert.Contains("Height=\"48\"", xaml);
        Assert.Contains("Background=\"{ThemeResource AccentFillColorDefaultBrush}\"", xaml);
        Assert.Contains("CornerRadius=\"12\"", xaml);
        Assert.Contains("Glyph=\"&#xE6E9;\"", xaml);
        Assert.Contains("FontSize=\"26\"", xaml);
        Assert.Contains("x:Name=\"WindowTitleText\"", xaml);
        Assert.Contains("FontSize=\"28\"", xaml);
        Assert.Contains("x:Name=\"WindowSubtitleText\"", xaml);
        Assert.Contains("FontSize=\"13\"", xaml);
        Assert.DoesNotContain("FontSize=\"30\"", xaml);
        Assert.DoesNotContain("Width=\"56\"", xaml);
        Assert.DoesNotContain("Height=\"56\"", xaml);
    }

    private static void AssertUsesModernTransparentChrome(string xaml, string code)
    {
        Assert.Contains("x:Name=\"AppTitleBar\"", xaml);
        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("ModernWindowAppearanceService.TryApplySystemBackdrop(this)", code);
        Assert.Contains("ExtendsContentIntoTitleBar = true", code);
        Assert.Contains("SetTitleBar(AppTitleBar)", code);
        Assert.Contains("ModernWindowAppearanceService.ConfigureTitleBar", code);
    }

    private static string ReadView(string repositoryRoot, string fileName) =>
        File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "AssestBundleTools.Presentation.WinUI",
            "Views",
            "BundleExplorer",
            fileName));

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
