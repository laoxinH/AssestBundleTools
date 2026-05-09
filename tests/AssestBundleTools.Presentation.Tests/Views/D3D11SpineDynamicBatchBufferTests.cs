using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class D3D11SpineDynamicBatchBufferTests
{
    [Theory]
    [InlineData(0, 256, 0)]
    [InlineData(1, 256, 256)]
    [InlineData(256, 256, 256)]
    [InlineData(257, 256, 512)]
    [InlineData(900, 384, 1536)]
    public void CalculateCapacity_ShouldGrowByPowerOfTwoFromMinimum(
        int requestedCount,
        int minimumCapacity,
        int expectedCapacity)
    {
        var capacity = D3D11SpineDynamicBatchBuffer.CalculateCapacity(requestedCount, minimumCapacity);

        Assert.Equal(expectedCapacity, capacity);
    }
}
