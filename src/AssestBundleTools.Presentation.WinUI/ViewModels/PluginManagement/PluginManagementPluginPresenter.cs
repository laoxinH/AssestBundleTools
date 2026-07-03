using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.PluginManagement;

internal sealed class PluginManagementPluginPresenter
{
    private readonly ILocalizationService _localizationService;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;

    public PluginManagementPluginPresenter(
        ILocalizationService localizationService,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
    }

    public PluginManagementPluginItemViewModel BuildAutoDetectDecryptor()
    {
        var id = AssetBundleDecryptorIds.BuiltInAutoDetect;
        var displayName = AssetBundleDecryptorDisplayText.GetDisplayName(_localizationService, id);
        var description = AssetBundleDecryptorDisplayText.GetDescription(
            _localizationService,
            _assetBundleDecryptorRegistry,
            id);
        var details = new[]
        {
            Detail(ResourceKeys.PluginManagementDetailIdLabel, id),
            Detail(ResourceKeys.PluginManagementDetailVersionLabel, "1.0.0"),
            Detail(ResourceKeys.PluginManagementDetailTypeLabel, GetString(ResourceKeys.PluginManagementTypeImportDecryptor)),
            Detail(ResourceKeys.PluginManagementDetailAutoDetectionLabel, GetString(ResourceKeys.PluginManagementValueEnabled)),
            Detail(ResourceKeys.PluginManagementDetailFallbackLabel, GetString(ResourceKeys.PluginManagementValueDisabled))
        };

        return new PluginManagementPluginItemViewModel(
            Id: id,
            DisplayName: displayName,
            TypeText: GetString(ResourceKeys.PluginManagementTypeImportDecryptor),
            ResourceKindText: GetString(ResourceKeys.PluginManagementScopeImportDecryptor),
            Description: description,
            PrimaryBadgeText: GetString(ResourceKeys.PluginManagementBadgeBuiltIn),
            SecondaryBadgeText: GetString(ResourceKeys.PluginManagementStatusAvailable),
            StatusFilterKey: PluginManagementFilterKeys.Available,
            TypeFilterKey: PluginManagementFilterKeys.ImportDecryptor,
            ResourceTypeFilterKeys: [PluginManagementFilterKeys.ImportDecryptor],
            SearchText: BuildSearchText(id, displayName, description),
            IsDecryptor: true,
            IsReadOnly: true,
            CanEdit: false,
            CanDelete: false,
            DetailItems: details,
            DiagnosticText: GetString(ResourceKeys.PluginManagementDiagnosticNotRun),
            DecryptorEntry: null,
            ResourceProcessorEntry: null);
    }

