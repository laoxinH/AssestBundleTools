using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationPreviewRenderBackendSelectorTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("d3d11")]
    public void ResolvePreferredKind_ShouldEnableD3D11SmokeBackend_WhenEnvironmentFlagIsEnabled(string value)
    {
        var kind = SpineAnimationPreviewRenderBackendSelector.ResolvePreferredKind(
            name => name == SpineAnimationPreviewRenderBackendSelector.D3D11SmokeEnvironmentVariable
                ? value
                : null);

        Assert.Equal(SpineAnimationPreviewRenderBackendKind.D3D11Smoke, kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("false")]
    public void ResolvePreferredKind_ShouldUseWin2DBackend_ByDefault(string? value)
    {
        var kind = SpineAnimationPreviewRenderBackendSelector.ResolvePreferredKind(
            name => name == SpineAnimationPreviewRenderBackendSelector.D3D11SmokeEnvironmentVariable
                ? value
                : null);

        Assert.Equal(SpineAnimationPreviewRenderBackendKind.Win2D, kind);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("d3d11", true)]
    [InlineData(null, false)]
    [InlineData("0", false)]
    public void ResolveD3D11DiagnosticsEnabled_ShouldFollowEnvironmentFlag(string? value, bool expected)
    {
        var enabled = SpineAnimationPreviewRenderBackendSelector.ResolveD3D11DiagnosticsEnabled(
            name => name == SpineAnimationPreviewRenderBackendSelector.D3D11DiagnosticsEnvironmentVariable
                ? value
                : null);

        Assert.Equal(expected, enabled);
    }
}
