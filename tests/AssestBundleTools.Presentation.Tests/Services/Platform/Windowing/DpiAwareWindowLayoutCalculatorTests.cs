using AssestBundleTools.Presentation.WinUI.Services.Platform.Windowing;

namespace AssestBundleTools.Presentation.Tests.Services.Platform.Windowing;

public sealed class DpiAwareWindowLayoutCalculatorTests
{
    private static readonly DpiAwareWindowLayoutProfile Profile =
        DpiAwareWindowLayoutProfile.WorkspaceOperationWindow;

    [Fact]
    public void Calculate_At100Percent_ShouldPreserveEffectiveDesignSizeAndCenterWindow()
    {
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            Profile,
            dpi: 96,
            new PhysicalWorkArea(0, 0, 1920, 1040));

        Assert.Equal(new DpiAwareWindowLayout(352, 152, 1216, 736, 1040, 680), layout);
    }

    [Fact]
    public void Calculate_MainWindowAt100Percent_ShouldUseCompactInitialSize()
    {
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            DpiAwareWindowLayoutProfile.MainWindow,
            dpi: 96,
            new PhysicalWorkArea(0, 0, 1920, 1040));

        Assert.Equal(new DpiAwareWindowLayout(320, 136, 1280, 768, 900, 640), layout);
    }

    [Fact]
    public void Calculate_WithRememberedEffectiveSize_ShouldScaleAndClampOnCurrentDisplay()
    {
        var rememberedProfile = DpiAwareWindowLayoutProfile.MainWindow with
        {
            InitialWidth = 1100d,
            InitialHeight = 700d
        };

        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            rememberedProfile,
            dpi: 144,
            new PhysicalWorkArea(0, 0, 1920, 1040));

        Assert.Equal(new DpiAwareWindowLayout(135, 24, 1650, 992, 1350, 960), layout);
    }

    [Theory]
    [InlineData(120, 200, 60, 1520, 920, 1300, 850)]
    [InlineData(144, 48, 24, 1824, 992, 1560, 992)]
    public void Calculate_AtScaledDpi_ShouldScaleThenConstrainToPhysicalWorkArea(
        uint dpi,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight,
        int expectedMinimumWidth,
        int expectedMinimumHeight)
    {
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            Profile,
            dpi,
            new PhysicalWorkArea(0, 0, 1920, 1040));

        Assert.Equal(
            new DpiAwareWindowLayout(
                expectedX,
                expectedY,
                expectedWidth,
                expectedHeight,
                expectedMinimumWidth,
                expectedMinimumHeight),
            layout);
    }

    [Fact]
    public void Calculate_WhenScaledMinimumExceedsWorkArea_ShouldKeepWholeWindowReachable()
    {
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            Profile,
            dpi: 144,
            new PhysicalWorkArea(1920, -120, 1366, 728));

        Assert.Equal(new DpiAwareWindowLayout(1944, -96, 1318, 680, 1318, 680), layout);
    }

    [Theory]
    [InlineData(96, 432, 192, 1056, 656, 760, 520)]
    [InlineData(120, 300, 110, 1320, 820, 950, 650)]
    [InlineData(144, 168, 28, 1584, 984, 1140, 780)]
    public void Calculate_AssetEditorAtSupportedScale_ShouldPreserveEffectiveLayout(
        uint dpi,
        int expectedX,
        int expectedY,
        int expectedWidth,
        int expectedHeight,
        int expectedMinimumWidth,
        int expectedMinimumHeight)
    {
        var layout = DpiAwareWindowLayoutCalculator.Calculate(
            DpiAwareWindowLayoutProfile.AssetEditorWindow,
            dpi,
            new PhysicalWorkArea(0, 0, 1920, 1040));

        Assert.Equal(
            new DpiAwareWindowLayout(
                expectedX,
                expectedY,
                expectedWidth,
                expectedHeight,
                expectedMinimumWidth,
                expectedMinimumHeight),
            layout);
    }
}
