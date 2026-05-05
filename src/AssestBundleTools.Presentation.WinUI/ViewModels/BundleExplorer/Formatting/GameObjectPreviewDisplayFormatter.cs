using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class GameObjectPreviewDisplayFormatter
{
    private const string UnknownValue = "-";
    private const string ObjectCardKey = "game-object-object";
    private const string TransformCardKey = "game-object-transform";
    private const string ComponentsCardKey = "game-object-components";
    private const string ComponentReferencesCardKey = "game-object-component-references";

    private readonly ILocalizationService _localizationService;
    private readonly GameObjectPreviewReferenceCardBuilder _referenceCardBuilder;
    private readonly GameObjectPreviewSummaryCardBuilder _summaryCardBuilder;

    public GameObjectPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _referenceCardBuilder = new GameObjectPreviewReferenceCardBuilder(_localizationService);
        _summaryCardBuilder = new GameObjectPreviewSummaryCardBuilder(_localizationService);
    }

    public GameObjectPreviewDisplayState BuildDisplayState(
        AssetPreviewGameObjectDto gameObject,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        ArgumentNullException.ThrowIfNull(details);

        return new GameObjectPreviewDisplayState(
            AssetNameText: ResolveAssetName(gameObject, details.Name),
            StateText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewStateFormat,
                FormatBoolean(gameObject.IsActive),
                FormatInteger(gameObject.Layer),
                FormatText(gameObject.Tag)),
            ComponentSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentSummaryFormat,
                gameObject.ComponentCount.ToString(CultureInfo.CurrentCulture),
                BuildComponentTypeSummary(gameObject.Components)),
            TransformSummaryText: BuildTransformSummaryText(gameObject.Transform),
            HierarchySummaryText: BuildHierarchySummaryText(gameObject.Transform),
            SummaryCards: _summaryCardBuilder.Build(gameObject, details),
            DetailCards: BuildDetailCards(gameObject, details));
    }

    private IReadOnlyList<InspectorInfoCardViewModel> BuildDetailCards(
        AssetPreviewGameObjectDto gameObject,
        AssetDetailsDto details)
    {
        var cards = new List<InspectorInfoCardViewModel>
        {
            CreateCard(
                ObjectCardKey,
                ResourceKeys.InspectorGameObjectPreviewObjectCardTitle,
                CreateObjectRows(gameObject),
                isExpanded: false),
            CreateCard(
                TransformCardKey,
                ResourceKeys.InspectorGameObjectPreviewTransformCardTitle,
                CreateTransformRows(gameObject.Transform, details),
                isExpanded: true),
            CreateCard(
                ComponentsCardKey,
                ResourceKeys.InspectorGameObjectPreviewComponentsCardTitle,
                CreateComponentRows(gameObject, details),
                isExpanded: true)
        };

        var referenceRows = _referenceCardBuilder.CreateRows(gameObject, details);
        if (referenceRows.Count > 0)
        {
            cards.Add(CreateCard(
                ComponentReferencesCardKey,
                ResourceKeys.InspectorGameObjectPreviewComponentReferencesCardTitle,
                referenceRows,
                isExpanded: true));
        }

        return cards;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateObjectRows(AssetPreviewGameObjectDto gameObject)
    {
        var rows = new List<InspectorInfoRowViewModel>
        {
            new(GetString(ResourceKeys.InspectorGameObjectPreviewNameLabel), FormatText(gameObject.Name)),
            new(GetString(ResourceKeys.InspectorGameObjectPreviewActiveLabel), FormatBoolean(gameObject.IsActive)),
            new(GetString(ResourceKeys.InspectorGameObjectPreviewLayerLabel), FormatInteger(gameObject.Layer)),
            new(GetString(ResourceKeys.InspectorGameObjectPreviewTagLabel), FormatText(gameObject.Tag)),
            new(
                GetString(ResourceKeys.InspectorGameObjectPreviewComponentCountLabel),
                gameObject.ComponentCount.ToString(CultureInfo.CurrentCulture))
        };

        if (gameObject.StaticEditorFlags.HasValue)
        {
            rows.Add(new InspectorInfoRowViewModel(
                GetString(ResourceKeys.InspectorGameObjectPreviewStaticFlagsLabel),
                gameObject.StaticEditorFlags.Value.ToString(CultureInfo.CurrentCulture)));
        }

        return rows;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateTransformRows(
        AssetPreviewGameObjectTransformDto? transform,
        AssetDetailsDto details)
    {
        if (transform is null)
        {
            return
            [
                new(
                    string.Empty,
                    GetString(ResourceKeys.InspectorGameObjectPreviewNoTransformText))
            ];
        }

        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        return
        [
            new(GetString(ResourceKeys.InspectorGameObjectPreviewTransformTypeLabel), FormatText(transform.TypeName)),
            CreateLinkedRow(
                GetString(ResourceKeys.InspectorGameObjectPreviewTransformPathIdLabel),
                FormatInteger(transform.PathId),
                GameObjectPreviewDependencyResolver.ResolvePathId(transform.PathId, details),
                openButtonText,
                openButtonToolTipText),
            CreateLinkedRow(
                GetString(ResourceKeys.InspectorGameObjectPreviewParentPathIdLabel),
                FormatInteger(transform.ParentPathId),
                GameObjectPreviewDependencyResolver.ResolvePathId(transform.ParentPathId, details),
                openButtonText,
                openButtonToolTipText),
            new(
                GetString(ResourceKeys.InspectorGameObjectPreviewChildCountLabel),
                transform.ChildCount.ToString(CultureInfo.CurrentCulture)),
            new(
                GetString(ResourceKeys.InspectorGameObjectPreviewLocalPositionLabel),
                FormatVector3(transform.LocalPositionX, transform.LocalPositionY, transform.LocalPositionZ)),
            new(
                GetString(ResourceKeys.InspectorGameObjectPreviewLocalRotationLabel),
                FormatQuaternion(
                    transform.LocalRotationX,
                    transform.LocalRotationY,
                    transform.LocalRotationZ,
                    transform.LocalRotationW)),
            new(
                GetString(ResourceKeys.InspectorGameObjectPreviewLocalScaleLabel),
                FormatVector3(transform.LocalScaleX, transform.LocalScaleY, transform.LocalScaleZ))
        ];
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateComponentRows(
        AssetPreviewGameObjectDto gameObject,
        AssetDetailsDto details)
    {
        var rows = new List<InspectorInfoRowViewModel>();
        if (gameObject.Components.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorGameObjectPreviewEmptyComponentsText)));
            return rows;
        }

        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        foreach (var component in gameObject.Components)
        {
            rows.Add(CreateLinkedRow(
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorGameObjectPreviewComponentLabelFormat,
                    (component.Index + 1).ToString(CultureInfo.CurrentCulture)),
                FormatComponentValue(component),
                GameObjectPreviewDependencyResolver.ResolvePathId(component.PathId, details),
                openButtonText,
                openButtonToolTipText));
        }

        if (gameObject.IsComponentListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorGameObjectPreviewComponentsTruncatedText)));
        }

        return rows;
    }

    private string BuildTransformSummaryText(AssetPreviewGameObjectTransformDto? transform)
    {
        if (transform is null)
        {
            return GetString(ResourceKeys.InspectorGameObjectPreviewNoTransformText);
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorGameObjectPreviewTransformSummaryFormat,
            FormatVector3(transform.LocalPositionX, transform.LocalPositionY, transform.LocalPositionZ),
            FormatVector3(transform.LocalScaleX, transform.LocalScaleY, transform.LocalScaleZ));
    }

    private string BuildHierarchySummaryText(AssetPreviewGameObjectTransformDto? transform)
    {
        if (transform is null)
        {
            return string.Empty;
        }

        return transform.ParentPathId.HasValue
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewParentHierarchyFormat,
                transform.ParentPathId.Value.ToString(CultureInfo.CurrentCulture),
                transform.ChildCount.ToString(CultureInfo.CurrentCulture))
            : _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewRootHierarchyFormat,
                transform.ChildCount.ToString(CultureInfo.CurrentCulture));
    }

    private string BuildComponentTypeSummary(IReadOnlyList<AssetPreviewGameObjectComponentDto> components)
    {
        if (components.Count == 0)
        {
            return GetString(ResourceKeys.InspectorGameObjectPreviewEmptyComponentsText);
        }

        return string.Join(
            ", ",
            components
                .Take(4)
                .Select(static component => !string.IsNullOrWhiteSpace(component.ScriptName)
                    ? component.ScriptName!.Trim()
                    : !string.IsNullOrWhiteSpace(component.TypeName)
                        ? component.TypeName!.Trim()
                        : UnknownValue));
    }

    private string FormatComponentValue(AssetPreviewGameObjectComponentDto component)
    {
        var segments = new List<string>
        {
            _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentTypeFormat,
                FormatText(component.TypeName))
        };

        if (!string.IsNullOrWhiteSpace(component.Name))
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentNameFormat,
                component.Name.Trim()));
        }

        if (component.PathId.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentPathIdFormat,
                component.PathId.Value.ToString(CultureInfo.CurrentCulture)));
        }

        if (!string.IsNullOrWhiteSpace(component.ScriptName))
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentScriptFormat,
                component.ScriptName.Trim()));
        }

        if (component.IsEnabled.HasValue)
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentEnabledFormat,
                FormatBoolean(component.IsEnabled)));
        }

        if (component.IsExternal && !string.IsNullOrWhiteSpace(component.ExternalReferencePath))
        {
            segments.Add(_localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentExternalFormat,
                component.ExternalReferencePath.Trim()));
        }

        return string.Join(" · ", segments);
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
            dependency is null ? null : GameObjectPreviewDependencyResolver.CreateNavigationTarget(dependency),
            openButtonText,
            openButtonToolTipText);
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

    private static string ResolveAssetName(AssetPreviewGameObjectDto gameObject, string assetName)
    {
        if (!string.IsNullOrWhiteSpace(gameObject.Name))
        {
            return gameObject.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(assetName)
            ? string.Empty
            : assetName.Trim();
    }

    private string FormatVector3(double? x, double? y, double? z)
    {
        return x.HasValue && y.HasValue && z.HasValue
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewVector3Format,
                FormatNumber(x),
                FormatNumber(y),
                FormatNumber(z))
            : UnknownValue;
    }

    private string FormatQuaternion(double? x, double? y, double? z, double? w)
    {
        return x.HasValue && y.HasValue && z.HasValue && w.HasValue
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewQuaternionFormat,
                FormatNumber(x),
                FormatNumber(y),
                FormatNumber(z),
                FormatNumber(w))
            : UnknownValue;
    }

    private static string FormatInteger(long? value)
    {
        return value.HasValue
            ? value.Value.ToString(CultureInfo.CurrentCulture)
            : UnknownValue;
    }

    private static string FormatText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? UnknownValue
            : value.Trim();
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
            : UnknownValue;
    }

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);
}

public sealed record GameObjectPreviewDisplayState(
    string AssetNameText,
    string StateText,
    string ComponentSummaryText,
    string TransformSummaryText,
    string HierarchySummaryText,
    IReadOnlyList<GameObjectPreviewSummaryCardDisplayState> SummaryCards,
    IReadOnlyList<InspectorInfoCardViewModel>? DetailCards = null);
