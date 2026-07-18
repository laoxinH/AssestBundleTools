using System.Net;
using System.Text;
using AssestBundleTools.Infrastructure.Extensions.Installation;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Extensions.Installation;

internal static class GitHubReleaseOnlineComponentPackageSourceTests
{
    public static async Task ResolveAsync_ShouldReadTrustedAssetAndSha256Digest()
    {
        const string digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        using var httpClient = CreateHttpClient(CreateReleaseJson(digest));
        var source = CreateSource();

        var package = await source.ResolveAsync(httpClient, CancellationToken.None);

        TestAssert.Equal(new Uri("https://github.com/vgmstream/vgmstream-releases/releases/download/nightly/vgmstream-win64.zip"), package.DownloadUri, "Trusted asset URL should be resolved.");
        TestAssert.Equal(digest, package.Sha256, "GitHub SHA-256 digest should be normalized without the algorithm prefix.");
        TestAssert.Equal(42L, package.SizeInBytes, "Release asset size should be retained.");
        TestAssert.Equal("nightly", package.Version, "Release name should be retained as the component version.");
    }

    public static async Task ResolveAsync_ShouldRejectNonHexadecimalDigest()
    {
        using var httpClient = CreateHttpClient(CreateReleaseJson(new string('z', 64)));
        var source = CreateSource();
        var rejected = false;

        try
        {
            await source.ResolveAsync(httpClient, CancellationToken.None);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        TestAssert.True(rejected, "A digest with the right length but non-hexadecimal characters must be rejected.");
    }

    private static GitHubReleaseOnlineComponentPackageSource CreateSource() =>
        new(
            new Uri("https://api.github.com/repos/vgmstream/vgmstream-releases/releases/tags/nightly"),
            "vgmstream-win64.zip",
            "github.com",
            "/vgmstream/vgmstream-releases/releases/download/",
            ["release-assets.githubusercontent.com"]);

    private static HttpClient CreateHttpClient(string json) =>
        new(new JsonContentHandler(json));

    private static string CreateReleaseJson(string digest) =>
        $$"""
        {
          "name": "nightly",
          "assets": [
            {
              "name": "vgmstream-win64.zip",
              "browser_download_url": "https://github.com/vgmstream/vgmstream-releases/releases/download/nightly/vgmstream-win64.zip",
              "digest": "sha256:{{digest}}",
              "size": 42
            }
          ]
        }
        """;

    private sealed class JsonContentHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
