using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class D3D11SpineAnimationBatchRendererTests
{
    [Fact]
    public void CreateInputElements_ShouldReadSingleVertexSlotWithExpectedOffsets()
    {
        var elements = D3D11SpineAnimationBatchRenderer.CreateInputElements();

        Assert.Equal(3, elements.Length);
        Assert.All(elements, static element => Assert.Equal(0u, element.Slot));
        Assert.Equal(0u, elements[0].AlignedByteOffset);
        Assert.Equal(8u, elements[1].AlignedByteOffset);
        Assert.Equal(16u, elements[2].AlignedByteOffset);
    }
}
