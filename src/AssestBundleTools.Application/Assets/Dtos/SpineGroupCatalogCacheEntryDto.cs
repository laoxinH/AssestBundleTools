namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示工作区 Spine 组目录的可重建派生缓存。
/// </summary>
public sealed record SpineGroupCatalogCacheEntryDto(
    int SchemaVersion,
    string WorkspaceKey,
    string CatalogFingerprint,
    string DetectorVersion,
    string BundleIndexFingerprint,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastAccessedAtUtc,
    IReadOnlyList<SpineAssetGroupListResultDto> BundleResults);
