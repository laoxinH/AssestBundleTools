using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.SettingsDiagnostics;

internal sealed class SettingsDecryptorCatalogEntryPresenter
{
    private readonly ILocalizationService _localizationService;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;

    public SettingsDecryptorCatalogEntryPresenter(
        ILocalizationService localizationService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
    }

    public SettingsDecryptorCatalogEntryItemViewModel Build(AssetBundleDecryptorCatalogEntryDto entry)
    {
        var displayName = GetDisplayName(entry.Definition.Id);
        var description = AssetBundleDecryptorDisplayText.GetDescription(
            _localizationService,
            _assetBundleDecryptorRegistry,
            entry.Definition.Id);

        var primaryBadgeText = entry.IsBuiltIn
            ? GetString(ResourceKeys.SettingsDiagnosticsDecryptorBuiltInBadge)
            : entry.IsReadOnly
                ? GetString(ResourceKeys.SettingsDiagnosticsDecryptorReadOnlyBadge)
                : GetString(ResourceKeys.SettingsDiagnosticsDecryptorUserManagedBadge);
        var secondaryBadgeText = entry.IsBuiltIn
            ? GetString(ResourceKeys.SettingsDiagnosticsDecryptorReadOnlyBadge)
            : null;

        var executablePathSummary = string.IsNullOrWhiteSpace(entry.Definition.ExecutablePath)
            ? string.Empty
            : _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsDecryptorExecutablePathFormat,
                entry.Definition.ExecutablePath);
        var manifestPathSummary = string.IsNullOrWhiteSpace(entry.ManifestFilePath)
            ? null
            : _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsDecryptorManifestPathFormat,
                entry.ManifestFilePath);

        return new SettingsDecryptorCatalogEntryItemViewModel(
            Id: entry.Definition.Id,
            DisplayName: displayName,
            VersionSummary: _localizationService.GetFormattedString(
                ResourceKeys.SettingsDiagnosticsDecryptorVersionFormat,
                string.IsNullOrWhiteSpace(entry.Definition.Version) ? "1.0.0" : entry.Definition.Version),
            Description: description,
            ExecutablePathSummary: executablePathSummary,
            ManifestPathSummary: manifestPathSummary,
            PrimaryBadgeText: primaryBadgeText,
            SecondaryBadgeText: secondaryBadgeText,
            CanEdit: !entry.IsReadOnly,
            CanDelete: !entry.IsReadOnly);
    }

    public string GetDisplayName(string decryptorId)
    {
        return AssetBundleDecryptorDisplayText.GetDisplayName(
            _localizationService,
            _assetBundleDecryptorRegistry,
            decryptorId);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
