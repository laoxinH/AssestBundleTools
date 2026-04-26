using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 新增或更新一个用户可管理的解密器定义。
/// </summary>
public sealed record UpsertAssetBundleDecryptorCommand(
    AssetBundleDecryptorDefinitionDto Definition,
    string? PreviousDecryptorId = null);
