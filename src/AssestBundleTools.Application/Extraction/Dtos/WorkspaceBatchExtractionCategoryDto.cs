using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 表示工作区级批量提取的导出类别。
/// </summary>
public sealed record WorkspaceBatchExtractionCategoryDto(
    WorkspaceBatchExtractionCategoryKind Kind,
    AssetType? AssetType = null,
    string? AssetTypeDisplayName = null)
{
    public static WorkspaceBatchExtractionCategoryDto All { get; } =
        new(WorkspaceBatchExtractionCategoryKind.All);

    public static WorkspaceBatchExtractionCategoryDto SpineGroup { get; } =
        new(WorkspaceBatchExtractionCategoryKind.SpineGroup);

    public static WorkspaceBatchExtractionCategoryDto FromAssetType(
        AssetType assetType,
        string? assetTypeDisplayName = null) =>
        new(
            WorkspaceBatchExtractionCategoryKind.UnityAssetType,
            assetType,
            NormalizeAssetTypeDisplayName(assetTypeDisplayName));

    private static string? NormalizeAssetTypeDisplayName(string? assetTypeDisplayName) =>
        string.IsNullOrWhiteSpace(assetTypeDisplayName)
            ? null
            : assetTypeDisplayName.Trim();
}

public enum WorkspaceBatchExtractionCategoryKind
{
    All,
    UnityAssetType,
    SpineGroup
}
