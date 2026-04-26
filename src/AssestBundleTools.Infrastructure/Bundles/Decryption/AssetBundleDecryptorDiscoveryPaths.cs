namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 提供外部解密器清单的默认发现目录。
/// </summary>
internal static class AssetBundleDecryptorDiscoveryPaths
{
    public static string GetUserManagedManifestDirectory()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appDataRoot, "AssestBundleTools", "decryptors");
    }

    public static IReadOnlyList<string> GetDefaultManifestDirectories()
    {
        var applicationDirectory = AppContext.BaseDirectory;

        return new[]
        {
            GetUserManagedManifestDirectory(),
            Path.Combine(applicationDirectory, "decryptors")
        };
    }
}
