using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class MonoBehaviourPreviewDisplayFormatter
{
    private const string UnknownValue = "-";

    private readonly ILocalizationService _localizationService;
    private readonly MonoBehaviourPreviewInfoCardBuilder _infoCardBuilder;

    public MonoBehaviourPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _infoCardBuilder = new MonoBehaviourPreviewInfoCardBuilder(_localizationService);
    }

    public MonoBehaviourPreviewDisplayState BuildDisplayState(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(monoBehaviour);
        ArgumentNullException.ThrowIfNull(details);

        return new MonoBehaviourPreviewDisplayState(
            AssetNameText: ResolveAssetName(monoBehaviour, details.Name),
            ScriptText: BuildScriptText(monoBehaviour.Script),
            StateText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMonoBehaviourPreviewStateFormat,
                FormatBoolean(monoBehaviour.IsEnabled),
                monoBehaviour.ReferenceCount.ToString(CultureInfo.CurrentCulture),
                monoBehaviour.ScalarFieldCount.ToString(CultureInfo.CurrentCulture)),
            FieldSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMonoBehaviourPreviewFieldSummaryFormat,
                monoBehaviour.ScalarFieldCount.ToString(CultureInfo.CurrentCulture),
                monoBehaviour.ArrayFieldCount.ToString(CultureInfo.CurrentCulture)),
            ReferenceSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMonoBehaviourPreviewReferenceSummaryFormat,
                CountResolvedReferences(monoBehaviour, details).ToString(CultureInfo.CurrentCulture),
                monoBehaviour.ReferenceCount.ToString(CultureInfo.CurrentCulture)),
            SummaryCards: BuildSummaryCards(monoBehaviour, details),
            DetailCards: _infoCardBuilder.Build(monoBehaviour, details));
    }

    private IReadOnlyList<MonoBehaviourPreviewSummaryCardDisplayState> BuildSummaryCards(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        AssetDetailsDto details)
    {
        return
        [
            new(
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewSummaryCardScriptTitle),
                FormatScriptName(monoBehaviour.Script),
                FormatText(monoBehaviour.Script?.AssemblyName)),
            new(
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewSummaryCardFieldsTitle),
                monoBehaviour.ScalarFieldCount.ToString(CultureInfo.CurrentCulture),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorMonoBehaviourPreviewSummaryFieldDetailFormat,
                    monoBehaviour.FieldCount.ToString(CultureInfo.CurrentCulture),
                    monoBehaviour.ArrayFieldCount.ToString(CultureInfo.CurrentCulture),
                    CountUserFieldGroups(monoBehaviour).ToString(CultureInfo.CurrentCulture))),
            new(
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewSummaryCardReferencesTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorMonoBehaviourPreviewResolvedReferenceCountFormat,
                    CountResolvedReferences(monoBehaviour, details).ToString(CultureInfo.CurrentCulture),
                    monoBehaviour.ReferenceCount.ToString(CultureInfo.CurrentCulture)),
                BuildReferenceTypeSummary(monoBehaviour.References))
        ];
    }

    private string BuildScriptText(AssetPreviewMonoBehaviourScriptDto? script)
    {
        if (script is null)
        {
            return GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptMissingText);
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMonoBehaviourPreviewScriptSummaryFormat,
            FormatScriptName(script),
            FormatText(script.AssemblyName));
    }

    private int CountResolvedReferences(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        AssetDetailsDto details)
    {
        return monoBehaviour.References.Count(reference =>
            MonoBehaviourPreviewDependencyResolver.ResolveReference(reference, details) is not null);
    }

    private static int CountUserFieldGroups(AssetPreviewMonoBehaviourDto monoBehaviour) =>
        GetFieldGroups(monoBehaviour).Count(static group => !group.IsUnityBuiltin);

    private static IReadOnlyList<AssetPreviewMonoBehaviourFieldGroupDto> GetFieldGroups(
        AssetPreviewMonoBehaviourDto monoBehaviour) =>
        monoBehaviour.FieldGroups ?? Array.Empty<AssetPreviewMonoBehaviourFieldGroupDto>();

    private string BuildReferenceTypeSummary(IReadOnlyList<AssetPreviewMonoBehaviourReferenceDto> references)
    {
        if (references.Count == 0)
        {
            return GetString(ResourceKeys.InspectorMonoBehaviourPreviewEmptyReferencesText);
        }

        return string.Join(
            ", ",
            references
                .GroupBy(static reference => string.IsNullOrWhiteSpace(reference.TypeName) ? UnknownValue : reference.TypeName.Trim())
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(group => _localizationService.GetFormattedString(
                    ResourceKeys.InspectorMonoBehaviourPreviewReferenceTypeCountFormat,
                    group.Key,
                    group.Count().ToString(CultureInfo.CurrentCulture))));
    }

    private static string ResolveAssetName(AssetPreviewMonoBehaviourDto monoBehaviour, string assetName)
    {
        if (!string.IsNullOrWhiteSpace(monoBehaviour.Name))
        {
            return monoBehaviour.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(assetName)
            ? string.Empty
            : assetName.Trim();
    }

    private string FormatScriptName(AssetPreviewMonoBehaviourScriptDto? script)
    {
        if (script is null)
        {
            return UnknownValue;
        }

        if (!string.IsNullOrWhiteSpace(script.ClassName))
        {
            return script.ClassName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(script.Name))
        {
            return script.Name.Trim();
        }

        return script.PathId.HasValue
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorMonoBehaviourPreviewScriptUnresolvedNameFormat,
                script.PathId.Value.ToString(CultureInfo.CurrentCulture))
            : UnknownValue;
    }

    private string FormatBoolean(bool? value)
    {
        if (!value.HasValue)
        {
            return UnknownValue;
        }

        return GetString(value.Value
            ? ResourceKeys.InspectorBooleanTrue
            : ResourceKeys.InspectorBooleanFalse);
    }

    private static string FormatText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? UnknownValue
            : value.Trim();

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);
}

public sealed record MonoBehaviourPreviewDisplayState(
    string AssetNameText,
    string ScriptText,
    string StateText,
    string FieldSummaryText,
    string ReferenceSummaryText,
    IReadOnlyList<MonoBehaviourPreviewSummaryCardDisplayState> SummaryCards,
    IReadOnlyList<InspectorInfoCardViewModel>? DetailCards = null);

public sealed record MonoBehaviourPreviewSummaryCardDisplayState(
    string TitleText,
    string ValueText,
    string DetailText);
