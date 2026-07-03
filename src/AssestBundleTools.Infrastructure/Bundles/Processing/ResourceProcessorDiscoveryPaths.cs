namespace AssestBundleTools.Infrastructure.Bundles.Processing;

/// <summary>
/// 提供导出资源处理器清单的默认发现目录。
/// </summary>
internal static class ResourceProcessorDiscoveryPaths
{
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
}
