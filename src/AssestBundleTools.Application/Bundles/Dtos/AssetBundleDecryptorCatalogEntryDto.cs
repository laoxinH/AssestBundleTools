namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示解密器目录中的一项可管理条目。
/// </summary>
public sealed record AssetBundleDecryptorCatalogEntryDto(
    AssetBundleDecryptorDefinitionDto Definition,
    AssetBundleDecryptorKind Kind,
    bool IsBuiltIn,
    bool IsReadOnly,
    string? ManifestFilePath);
