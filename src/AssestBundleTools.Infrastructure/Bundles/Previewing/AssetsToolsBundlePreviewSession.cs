using AssetsTools.NET.Extra;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 缓存单个已加载 bundle 的 AssetsTools 会话，避免并发预览重复物理解包。
/// </summary>
internal sealed class AssetsToolsBundlePreviewSession : IDisposable
{
    private AssetsToolsBundlePreviewSession(
        string bundlePath,
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        BundlePath = bundlePath;
        AssetsManager = assetsManager;
        BundleInstance = bundleInstance;
        SerializedFiles = serializedFiles;
        InternalSerializedFileLookup = internalSerializedFileLookup;
    }

    public string BundlePath { get; }

    public AssetsManager AssetsManager { get; }

    public BundleFileInstance BundleInstance { get; }

    public IReadOnlyList<AssetsFileInstance> SerializedFiles { get; }

    public IReadOnlyDictionary<string, string> InternalSerializedFileLookup { get; }

    public SemaphoreSlim ExecutionGate { get; } = new(1, 1);

    public static AssetsToolsBundlePreviewSession Load(string bundlePath)
    {
        var assetsManager = new AssetsManager
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance);
            var internalSerializedFileLookup = AssetsToolsBundleDependencyResolver.BuildInternalSerializedFileLookup(serializedFiles);
            return new AssetsToolsBundlePreviewSession(
                bundlePath,
                assetsManager,
                bundleInstance,
                serializedFiles,
                internalSerializedFileLookup);
        }
        catch
        {
            assetsManager.UnloadAll(unloadClassData: false);
            throw;
        }
    }

    public void Dispose()
    {
        ExecutionGate.Dispose();
        AssetsManager.UnloadAll(unloadClassData: false);
    }

    private static IReadOnlyList<AssetsFileInstance> LoadSerializedFiles(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance)
    {
        var serializedFiles = new List<AssetsFileInstance>();
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;

        for (var index = 0; index < directoryInfos.Count; index++)
        {
            if (!bundleInstance.file.IsAssetsFile(index))
            {
                continue;
            }

            serializedFiles.Add(assetsManager.LoadAssetsFileFromBundle(bundleInstance, index, loadDeps: false));
        }

        return serializedFiles;
    }
}
