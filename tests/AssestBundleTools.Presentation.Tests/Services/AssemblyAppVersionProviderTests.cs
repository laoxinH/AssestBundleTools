using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class AssemblyAppVersionProviderTests
{
    [Fact]
    public void ResolveVersion_ShouldPreferInformationalVersionFromGitTag()
    {
        var version = AssemblyAppVersionProvider.ResolveVersion(
            " 1.4.2-beta.1 ",
            new Version(1, 0, 0, 0));

        Assert.Equal("1.4.2-beta.1", version);
    }

    [Fact]
    public void ResolveVersion_ShouldFallBackToThreePartAssemblyVersion()
    {
        var version = AssemblyAppVersionProvider.ResolveVersion(
            " ",
            new Version(2, 3, 4, 5));

        Assert.Equal("2.3.4", version);
    }
}
