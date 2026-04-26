using AssestBundleTools.Application.Bundles.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 提供可重载的解密器注册表包装器。
/// </summary>
/// <remarks>
/// 这样组合根只需要分发同一个实例，
/// 设置页重载后各处依赖都能立即看到最新的解密器集合。
/// </remarks>
public sealed class ReloadableAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
{
    private readonly object _syncRoot = new();
    private IAssetBundleDecryptorRegistry _innerRegistry;

    public ReloadableAssetBundleDecryptorRegistry(IEnumerable<string>? manifestDirectories = null)
    {
        _innerRegistry = new FileSystemAssetBundleDecryptorRegistry(manifestDirectories);
    }

    public IReadOnlyList<IAssetBundleDecryptor> GetAll()
    {
        lock (_syncRoot)
        {
            return _innerRegistry.GetAll();
        }
    }

    public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
    {
        lock (_syncRoot)
        {
            return _innerRegistry.TryGet(decryptorId, out decryptor!);
        }
    }

    public void Reload(IEnumerable<string>? manifestDirectories = null)
    {
        lock (_syncRoot)
        {
            _innerRegistry = new FileSystemAssetBundleDecryptorRegistry(manifestDirectories);
        }
    }
}
