using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

/// <summary>
/// 表示设置页中的一条解密器目录项摘要。
/// </summary>
public sealed record SettingsDecryptorCatalogEntryItemViewModel(
    string Id,
    string DisplayName,
    string VersionSummary,
    string Description,
    string ExecutablePathSummary,
    string? ManifestPathSummary,
    string PrimaryBadgeText,
    string? SecondaryBadgeText,
    bool CanEdit,
    bool CanDelete)
{
    public Visibility DescriptionVisibility =>
        string.IsNullOrWhiteSpace(Description) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ExecutablePathVisibility =>
        string.IsNullOrWhiteSpace(ExecutablePathSummary) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ManifestPathVisibility =>
        string.IsNullOrWhiteSpace(ManifestPathSummary) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SecondaryBadgeVisibility =>
        string.IsNullOrWhiteSpace(SecondaryBadgeText) ? Visibility.Collapsed : Visibility.Visible;
}
