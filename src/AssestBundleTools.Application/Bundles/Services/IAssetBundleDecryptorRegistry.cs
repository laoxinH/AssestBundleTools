namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 提供当前应用可用解密器的统一发现入口。
/// </summary>
public interface IAssetBundleDecryptorRegistry
{
    IReadOnlyList<IAssetBundleDecryptor> GetAll();

    bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor);
}
