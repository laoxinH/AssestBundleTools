namespace AssestBundleTools.Infrastructure.Bundles.Processing;

/// <summary>
/// 提供导出资源处理器清单的默认发现目录。
/// </summary>
internal static class ResourceProcessorDiscoveryPaths
{
    private const string FmodWrapperExecutableFileName = "AssestBundleTools.Audio.FmodProcessor.exe";
    private const string FsbankExecutableFileName = "fsbankcl.exe";

    public static string GetUserManagedManifestDirectory()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataRoot, "AssestBundleTools", "resource-processors");
    }

    public static IReadOnlyList<string> GetDefaultManifestDirectories()
    {
        var applicationDirectory = AppContext.BaseDirectory;

        return new[]
        {
            GetUserManagedManifestDirectory(),
            Path.Combine(applicationDirectory, "resource-processors")
        };
    }

    public static IReadOnlyList<string> GetBuiltInFmodWrapperCandidates()
    {
        var applicationDirectory = AppContext.BaseDirectory;

        return
        [
            Path.Combine(applicationDirectory, FmodWrapperExecutableFileName),
            Path.Combine(applicationDirectory, "tools", "resource-processors", "audio", "fmod", FmodWrapperExecutableFileName)
        ];
    }

    public static IReadOnlyList<string> GetFmodFsbankExecutableCandidates()
    {
        var applicationDirectory = AppContext.BaseDirectory;
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userToolDirectory = Path.Combine(appDataRoot, "AssestBundleTools", "tools", "fmod");
        var appToolDirectory = Path.Combine(applicationDirectory, "tools", "fmod");

        return
        [
            Path.Combine(userToolDirectory, FsbankExecutableFileName),
            Path.Combine(appToolDirectory, FsbankExecutableFileName),
            Path.Combine(applicationDirectory, FsbankExecutableFileName),
            FsbankExecutableFileName
        ];
    }
}
