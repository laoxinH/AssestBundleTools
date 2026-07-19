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

        Assert.Equal(new DpiAwareWindowLayout(200, 60, 1520, 920, 1040, 680), layout);
    }

    [Theory]
    [InlineData(120, 20, 20, 1880, 1000, 1300, 850)]
    [InlineData(144, 24, 24, 1872, 992, 1560, 992)]
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
}
