namespace AssestBundleTools.Infrastructure.Bundles.PostExport;

internal static class PostExportPluginDiscoveryPaths
{
    public static string GetUserManagedManifestDirectory()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataRoot, "AssestBundleTools", "post-export-plugins");
    }

    public static IReadOnlyList<string> GetDefaultManifestDirectories() =>
    [
        GetUserManagedManifestDirectory(),
        Path.Combine(AppContext.BaseDirectory, "post-export-plugins")
    ];
}
