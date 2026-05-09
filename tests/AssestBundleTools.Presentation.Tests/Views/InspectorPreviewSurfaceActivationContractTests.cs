namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class InspectorPreviewSurfaceActivationContractTests
{
    [Fact]
    public void InspectorPanelPreview_ShouldDisableNestedPreview_WhenDetailsPreviewIsHidden()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPanelView.xaml"));

        Assert.Contains("IsPreviewActive=\"{x:Bind ShowDetailsPreview, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldFollowParentPreviewActivation()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorPreviewSurfaceView.xaml"));

        Assert.Contains("<controls:InspectorSpineAnimationPreviewSurfaceView", xaml);
        Assert.Contains("IsPreviewActive=\"{x:Bind IsPreviewActive, Mode=OneWay}\"", xaml);
    }

    [Fact]
    public void SpinePreviewSurface_ShouldBindSliderToThrottledDisplayPlaybackTime()
    {
        var xaml = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "src",
                "AssestBundleTools.Presentation.WinUI",
                "Views",
                "BundleExplorer",
                "Controls",
                "InspectorSpineAnimationPreviewSurfaceView.xaml"));

        Assert.Contains(
            "Value=\"{x:Bind ViewModel.DisplayPlaybackTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"",
            xaml);
        Assert.Contains(
            "Text=\"{x:Bind ViewModel.DisplayPlaybackTimeText, Mode=OneWay}\"",
            xaml);
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
