namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;

/// <summary>
/// 统一维护外部音频转码工具的默认发现路径。
/// </summary>
internal static class AudioTranscoderDiscoveryPaths
{
    public static IReadOnlyList<string> GetVgmstreamExecutableCandidates()
    {
        var appBaseDirectory = AppContext.BaseDirectory;
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userToolDirectory = Path.Combine(appDataRoot, "AssestBundleTools", "tools", "vgmstream");
        var appToolDirectory = Path.Combine(appBaseDirectory, "tools", "vgmstream");

        return
        [
            Path.Combine(userToolDirectory, "vgmstream-cli.exe"),
            Path.Combine(appToolDirectory, "vgmstream-cli.exe"),
            Path.Combine(appBaseDirectory, "vgmstream-cli.exe"),
            Path.Combine(userToolDirectory, "test.exe"),
            Path.Combine(appToolDirectory, "test.exe"),
            Path.Combine(appBaseDirectory, "test.exe"),
            "vgmstream-cli.exe",
            "test.exe"
        ];
    }
}
