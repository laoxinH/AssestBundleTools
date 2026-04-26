using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Services;

/// <summary>
/// 提供解密器目录项的加载与用户管理清单写入能力。
/// </summary>
public interface IAssetBundleDecryptorCatalogStore
{
    IReadOnlyList<AssetBundleDecryptorCatalogEntryDto> Load();

    void SaveUserManagedDecryptor(
        AssetBundleDecryptorDefinitionDto definition,
        string? previousDecryptorId = null);

    void DeleteUserManagedDecryptor(string decryptorId);
}
