namespace AssestBundleTools.Presentation.WinUI.Services;

internal static class GitHubReleaseUriFactory
{
    private const string ReleasesRoot = "https://github.com/laoxinH/AssestBundleTools/releases";

    public static Uri HistoryUri { get; } = new(ReleasesRoot);

    public static Uri CreateCurrentReleaseUri(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var normalizedVersion = version.Trim().TrimStart('v', 'V');
        var metadataSeparatorIndex = normalizedVersion.IndexOf('+');
        if (metadataSeparatorIndex >= 0)
        {
            normalizedVersion = normalizedVersion[..metadataSeparatorIndex];
        }

        return new Uri($"{ReleasesRoot}/tag/v{Uri.EscapeDataString(normalizedVersion)}");
    }
}
