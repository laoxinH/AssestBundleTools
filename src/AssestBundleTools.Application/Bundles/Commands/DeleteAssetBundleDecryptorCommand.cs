namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 删除一个用户可管理的解密器定义。
/// </summary>
public sealed record DeleteAssetBundleDecryptorCommand(string DecryptorId);
