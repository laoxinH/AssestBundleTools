using AssestBundleTools.Application.Bundles.Dtos;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;

/// <summary>
/// 表示插件管理页目录和详情面板中的一条插件摘要。
/// </summary>
public sealed record PluginManagementPluginItemViewModel(
    string Id,
    string DisplayName,
    string TypeText,
    string ResourceKindText,
    string Description,
    string PrimaryBadgeText,
    string SecondaryBadgeText,
    string StatusFilterKey,
    string TypeFilterKey,
    IReadOnlyList<string> ResourceTypeFilterKeys,
    string SearchText,
    bool IsDecryptor,
    bool IsReadOnly,
    bool CanEdit,
    bool CanDelete,
    IReadOnlyList<PluginManagementDetailItemViewModel> DetailItems,
    string DiagnosticText,
    AssetBundleDecryptorCatalogEntryDto? DecryptorEntry,
    ResourceProcessorCatalogEntryDto? ResourceProcessorEntry)
{
    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SecondaryBadgeVisibility =>
        string.IsNullOrWhiteSpace(SecondaryBadgeText) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility EditVisibility => IsDecryptor ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DeleteVisibility => IsDecryptor ? Visibility.Visible : Visibility.Collapsed;

    public bool MatchesTypeFilter(string filterKey)
    {
        if (string.Equals(filterKey, PluginManagementFilterKeys.All, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(filterKey, PluginManagementFilterKeys.ImportDecryptor, StringComparison.Ordinal))
        {
            return IsDecryptor;
        }

        if (string.Equals(filterKey, PluginManagementFilterKeys.ExportProcessor, StringComparison.Ordinal))
        {
            return !IsDecryptor;
        }

        return ResourceTypeFilterKeys.Any(resourceTypeFilterKey =>
            string.Equals(resourceTypeFilterKey, filterKey, StringComparison.Ordinal));
    }

    public bool MatchesStatusFilter(string filterKey)
    {
        if (string.Equals(filterKey, PluginManagementFilterKeys.All, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(filterKey, PluginManagementFilterKeys.ReadOnly, StringComparison.Ordinal))
        {
            return IsReadOnly;
        }

        if (string.Equals(filterKey, PluginManagementFilterKeys.UserManaged, StringComparison.Ordinal))
        {
            return !IsReadOnly;
        }

        return string.Equals(StatusFilterKey, filterKey, StringComparison.Ordinal);
    }
}

internal static class PluginManagementFilterKeys
{
    public const string All = "all";
    public const string ImportDecryptor = "import-decryptor";
    public const string ExportProcessor = "export-processor";
    public const string Texture2D = "resource-kind:texture2d";
    public const string Audio = "resource-kind:audio";
    public const string Atlas = "resource-kind:atlas";
    public const string Skel = "resource-kind:skel";
    public const string Available = "available";
    public const string Warning = "warning";
    public const string Unavailable = "unavailable";
    public const string ReadOnly = "read-only";
    public const string UserManaged = "user-managed";
}
