namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 标识资源包内某个可定位资源对象的稳定键。
/// </summary>
internal readonly record struct AssetsToolsAssetObjectKey(string SerializedFileName, long PathId);