    public PluginManagementPluginItemViewModel BuildDecryptor(AssetBundleDecryptorCatalogEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var displayName = AssetBundleDecryptorDisplayText.GetDisplayName(
            _localizationService,
            _assetBundleDecryptorRegistry,
            entry.Definition.Id);
        var description = AssetBundleDecryptorDisplayText.GetDescription(
            _localizationService,
            _assetBundleDecryptorRegistry,
            entry.Definition.Id);
        var primaryBadge = entry.IsBuiltIn
            ? GetString(ResourceKeys.PluginManagementBadgeBuiltIn)
            : entry.IsReadOnly
                ? GetString(ResourceKeys.PluginManagementBadgeReadOnly)
                : GetString(ResourceKeys.PluginManagementBadgeUserManaged);
        var secondaryBadge = GetString(ResourceKeys.PluginManagementStatusAvailable);
        var details = new List<PluginManagementDetailItemViewModel>
        {
            Detail(ResourceKeys.PluginManagementDetailIdLabel, entry.Definition.Id),
            Detail(ResourceKeys.PluginManagementDetailVersionLabel, NormalizeVersion(entry.Definition.Version)),
            Detail(ResourceKeys.PluginManagementDetailTypeLabel, GetString(ResourceKeys.PluginManagementTypeImportDecryptor)),
            Detail(ResourceKeys.PluginManagementDetailAutoDetectionLabel, FormatBoolean(entry.Definition.SupportsAutoDetection)),
            Detail(ResourceKeys.PluginManagementDetailFallbackLabel, FormatBoolean(entry.Definition.IsFallback)),
            Detail(ResourceKeys.PluginManagementDetailTimeoutLabel, FormatTimeout(entry.Definition.TimeoutMs))
        };
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailExecutablePathLabel, entry.Definition.ExecutablePath);
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailWorkingDirectoryLabel, entry.Definition.WorkingDirectoryPath);
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailManifestPathLabel, entry.ManifestFilePath);
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailProbeArgumentsLabel, JoinArguments(entry.Definition.ProbeArguments));
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailDecryptArgumentsLabel, JoinArguments(entry.Definition.DecryptArguments));

        return new PluginManagementPluginItemViewModel(
            Id: entry.Definition.Id,
            DisplayName: displayName,
            TypeText: GetString(ResourceKeys.PluginManagementTypeImportDecryptor),
            ResourceKindText: GetString(ResourceKeys.PluginManagementScopeImportDecryptor),
            Description: description,
            PrimaryBadgeText: primaryBadge,
            SecondaryBadgeText: secondaryBadge,
            StatusFilterKey: PluginManagementFilterKeys.Available,
            TypeFilterKey: PluginManagementFilterKeys.ImportDecryptor,
            ResourceTypeFilterKeys: [PluginManagementFilterKeys.ImportDecryptor],
            SearchText: BuildSearchText(
                entry.Definition.Id,
                displayName,
                description,
                entry.ManifestFilePath,
                entry.Definition.ExecutablePath),
            IsDecryptor: true,
            IsReadOnly: entry.IsReadOnly,
            CanEdit: !entry.IsReadOnly,
            CanDelete: !entry.IsReadOnly,
            DetailItems: details,
            DiagnosticText: GetString(ResourceKeys.PluginManagementDiagnosticNotRun),
            DecryptorEntry: entry,
            ResourceProcessorEntry: null);
    }

    public PluginManagementPluginItemViewModel BuildResourceProcessor(ResourceProcessorCatalogEntryDto entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var resourceKindText = FormatResourceKinds(entry.ResourceKinds);
        var displayName = ResolveResourceProcessorDisplayName(entry, resourceKindText);
        var description = string.IsNullOrWhiteSpace(entry.Description)
            ? _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDescriptionFormat,
                resourceKindText)
            : entry.Description;
        var details = new List<PluginManagementDetailItemViewModel>
        {
            Detail(ResourceKeys.PluginManagementDetailIdLabel, entry.Id),
            Detail(ResourceKeys.PluginManagementDetailVersionLabel, NormalizeVersion(entry.Version)),
            Detail(ResourceKeys.PluginManagementDetailTypeLabel, GetString(ResourceKeys.PluginManagementTypeExportProcessor)),
            Detail(ResourceKeys.PluginManagementDetailResourceKindsLabel, resourceKindText),
            Detail(ResourceKeys.PluginManagementDetailTimeoutLabel, FormatTimeout(entry.TimeoutMs)),
            Detail(ResourceKeys.PluginManagementDetailArgumentsLabel, JoinArguments(entry.ProcessArguments))
        };
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailExecutablePathLabel, entry.ExecutablePath);
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailWorkingDirectoryLabel, entry.WorkingDirectoryPath);
        AddDetailIfPresent(details, ResourceKeys.PluginManagementDetailManifestPathLabel, entry.ManifestFilePath);

        return new PluginManagementPluginItemViewModel(
            Id: entry.Id,
            DisplayName: displayName,
            TypeText: GetString(ResourceKeys.PluginManagementTypeExportProcessor),
            ResourceKindText: resourceKindText,
            Description: description,
            PrimaryBadgeText: entry.IsBuiltIn
                ? GetString(ResourceKeys.PluginManagementBadgeBuiltIn)
                : entry.IsReadOnly
                    ? GetString(ResourceKeys.PluginManagementBadgeReadOnly)
                    : GetString(ResourceKeys.PluginManagementBadgeUserManaged),
            SecondaryBadgeText: ResolveStatusText(entry.Status),
            StatusFilterKey: ResolveStatusFilterKey(entry.Status),
            TypeFilterKey: PluginManagementFilterKeys.ExportProcessor,
            ResourceTypeFilterKeys: ResolveResourceTypeFilterKeys(entry.ResourceKinds),
            SearchText: BuildSearchText(
                entry.Id,
                displayName,
                description,
                entry.ManifestFilePath,
                entry.ExecutablePath,
                resourceKindText),
            IsDecryptor: false,
            IsReadOnly: entry.IsReadOnly,
            CanEdit: !entry.IsReadOnly,
            CanDelete: !entry.IsReadOnly,
            DetailItems: details,
            DiagnosticText: GetString(ResourceKeys.PluginManagementDiagnosticNotRun),
            DecryptorEntry: null,
            ResourceProcessorEntry: entry);
    }

    public string GetDecryptorDisplayName(string decryptorId)
    {
        return AssetBundleDecryptorDisplayText.GetDisplayName(
            _localizationService,
            _assetBundleDecryptorRegistry,
            decryptorId);
    }

    public string GetResourceKindDisplayName(string resourceKind)
    {
        return NormalizeResourceKind(resourceKind) switch
        {
            "texture2d" => GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D),
            "audio" => GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorAudio),
            "atlas" => GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorAtlas),
            "skel" => GetString(ResourceKeys.WorkspaceExportDialogResourceProcessorSkel),
            _ => resourceKind
        };
    }

    private string ResolveResourceProcessorDisplayName(
        ResourceProcessorCatalogEntryDto entry,
        string resourceKindText)
    {
        if (entry.IsBuiltIn)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.PluginManagementResourceProcessorDisplayNameFormat,
                resourceKindText);
        }

        return string.IsNullOrWhiteSpace(entry.DisplayName)
            ? entry.Id
            : entry.DisplayName;
    }

    private static IReadOnlyList<string> ResolveResourceTypeFilterKeys(IReadOnlyList<string> resourceKinds)
    {
        var filterKeys = new List<string>();
        foreach (var resourceKind in resourceKinds)
        {
            var filterKey = NormalizeResourceKind(resourceKind) switch
            {
                "texture2d" => PluginManagementFilterKeys.Texture2D,
                "audio" => PluginManagementFilterKeys.Audio,
                "atlas" => PluginManagementFilterKeys.Atlas,
                "skel" => PluginManagementFilterKeys.Skel,
                _ => null
            };
            if (filterKey is not null)
            {
                filterKeys.Add(filterKey);
            }
        }

        return filterKeys.Count == 0
            ? [PluginManagementFilterKeys.ExportProcessor]
            : filterKeys.Distinct(StringComparer.Ordinal).ToArray();
    }

    private string FormatResourceKinds(IReadOnlyList<string> resourceKinds)
    {
        return resourceKinds.Count == 0
            ? GetString(ResourceKeys.PluginManagementResourceKindUnknown)
            : string.Join(
                ", ",
                resourceKinds
                    .Where(static resourceKind => !string.IsNullOrWhiteSpace(resourceKind))
                    .Select(GetResourceKindDisplayName));
    }

    private string ResolveStatusText(ResourceProcessorStatus status)
    {
        return status switch
        {
            ResourceProcessorStatus.Warning => GetString(ResourceKeys.PluginManagementStatusWarning),
            ResourceProcessorStatus.Unavailable => GetString(ResourceKeys.PluginManagementStatusUnavailable),
            _ => GetString(ResourceKeys.PluginManagementStatusAvailable)
        };
    }

    private static string ResolveStatusFilterKey(ResourceProcessorStatus status)
    {
        return status switch
        {
            ResourceProcessorStatus.Warning => PluginManagementFilterKeys.Warning,
            ResourceProcessorStatus.Unavailable => PluginManagementFilterKeys.Unavailable,
            _ => PluginManagementFilterKeys.Available
        };
    }

    private PluginManagementDetailItemViewModel Detail(string labelResourceKey, string value) =>
        new(GetString(labelResourceKey), value);

    private void AddDetailIfPresent(
        ICollection<PluginManagementDetailItemViewModel> details,
        string labelResourceKey,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        details.Add(Detail(labelResourceKey, value.Trim()));
    }

    private string FormatBoolean(bool value) =>
        value ? GetString(ResourceKeys.PluginManagementValueEnabled) : GetString(ResourceKeys.PluginManagementValueDisabled);

    private string FormatTimeout(int timeoutMs) =>
        _localizationService.GetFormattedString(ResourceKeys.PluginManagementTimeoutFormat, Math.Max(0, timeoutMs));

    private static string NormalizeVersion(string? version) =>
        string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim();

    private static string JoinArguments(IReadOnlyList<string> arguments) =>
        arguments.Count == 0
            ? string.Empty
            : string.Join(" ", arguments.Where(static argument => !string.IsNullOrWhiteSpace(argument)));

    private static string BuildSearchText(params string?[] values) =>
        string.Join(
            " ",
            values
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!.Trim()));

    private static string NormalizeResourceKind(string resourceKind) =>
        resourceKind.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
