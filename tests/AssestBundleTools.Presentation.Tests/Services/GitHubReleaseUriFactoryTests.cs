using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class GitHubReleaseUriFactoryTests
{
    [Theory]
    [InlineData("0.1.3", "https://github.com/laoxinH/AssestBundleTools/releases/tag/v0.1.3")]
    [InlineData("v0.2.0-preview.1+build.7", "https://github.com/laoxinH/AssestBundleTools/releases/tag/v0.2.0-preview.1")]
    public void CreateCurrentReleaseUri_ShouldTargetVersionTag(string version, string expected)
    {
        Assert.Equal(expected, GitHubReleaseUriFactory.CreateCurrentReleaseUri(version).AbsoluteUri);
    }

    [Fact]
    public void HistoryUri_ShouldTargetRepositoryReleaseList()
    {
        Assert.Equal(
            "https://github.com/laoxinH/AssestBundleTools/releases",
            GitHubReleaseUriFactory.HistoryUri.AbsoluteUri.TrimEnd('/'));
    }
}
