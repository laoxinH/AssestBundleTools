namespace AssestBundleTools.Infrastructure.Extensions.Installation;

internal static class OnlineComponentInstallationPaths
{
    public static string VgmstreamInstallDirectoryPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AssestBundleTools",
            "tools",
            "vgmstream");
}
