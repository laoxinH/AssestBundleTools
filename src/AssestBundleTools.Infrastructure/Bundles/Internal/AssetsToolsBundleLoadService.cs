using AssetsTools.NET.Extra;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 统一 AssetsTools bundle 加载入口；读预览可缓存，写包必须使用独占可变会话。
/// </summary>
internal static class AssetsToolsBundleLoadService
{
    public static AssetsToolsBundleLoadedSession LoadIsolated(string bundlePath)
    {
        var assetsManager = CreateAssetsManager();

        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance, out var directoryIndexLookup);
            var internalSerializedFileLookup = AssetsToolsBundleDependencyResolver.BuildInternalSerializedFileLookup(serializedFiles);
            return new AssetsToolsBundleLoadedSession(
                bundlePath,
                assetsManager,
                bundleInstance,
                serializedFiles,
                directoryIndexLookup,
                internalSerializedFileLookup);
        }
        catch
        {
            assetsManager.UnloadAll(unloadClassData: false);
            throw;
        }
    }

    private static AssetsManager CreateAssetsManager() =>
        new()
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

    private static IReadOnlyList<AssetsFileInstance> LoadSerializedFiles(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance,
        out IReadOnlyDictionary<string, int> directoryIndexLookup)
    {
        var serializedFiles = new List<AssetsFileInstance>();
        var mutableDirectoryIndexLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;

        for (var index = 0; index < directoryInfos.Count; index++)
        {
            if (!bundleInstance.file.IsAssetsFile(index))
            {
                continue;
            }

            var serializedFile = assetsManager.LoadAssetsFileFromBundle(bundleInstance, index, loadDeps: false);
            serializedFiles.Add(serializedFile);
            TrackSerializedFileDirectoryIndex(mutableDirectoryIndexLookup, serializedFile, index);
        }

        directoryIndexLookup = mutableDirectoryIndexLookup;
        return serializedFiles;
    }

    private static void TrackSerializedFileDirectoryIndex(
        Dictionary<string, int> directoryIndexLookup,
        AssetsFileInstance serializedFile,
        int directoryIndex)
    {
        if (!string.IsNullOrWhiteSpace(serializedFile.name))
        {
            directoryIndexLookup[serializedFile.name] = directoryIndex;
        }

        if (!string.IsNullOrWhiteSpace(serializedFile.path))
        {
            directoryIndexLookup[serializedFile.path] = directoryIndex;
        }
    }
}
