using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class MonoBehaviourPreviewDisplayFormatter
{
    private const string UnknownValue = "-";
    private const string ObjectCardKey = "mono-behaviour-object";
    private const string ScriptCardKey = "mono-behaviour-script";
    private const string FieldGroupsCardKey = "mono-behaviour-field-groups";
    private const string ContainersCardKey = "mono-behaviour-containers";
    private const string FieldsCardKey = "mono-behaviour-fields";
    private const string ReferencesCardKey = "mono-behaviour-references";

    private readonly ILocalizationService _localizationService;

    public MonoBehaviourPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
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
            DetailCards: BuildDetailCards(monoBehaviour, details));
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

    private IReadOnlyList<InspectorInfoCardViewModel> BuildDetailCards(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        AssetDetailsDto details)
    {
        var cards = new List<InspectorInfoCardViewModel>
        {
            CreateCard(
                ObjectCardKey,
                ResourceKeys.InspectorMonoBehaviourPreviewObjectCardTitle,
                CreateObjectRows(monoBehaviour),
                isExpanded: false),
            CreateCard(
                ScriptCardKey,
                ResourceKeys.InspectorMonoBehaviourPreviewScriptCardTitle,
                CreateScriptRows(monoBehaviour.Script, details),
                isExpanded: true),
            CreateCard(
                FieldGroupsCardKey,
                ResourceKeys.InspectorMonoBehaviourPreviewFieldGroupsCardTitle,
                CreateFieldGroupRows(monoBehaviour),
                isExpanded: true),
            CreateCard(
                ContainersCardKey,
                ResourceKeys.InspectorMonoBehaviourPreviewContainersCardTitle,
                CreateContainerRows(monoBehaviour),
                isExpanded: true),
            CreateCard(
                FieldsCardKey,
                ResourceKeys.InspectorMonoBehaviourPreviewFieldsCardTitle,
                CreateFieldRows(monoBehaviour),
                isExpanded: true)
        };

        var referenceRows = CreateReferenceRows(monoBehaviour, details);
        if (referenceRows.Count > 0)
        {
            cards.Add(CreateCard(
                ReferencesCardKey,
                ResourceKeys.InspectorMonoBehaviourPreviewReferencesCardTitle,
                referenceRows,
                isExpanded: true));
        }

        return cards;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateObjectRows(AssetPreviewMonoBehaviourDto monoBehaviour)
    {
        return
        [
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewNameLabel), FormatText(monoBehaviour.Name)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewEnabledLabel), FormatBoolean(monoBehaviour.IsEnabled)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewFieldCountLabel), monoBehaviour.FieldCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScalarFieldCountLabel), monoBehaviour.ScalarFieldCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewArrayFieldCountLabel), monoBehaviour.ArrayFieldCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewReferenceCountLabel), monoBehaviour.ReferenceCount.ToString(CultureInfo.CurrentCulture))
        ];
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateScriptRows(
        AssetPreviewMonoBehaviourScriptDto? script,
        AssetDetailsDto details)
    {
        if (script is null)
        {
            return
            [
                new(string.Empty, GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptMissingText))
            ];
        }

        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        var scriptDependency = details.Dependencies
            .Where(dependency => dependency.PathId == script.PathId)
            .OrderBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        return
        [
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptNameLabel), FormatScriptName(script)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptClassLabel), FormatText(script.ClassName)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptNamespaceLabel), FormatText(script.Namespace)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptAssemblyLabel), FormatText(script.AssemblyName)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptSerializedFileLabel), FormatText(script.SerializedFileName)),
            new(GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptExternalPathLabel), FormatText(script.ExternalReferencePath)),
            CreateLinkedRow(
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewScriptPathIdLabel),
                FormatInteger(script.PathId),
                scriptDependency,
                openButtonText,
                openButtonToolTipText)
        ];
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateFieldGroupRows(AssetPreviewMonoBehaviourDto monoBehaviour)
    {
        var fieldGroups = GetFieldGroups(monoBehaviour)
            .Where(static group => !group.IsUnityBuiltin)
            .ToArray();
        return fieldGroups.Length == 0
            ? [new InspectorInfoRowViewModel(string.Empty, GetString(ResourceKeys.InspectorMonoBehaviourPreviewEmptyFieldsText))]
            : fieldGroups
                .Select(group => new InspectorInfoRowViewModel(
                    FormatText(group.Name),
                    _localizationService.GetFormattedString(
                        ResourceKeys.InspectorMonoBehaviourPreviewFieldGroupSummaryFormat,
                        group.FieldCount.ToString(CultureInfo.CurrentCulture),
                        group.ScalarFieldCount.ToString(CultureInfo.CurrentCulture),
                        group.ArrayFieldCount.ToString(CultureInfo.CurrentCulture),
                        group.ReferenceCount.ToString(CultureInfo.CurrentCulture))))
                .ToArray();
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateContainerRows(AssetPreviewMonoBehaviourDto monoBehaviour)
    {
        var containers = GetContainers(monoBehaviour);
        return containers.Count == 0
            ? [new InspectorInfoRowViewModel(string.Empty, GetString(ResourceKeys.InspectorMonoBehaviourPreviewEmptyFieldsText))]
            : containers
                .Select(container => new InspectorInfoRowViewModel(
                    FormatText(container.Path),
                    _localizationService.GetFormattedString(
                        ResourceKeys.InspectorMonoBehaviourPreviewContainerSummaryFormat,
                        FormatText(container.TypeName),
                        container.ChildCount.ToString(CultureInfo.CurrentCulture),
                        container.Depth.ToString(CultureInfo.CurrentCulture))))
                .ToArray();
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateFieldRows(AssetPreviewMonoBehaviourDto monoBehaviour)
    {
        var rows = new List<InspectorInfoRowViewModel>();
        if (monoBehaviour.Fields.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewEmptyFieldsText)));
            return rows;
        }

        foreach (var field in monoBehaviour.Fields)
        {
            rows.Add(new InspectorInfoRowViewModel(
                FormatFieldLabel(field),
                FormatFieldValue(field)));
        }

        if (monoBehaviour.IsFieldListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewFieldsTruncatedText)));
        }

        return rows;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateReferenceRows(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        AssetDetailsDto details)
    {
        var rows = new List<InspectorInfoRowViewModel>();
        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        foreach (var reference in monoBehaviour.References)
        {
            var resolvedDependency = MonoBehaviourPreviewDependencyResolver.ResolveReference(reference, details);
            rows.Add(new InspectorInfoRowViewModel(
                FormatReferenceLabel(reference),
                FormatReferenceValue(reference, resolvedDependency),
                resolvedDependency is null
                    ? null
                    : MonoBehaviourPreviewDependencyResolver.CreateNavigationTarget(resolvedDependency),
                openButtonText,
                openButtonToolTipText));
        }

        if (monoBehaviour.IsReferenceListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorMonoBehaviourPreviewReferencesTruncatedText)));
        }

        return rows;
    }

    private string FormatReferenceValue(
        AssetPreviewMonoBehaviourReferenceDto reference,
        AssetDependencyDto? resolvedDependency)
    {
        var targetText = _localizationService.GetFormattedString(
            ResourceKeys.InspectorMonoBehaviourPreviewReferenceTargetFormat,
            FormatReferenceTargetName(reference),
            FormatInteger(reference.PathId));
        if (resolvedDependency is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorMonoBehaviourPreviewReferenceResolvedFormat,
                targetText,
                resolvedDependency.Name,
                MonoBehaviourPreviewDependencyResolver.ResolveAssetTypeDisplayName(resolvedDependency));
        }

        return _localizationService.GetFormattedString(
            reference.IsExternal
                ? ResourceKeys.InspectorMonoBehaviourPreviewReferenceExternalUnresolvedFormat
                : ResourceKeys.InspectorMonoBehaviourPreviewReferenceInternalUnresolvedFormat,
            targetText);
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

    private static IReadOnlyList<AssetPreviewMonoBehaviourContainerDto> GetContainers(
        AssetPreviewMonoBehaviourDto monoBehaviour) =>
        monoBehaviour.Containers ?? Array.Empty<AssetPreviewMonoBehaviourContainerDto>();

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

    private string FormatFieldLabel(AssetPreviewMonoBehaviourFieldDto field) =>
        string.IsNullOrWhiteSpace(field.Path) ? FormatText(field.Name) : field.Path.Trim();

    private string FormatReferenceLabel(AssetPreviewMonoBehaviourReferenceDto reference) =>
        string.IsNullOrWhiteSpace(reference.Path) ? FormatText(reference.Name) : reference.Path.Trim();

    private string FormatFieldValue(AssetPreviewMonoBehaviourFieldDto field)
    {
        if (field.ChildCount.HasValue)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorMonoBehaviourPreviewFieldArrayCountFormat,
                FormatText(field.TypeName),
                field.ChildCount.Value.ToString(CultureInfo.CurrentCulture));
        }

        var value = field.ValueKind switch
        {
            AssetPreviewInfoValueKind.Boolean => FormatBoolean(field.BooleanValue),
            AssetPreviewInfoValueKind.Decimal => FormatNumber(field.DoubleValue),
            AssetPreviewInfoValueKind.Integer => FormatInteger(field.Int64Value),
            AssetPreviewInfoValueKind.Text => FormatText(field.StringValue),
            _ => UnknownValue
        };
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMonoBehaviourPreviewFieldValueFormat,
            FormatText(field.TypeName),
            value);
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

    private static string FormatReferenceTargetName(AssetPreviewMonoBehaviourReferenceDto reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.TargetName))
        {
            return reference.TargetName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(reference.TypeName))
        {
            return reference.TypeName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(reference.SerializedFileName))
        {
            return reference.SerializedFileName.Trim();
        }

        return UnknownValue;
    }

    private InspectorInfoCardViewModel CreateCard(
        string key,
        string titleResourceKey,
        IReadOnlyList<InspectorInfoRowViewModel> rows,
        bool isExpanded)
    {
        return new InspectorInfoCardViewModel(
            key,
            GetString(titleResourceKey),
            GetString(ResourceKeys.InspectorInfoCardCopyToolTip),
            _localizationService.GetFormattedString(
                ResourceKeys.InspectorInfoCardItemCountFormat,
                rows.Count.ToString(CultureInfo.CurrentCulture)),
            isExpanded,
            rows);
    }

    private static InspectorInfoRowViewModel CreateLinkedRow(
        string label,
        string value,
        AssetDependencyDto? dependency,
        string openButtonText,
        string openButtonToolTipText)
    {
        return new InspectorInfoRowViewModel(
            label,
            value,
            dependency is null ? null : MonoBehaviourPreviewDependencyResolver.CreateNavigationTarget(dependency),
            openButtonText,
            openButtonToolTipText);
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

    private static string FormatInteger(long? value) =>
        value.HasValue
            ? value.Value.ToString(CultureInfo.CurrentCulture)
            : UnknownValue;

    private static string FormatNumber(double? value) =>
        value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
            : UnknownValue;

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
