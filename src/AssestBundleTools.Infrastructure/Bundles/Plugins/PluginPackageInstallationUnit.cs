namespace AssestBundleTools.Infrastructure.Bundles.Plugins;

internal static class PluginPackageInstallationUnit
{
    public static void DeleteManifestOrPackage(string manifestPath, string catalogRootPath, string pluginId)
    {
        var rootPath = Path.GetFullPath(catalogRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullManifestPath = Path.GetFullPath(manifestPath);
        if (!fullManifestPath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = Path.GetRelativePath(rootPath, fullManifestPath);
        var firstSeparatorIndex = relativePath.IndexOfAny(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        if (firstSeparatorIndex > 0)
        {
            var topLevelDirectoryName = relativePath[..firstSeparatorIndex];
            if (string.Equals(topLevelDirectoryName, SanitizeDirectoryName(pluginId), StringComparison.OrdinalIgnoreCase))
            {
                var installationDirectoryPath = Path.Combine(rootPath, topLevelDirectoryName);
                if (Directory.Exists(installationDirectoryPath))
                {
                    Directory.Delete(installationDirectoryPath, recursive: true);
                    return;
                }
            }
        }

        if (File.Exists(fullManifestPath))
        {
            File.Delete(fullManifestPath);
        }
    }

    private static string SanitizeDirectoryName(string id)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var value = new string(id.Trim()
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');
        return string.IsNullOrWhiteSpace(value) ? "plugin" : value;
    }
}
