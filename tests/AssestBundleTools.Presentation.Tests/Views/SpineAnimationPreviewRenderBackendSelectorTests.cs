using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationPreviewRenderBackendSelectorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("d3d11")]
    public void ResolvePreferredKind_ShouldUseD3D11Backend_ByDefault(string? value)
    {
        var kind = SpineAnimationPreviewRenderBackendSelector.ResolvePreferredKind(
            name => name == SpineAnimationPreviewRenderBackendSelector.D3D11EnvironmentVariable
                ? value
                : null);

        Assert.Equal(SpineAnimationPreviewRenderBackendKind.D3D11, kind);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("off")]
    [InlineData("win2d")]
    public void ResolvePreferredKind_ShouldUseWin2DBackend_WhenD3D11IsDisabled(string? value)
    {
        var kind = SpineAnimationPreviewRenderBackendSelector.ResolvePreferredKind(
            name => name == SpineAnimationPreviewRenderBackendSelector.D3D11EnvironmentVariable
                ? value
                : null);

        Assert.Equal(SpineAnimationPreviewRenderBackendKind.Win2D, kind);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    public void ResolvePreferredKind_ShouldHonorLegacySmokeFlag_WhenMainFlagIsUnset(
        string legacyValue,
        bool expectedD3D11)
    {
        var kind = SpineAnimationPreviewRenderBackendSelector.ResolvePreferredKind(name =>
            name == SpineAnimationPreviewRenderBackendSelector.D3D11SmokeEnvironmentVariable
                ? legacyValue
                : null);

        var expected = expectedD3D11
            ? SpineAnimationPreviewRenderBackendKind.D3D11
            : SpineAnimationPreviewRenderBackendKind.Win2D;
        Assert.Equal(expected, kind);
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
