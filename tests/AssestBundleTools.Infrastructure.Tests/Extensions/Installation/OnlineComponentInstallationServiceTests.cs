using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using AssestBundleTools.Application.Extensions.Dtos;
using AssestBundleTools.Infrastructure.Extensions.Installation;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Extensions.Installation;

internal static class OnlineComponentInstallationServiceTests
{
    public static async Task InstallAsync_ShouldVerifyAndInstallCompleteArchive()
    {
        using var scope = new TemporaryDirectoryScope();
        var archive = CreateArchive(
            ("vgmstream-cli.exe", [0x01, 0x02, 0x03]),
            ("libatrac9.dll", [0x04, 0x05]),
            ("COPYING", "license"u8.ToArray()));
        var installDirectory = Path.Combine(scope.DirectoryPath, "tools", "vgmstream");
        using var httpClient = CreateHttpClient(archive);
        var registration = CreateRegistration(installDirectory, archive);
        var service = new OnlineComponentInstallationService(httpClient, [registration]);
        var progress = new List<OnlineComponentInstallProgressDto>();

        var result = await service.InstallAsync(
            registration.Id,
            new InlineProgress<OnlineComponentInstallProgressDto>(progress.Add));

        TestAssert.True(result.IsSuccess, "Trusted component package should install successfully.");
        TestAssert.True(File.Exists(Path.Combine(installDirectory, "vgmstream-cli.exe")), "Required executable should be installed.");
        TestAssert.True(File.Exists(Path.Combine(installDirectory, "libatrac9.dll")), "Runtime dependencies should be installed with the executable.");
        TestAssert.True(service.ResolveDescriptor(registration.Id)?.IsInstalled == true, "Catalog should report the installed component.");
        TestAssert.True(progress.Any(static item => item.Stage == OnlineComponentInstallStage.Verifying), "Installation should report hash verification.");
        TestAssert.True(progress.Any(static item => item.Stage == OnlineComponentInstallStage.Installing), "Installation should report the commit stage.");
    }

    public static async Task InstallAsync_ShouldRejectDigestMismatch_WithoutInstallingFiles()
    {
        using var scope = new TemporaryDirectoryScope();
        var archive = CreateArchive(("vgmstream-cli.exe", [0x01]));
        var installDirectory = Path.Combine(scope.DirectoryPath, "tools", "vgmstream");
        using var httpClient = CreateHttpClient(archive);
        var registration = CreateRegistration(installDirectory, archive, sha256: new string('0', 64));
        var service = new OnlineComponentInstallationService(httpClient, [registration]);

        var result = await service.InstallAsync(registration.Id);

        TestAssert.False(result.IsSuccess, "A package with a mismatched digest must fail installation.");
        TestAssert.False(Directory.Exists(installDirectory), "A failed verification must not publish an install directory.");
    }

    public static async Task InstallAsync_ShouldRejectPathTraversal_WithoutInstallingFiles()
    {
        using var scope = new TemporaryDirectoryScope();
        var archive = CreateArchive(
            ("vgmstream-cli.exe", [0x01]),
            ("../outside.txt", "outside"u8.ToArray()));
        var installDirectory = Path.Combine(scope.DirectoryPath, "tools", "vgmstream");
        using var httpClient = CreateHttpClient(archive);
        var registration = CreateRegistration(installDirectory, archive);
        var service = new OnlineComponentInstallationService(httpClient, [registration]);

        var result = await service.InstallAsync(registration.Id);

        TestAssert.False(result.IsSuccess, "An archive containing a traversal entry must fail installation.");
        TestAssert.False(Directory.Exists(installDirectory), "A rejected archive must not publish an install directory.");
        TestAssert.False(File.Exists(Path.Combine(scope.DirectoryPath, "outside.txt")), "A traversal entry must not write outside staging.");
    }

    private static OnlineComponentRegistration CreateRegistration(
        string installDirectory,
        byte[] archive,
        string? sha256 = null) =>
        new(
            "runtime.test-component",
            "Test component",
            OnlineComponentKind.RuntimeDependency,
            "Test publisher",
            "https://example.test/project",
            "Test license",
            "Test source",
            installDirectory,
            "vgmstream-cli.exe",
            new TestPackageSource(
                new Uri("https://packages.example.test/component.zip"),
                sha256 ?? Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant(),
                archive.LongLength,
                "test-version"));

    private static HttpClient CreateHttpClient(byte[] content) =>
        new(new StaticContentHandler(content));

    private static byte[] CreateArchive(params (string Path, byte[] Content)[] entries)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var entry = archive.CreateEntry(path, CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }

    private sealed class TestPackageSource(
        Uri downloadUri,
        string sha256,
        long sizeInBytes,
        string version) : IOnlineComponentPackageSource
    {
        public Task<ResolvedOnlineComponentPackage> ResolveAsync(
            HttpClient httpClient,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedOnlineComponentPackage(
                downloadUri,
                sha256,
                sizeInBytes,
                version));

        public bool IsDownloadUriAllowed(Uri uri) =>
            uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals(uri.Host, downloadUri.Host, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticContentHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(content)
            });
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
