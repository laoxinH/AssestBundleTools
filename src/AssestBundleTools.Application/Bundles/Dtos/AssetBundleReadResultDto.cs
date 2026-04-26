namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示 Reader 生成的资源包级索引结果。
/// </summary>
public sealed record AssetBundleReadResultDto(
    string DisplayName,
    string LogicalPath,
    string ParserVersion,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<AssetBundleReadAssetDto> Assets,
    IReadOnlyList<string>? SerializedFileNames = null);
