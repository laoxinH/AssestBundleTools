namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class AssetEditorWindowSpriteEditActionBarContractTests
{
    [Fact]
    public void TextureReplacement_ShouldLiveInSpriteEditActionBarInsteadOfPreviewToolbar()
    {
        var repositoryRoot = FindRepositoryRoot();
        var assetEditorXaml = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "AssetEditorWindow.xaml"));
        var assetEditorCode = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "AssetEditorWindow.xaml.cs"));
        var previewSurfaceXaml = File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.xaml"));

        var actionBarIndex = assetEditorXaml.IndexOf("SpriteEditActionBarVisibility", StringComparison.Ordinal);
        var replaceIndex = assetEditorXaml.IndexOf("x:Name=\"ReplaceSpriteTextureButton\"", StringComparison.Ordinal);
        var saveIndex = assetEditorXaml.IndexOf("x:Name=\"SaveSpriteEditButton\"", StringComparison.Ordinal);

        Assert.True(actionBarIndex >= 0);
        Assert.True(replaceIndex > actionBarIndex);
        Assert.True(saveIndex > replaceIndex);
        Assert.Contains("Click=\"OnReplaceSpriteTextureButtonClick\"", assetEditorXaml);
        Assert.Contains("ViewModel.SelectSpriteTextureReplacementSourceAsync()", assetEditorCode);
        Assert.DoesNotContain("SpriteEditReplaceTextureButton", previewSurfaceXaml);
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
