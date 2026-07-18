using AssestBundleTools.Application.Extensions;
using AssestBundleTools.Application.Extensions.Dtos;

namespace AssestBundleTools.Infrastructure.Extensions.Installation;

internal static class OnlineComponentCatalog
{
    public static IReadOnlyList<OnlineComponentRegistration> CreateDefault()
    {
        var assetName = Environment.Is64BitProcess
            ? "vgmstream-win64.zip"
            : "vgmstream-win.zip";
        return
        [
            new OnlineComponentRegistration(
                Id: OnlineComponentIds.VgmstreamCli,
                DisplayName: "vgmstream-cli",
                Kind: OnlineComponentKind.RuntimeDependency,
                Publisher: "vgmstream project",
                ProjectUri: "https://github.com/vgmstream/vgmstream",
                LicenseName: "vgmstream COPYING",
                SourceDisplayName: "GitHub vgmstream/vgmstream-releases",
                InstallDirectoryPath: OnlineComponentInstallationPaths.VgmstreamInstallDirectoryPath,
                RequiredFileRelativePath: "vgmstream-cli.exe",
                PackageSource: new GitHubReleaseOnlineComponentPackageSource(
                    new Uri("https://api.github.com/repos/vgmstream/vgmstream-releases/releases/tags/nightly"),
                    assetName,
                    allowedDownloadHost: "github.com",
                    allowedDownloadPathPrefix: "/vgmstream/vgmstream-releases/releases/download/",
                    allowedRedirectHosts: ["release-assets.githubusercontent.com"]))
        ];
    }
}
