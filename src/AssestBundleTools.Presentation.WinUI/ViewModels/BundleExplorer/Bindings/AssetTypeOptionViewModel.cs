using DomainAssetType = AssestBundleTools.Domain.Assets.AssetType;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 表示资产类型过滤下拉框中的一个可选项。
/// </summary>
public sealed record AssetTypeOptionViewModel(
    string DisplayName,
    DomainAssetType? AssetType,
    string? AssetTypeDisplayNameFilter = null,
    int? Count = null)
{
    private const char DynamicFilterSeparator = ':';

    public string DisplayText => Count.HasValue
        ? $"{DisplayName} ({Count.Value})"
        : DisplayName;

    public string? FilterKey => CreateFilterKey(AssetType, AssetTypeDisplayNameFilter);

    public bool MatchesFilterKey(string? filterKey)
    {
        return string.Equals(
            FilterKey ?? string.Empty,
            NormalizeFilterKey(filterKey),
            StringComparison.OrdinalIgnoreCase);
    }

    public static AssetTypeOptionViewModel CreateFromFilterKey(string? filterKey)
    {
        if (!TryParseFilterKey(filterKey, out var assetType, out var assetTypeDisplayNameFilter) ||
            assetType is null)
        {
            return new AssetTypeOptionViewModel(string.Empty, null);
        }

        return new AssetTypeOptionViewModel(
            ResolveFallbackDisplayName(assetType.Value, assetTypeDisplayNameFilter),
            assetType,
            assetTypeDisplayNameFilter);
    }

    public static string? CreateFilterKey(
        DomainAssetType? assetType,
        string? assetTypeDisplayNameFilter)
    {
        if (assetType is null)
        {
            return null;
        }

        var normalizedDisplayName = NormalizeDisplayName(assetTypeDisplayNameFilter);
        return assetType.Value == DomainAssetType.Unknown && normalizedDisplayName is not null
            ? $"{assetType.Value}{DynamicFilterSeparator}{normalizedDisplayName}"
            : assetType.Value.ToString();
    }

    public static bool TryParseFilterKey(
        string? filterKey,
        out DomainAssetType? assetType,
        out string? assetTypeDisplayNameFilter)
    {
        assetType = null;
        assetTypeDisplayNameFilter = null;

        var normalizedFilterKey = NormalizeFilterKey(filterKey);
        if (string.IsNullOrWhiteSpace(normalizedFilterKey))
        {
            return true;
        }

        var separatorIndex = normalizedFilterKey.IndexOf(DynamicFilterSeparator, StringComparison.Ordinal);
        var assetTypeText = separatorIndex >= 0
            ? normalizedFilterKey[..separatorIndex]
            : normalizedFilterKey;
        if (!Enum.TryParse<DomainAssetType>(assetTypeText, ignoreCase: true, out var parsedAssetType))
        {
            return false;
        }

        assetType = parsedAssetType;
        assetTypeDisplayNameFilter = separatorIndex >= 0
            ? NormalizeDisplayName(normalizedFilterKey[(separatorIndex + 1)..])
            : null;
        return true;
    }

    private static string? NormalizeFilterKey(string? filterKey) =>
        string.IsNullOrWhiteSpace(filterKey) ? string.Empty : filterKey.Trim();

    private static string? NormalizeDisplayName(string? displayName) =>
        string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

    private static string ResolveFallbackDisplayName(
        DomainAssetType assetType,
        string? assetTypeDisplayNameFilter)
    {
        return string.IsNullOrWhiteSpace(assetTypeDisplayNameFilter)
            ? assetType.ToString()
            : assetTypeDisplayNameFilter.Trim();
    }
}
