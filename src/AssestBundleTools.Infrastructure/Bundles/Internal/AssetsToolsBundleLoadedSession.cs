using AssetsTools.NET.Extra;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 缓存单个已加载 bundle 的 AssetsTools 会话，供读取、预览和提取共享。
/// </summary>
internal sealed class AssetsToolsBundleLoadedSession : IDisposable
{
    internal AssetsToolsBundleLoadedSession(
        string bundlePath,
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        IReadOnlyDictionary<string, int> serializedFileDirectoryIndexLookup,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        BundlePath = bundlePath;
        AssetsManager = assetsManager;
        BundleInstance = bundleInstance;
        SerializedFiles = serializedFiles;
        SerializedFileDirectoryIndexLookup = serializedFileDirectoryIndexLookup;
        InternalSerializedFileLookup = internalSerializedFileLookup;
    }

    public string BundlePath { get; }

    public AssetsManager AssetsManager { get; }

    public BundleFileInstance BundleInstance { get; }

    public IReadOnlyList<AssetsFileInstance> SerializedFiles { get; }

    public IReadOnlyDictionary<string, int> SerializedFileDirectoryIndexLookup { get; }

    public IReadOnlyDictionary<string, string> InternalSerializedFileLookup { get; }

    public SemaphoreSlim ExecutionGate { get; } = new(1, 1);

    public string OriginalCompression => BundleInstance.originalCompression.ToString();

    public static AssetsToolsBundleLoadedSession Load(string bundlePath) =>
        AssetsToolsBundleLoadService.LoadIsolated(bundlePath);

    public void Dispose()
    {
        ExecutionGate.Dispose();
        AssetsManager.UnloadAll(unloadClassData: false);
    }

}
