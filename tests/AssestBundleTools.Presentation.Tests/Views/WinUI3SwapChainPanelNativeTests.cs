using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class WinUI3SwapChainPanelNativeTests
{
    [Fact]
    public void Contract_ShouldUseWindowsAppSdkSwapChainPanelNativeInterface()
    {
        Assert.Equal(
            new Guid("63aad0b8-7c24-40ff-85a8-640d944cc325"),
            WinUI3SwapChainPanelNative.Iid);
        Assert.Equal(3, WinUI3SwapChainPanelNative.SetSwapChainVtableIndex);
    }
}
