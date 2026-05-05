using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class AnimatorControllerPreviewDisplayFormatter
{
    private const string UnknownValue = "-";
    private const string ObjectCardKey = "animator-controller-object";
    private const string LayersCardKey = "animator-controller-layers";
    private const string ParametersCardKey = "animator-controller-parameters";
    private const string ContainersCardKey = "animator-controller-containers";
    private const string ReferencesCardKey = "animator-controller-references";

    private readonly ILocalizationService _localizationService;

    public AnimatorControllerPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public AnimatorControllerPreviewDisplayState BuildDisplayState(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(animatorController);
        ArgumentNullException.ThrowIfNull(details);

        return new AnimatorControllerPreviewDisplayState(
            AssetNameText: ResolveAssetName(animatorController, details.Name),
            StructureText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimatorControllerPreviewStructureFormat,
                animatorController.LayerCount.ToString(CultureInfo.CurrentCulture),
                animatorController.ParameterCount.ToString(CultureInfo.CurrentCulture)),
            StateSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimatorControllerPreviewStateSummaryFormat,
                animatorController.StateMachineCount.ToString(CultureInfo.CurrentCulture),
                animatorController.StateCount.ToString(CultureInfo.CurrentCulture),
                animatorController.TransitionCount.ToString(CultureInfo.CurrentCulture)),
            ReferenceSummaryText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimatorControllerPreviewReferenceSummaryFormat,
                CountResolvedReferences(animatorController, details).ToString(CultureInfo.CurrentCulture),
                animatorController.ReferenceCount.ToString(CultureInfo.CurrentCulture),
                BuildReferenceTypeSummary(animatorController.References)),
            SummaryCards: BuildSummaryCards(animatorController, details),
            DetailCards: BuildDetailCards(animatorController, details));
    }

    private IReadOnlyList<AnimatorControllerPreviewSummaryCardDisplayState> BuildSummaryCards(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetDetailsDto details)
    {
        return
        [
            new(
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewSummaryCardStructureTitle),
                animatorController.StateMachineCount.ToString(CultureInfo.CurrentCulture),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewSummaryStructureDetailFormat,
                    animatorController.LayerCount.ToString(CultureInfo.CurrentCulture),
                    CountContainers(animatorController, AssetPreviewAnimatorControllerContainerKind.Value)
                        .ToString(CultureInfo.CurrentCulture))),
            new(
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewSummaryCardParametersTitle),
                animatorController.ParameterCount.ToString(CultureInfo.CurrentCulture),
                BuildParameterTypeSummary(animatorController.Parameters)),
            new(
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewSummaryCardStatesTitle),
                animatorController.StateCount.ToString(CultureInfo.CurrentCulture),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewSummaryStateDetailFormat,
                    animatorController.TransitionCount.ToString(CultureInfo.CurrentCulture),
                    animatorController.AnyStateTransitionCount.ToString(CultureInfo.CurrentCulture))),
            new(
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewSummaryCardReferencesTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewResolvedReferenceCountFormat,
                    CountResolvedReferences(animatorController, details).ToString(CultureInfo.CurrentCulture),
                    animatorController.ReferenceCount.ToString(CultureInfo.CurrentCulture)),
                BuildReferenceTypeSummary(animatorController.References))
        ];
    }

    private IReadOnlyList<InspectorInfoCardViewModel> BuildDetailCards(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetDetailsDto details)
    {
        var cards = new List<InspectorInfoCardViewModel>
        {
            CreateCard(
                ObjectCardKey,
                ResourceKeys.InspectorAnimatorControllerPreviewObjectCardTitle,
                CreateObjectRows(animatorController),
                isExpanded: false),
            CreateCard(
                LayersCardKey,
                ResourceKeys.InspectorAnimatorControllerPreviewLayersCardTitle,
                CreateLayerRows(animatorController, details),
                isExpanded: true),
            CreateCard(
                ParametersCardKey,
                ResourceKeys.InspectorAnimatorControllerPreviewParametersCardTitle,
                CreateParameterRows(animatorController),
                isExpanded: true),
            CreateCard(
                ContainersCardKey,
                ResourceKeys.InspectorAnimatorControllerPreviewContainersCardTitle,
                CreateContainerRows(animatorController),
                isExpanded: true)
        };

        var referenceRows = CreateReferenceRows(animatorController, details);
        if (referenceRows.Count > 0)
        {
            cards.Add(CreateCard(
                ReferencesCardKey,
                ResourceKeys.InspectorAnimatorControllerPreviewReferencesCardTitle,
                referenceRows,
                isExpanded: true));
        }

        return cards;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateObjectRows(
        AssetPreviewAnimatorControllerDto animatorController)
    {
        return
        [
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewNameLabel), FormatText(animatorController.Name)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewLayerCountLabel), animatorController.LayerCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewParameterCountLabel), animatorController.ParameterCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewStateMachineCountLabel), animatorController.StateMachineCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewStateCountLabel), animatorController.StateCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewTransitionCountLabel), animatorController.TransitionCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewAnyStateTransitionCountLabel), animatorController.AnyStateTransitionCount.ToString(CultureInfo.CurrentCulture)),
            new(GetString(ResourceKeys.InspectorAnimatorControllerPreviewReferenceCountLabel), animatorController.ReferenceCount.ToString(CultureInfo.CurrentCulture))
        ];
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateLayerRows(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetDetailsDto details)
    {
        var rows = new List<InspectorInfoRowViewModel>();
        if (animatorController.Layers.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewEmptyLayersText)));
            return rows;
        }

        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        foreach (var layer in animatorController.Layers)
        {
            var stateMachineDependency = AnimatorControllerPreviewDependencyResolver.ResolvePathId(
                layer.StateMachinePathId,
                details);
            rows.Add(new InspectorInfoRowViewModel(
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewLayerLabelFormat,
                    (layer.Index + 1).ToString(CultureInfo.CurrentCulture)),
                FormatLayerValue(layer),
                stateMachineDependency is null
                    ? null
                    : AnimatorControllerPreviewDependencyResolver.CreateNavigationTarget(stateMachineDependency),
                openButtonText,
                openButtonToolTipText));
        }

        if (animatorController.IsLayerListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewLayersTruncatedText)));
        }

        return rows;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateParameterRows(
        AssetPreviewAnimatorControllerDto animatorController)
    {
        var rows = new List<InspectorInfoRowViewModel>();
        if (animatorController.Parameters.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewEmptyParametersText)));
            return rows;
        }

        foreach (var parameter in animatorController.Parameters)
        {
            rows.Add(new InspectorInfoRowViewModel(
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewParameterLabelFormat,
                    (parameter.Index + 1).ToString(CultureInfo.CurrentCulture)),
                FormatParameterValue(parameter)));
        }

        if (animatorController.IsParameterListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewParametersTruncatedText)));
        }

        return rows;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateContainerRows(
        AssetPreviewAnimatorControllerDto animatorController)
    {
        var containers = animatorController.Containers ?? Array.Empty<AssetPreviewAnimatorControllerContainerDto>();
        var rows = new List<InspectorInfoRowViewModel>();
        if (containers.Count == 0)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewEmptyContainersText)));
            return rows;
        }

        foreach (var container in containers)
        {
            rows.Add(new InspectorInfoRowViewModel(
                FormatText(container.Path),
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewContainerSummaryFormat,
                    GetContainerKindText(container.Kind),
                    container.ChildCount.ToString(CultureInfo.CurrentCulture),
                    container.Depth.ToString(CultureInfo.CurrentCulture))));
        }

        if (animatorController.IsContainerListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainersTruncatedText)));
        }

        return rows;
    }

    private IReadOnlyList<InspectorInfoRowViewModel> CreateReferenceRows(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetDetailsDto details)
    {
        var rows = new List<InspectorInfoRowViewModel>();
        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        foreach (var reference in animatorController.References)
        {
            var resolvedDependency = AnimatorControllerPreviewDependencyResolver.ResolveReference(reference, details);
            rows.Add(new InspectorInfoRowViewModel(
                FormatReferenceLabel(reference),
                FormatReferenceValue(reference, resolvedDependency),
                resolvedDependency is null
                    ? null
                    : AnimatorControllerPreviewDependencyResolver.CreateNavigationTarget(resolvedDependency),
                openButtonText,
                openButtonToolTipText));
        }

        if (animatorController.IsReferenceListTruncated)
        {
            rows.Add(new InspectorInfoRowViewModel(
                string.Empty,
                GetString(ResourceKeys.InspectorAnimatorControllerPreviewReferencesTruncatedText)));
        }

        return rows;
    }

    private string FormatLayerValue(AssetPreviewAnimatorControllerLayerDto layer)
    {
        var layerName = FormatText(layer.Name);
        var summary = _localizationService.GetFormattedString(
            ResourceKeys.InspectorAnimatorControllerPreviewLayerValueFormat,
            FormatNumber(layer.DefaultWeight),
            FormatInteger(layer.StateCount),
            FormatInteger(layer.TransitionCount));
        if (layer.StateMachinePathId.HasValue)
        {
            summary = string.Concat(
                summary,
                " · ",
                _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewLayerStateMachinePathIdFormat,
                    FormatInteger(layer.StateMachinePathId)));
        }

        return string.Equals(layerName, UnknownValue, StringComparison.Ordinal)
            ? summary
            : string.Concat(layerName, " · ", summary);
    }

    private string FormatParameterValue(AssetPreviewAnimatorControllerParameterDto parameter)
    {
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorAnimatorControllerPreviewParameterValueFormat,
            FormatText(parameter.Name),
            FormatParameterType(parameter),
            FormatParameterDefaultValue(parameter));
    }

    private string FormatReferenceValue(
        AssetPreviewAnimatorControllerReferenceDto reference,
        AssetDependencyDto? resolvedDependency)
    {
        var targetText = _localizationService.GetFormattedString(
            ResourceKeys.InspectorAnimatorControllerPreviewReferenceTargetFormat,
            FormatReferenceTargetName(reference),
            FormatInteger(reference.PathId));
        if (resolvedDependency is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimatorControllerPreviewReferenceResolvedFormat,
                targetText,
                resolvedDependency.Name,
                AnimatorControllerPreviewDependencyResolver.ResolveAssetTypeDisplayName(resolvedDependency));
        }

        return _localizationService.GetFormattedString(
            reference.IsExternal
                ? ResourceKeys.InspectorAnimatorControllerPreviewReferenceExternalUnresolvedFormat
                : ResourceKeys.InspectorAnimatorControllerPreviewReferenceInternalUnresolvedFormat,
            targetText);
    }

    private string BuildParameterTypeSummary(
        IReadOnlyList<AssetPreviewAnimatorControllerParameterDto> parameters)
    {
        if (parameters.Count == 0)
        {
            return GetString(ResourceKeys.InspectorAnimatorControllerPreviewEmptyParametersText);
        }

        return string.Join(
            ", ",
            parameters
                .GroupBy(FormatParameterType)
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(group => _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeCountFormat,
                    group.Key,
                    group.Count().ToString(CultureInfo.CurrentCulture))));
    }

    private string BuildReferenceTypeSummary(
        IReadOnlyList<AssetPreviewAnimatorControllerReferenceDto> references)
    {
        if (references.Count == 0)
        {
            return GetString(ResourceKeys.InspectorAnimatorControllerPreviewEmptyReferencesText);
        }

        return string.Join(
            ", ",
            references
                .GroupBy(static reference => string.IsNullOrWhiteSpace(reference.TypeName) ? UnknownValue : reference.TypeName.Trim())
                .OrderByDescending(static group => group.Count())
                .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(group => _localizationService.GetFormattedString(
                    ResourceKeys.InspectorAnimatorControllerPreviewReferenceTypeCountFormat,
                    group.Key,
                    group.Count().ToString(CultureInfo.CurrentCulture))));
    }

    private int CountResolvedReferences(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetDetailsDto details)
    {
        return animatorController.References.Count(reference =>
            AnimatorControllerPreviewDependencyResolver.ResolveReference(reference, details) is not null);
    }

    private static int CountContainers(
        AssetPreviewAnimatorControllerDto animatorController,
        AssetPreviewAnimatorControllerContainerKind kind)
    {
        return animatorController.Containers?.Count(container => container.Kind == kind) ?? 0;
    }

    private string FormatParameterType(AssetPreviewAnimatorControllerParameterDto parameter)
    {
        if (string.Equals(parameter.TypeName, "Float", StringComparison.OrdinalIgnoreCase))
        {
            return GetString(ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeFloat);
        }

        if (string.Equals(parameter.TypeName, "Int", StringComparison.OrdinalIgnoreCase))
        {
            return GetString(ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeInt);
        }

        if (string.Equals(parameter.TypeName, "Bool", StringComparison.OrdinalIgnoreCase))
        {
            return GetString(ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeBool);
        }

        if (string.Equals(parameter.TypeName, "Trigger", StringComparison.OrdinalIgnoreCase))
        {
            return GetString(ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeTrigger);
        }

        return parameter.Type.HasValue
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorAnimatorControllerPreviewParameterTypeUnknownFormat,
                parameter.Type.Value.ToString(CultureInfo.CurrentCulture))
            : UnknownValue;
    }

    private string FormatParameterDefaultValue(AssetPreviewAnimatorControllerParameterDto parameter)
    {
        if (string.Equals(parameter.TypeName, "Bool", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameter.TypeName, "Trigger", StringComparison.OrdinalIgnoreCase))
        {
            return FormatBoolean(parameter.DefaultBool);
        }

        if (string.Equals(parameter.TypeName, "Int", StringComparison.OrdinalIgnoreCase))
        {
            return FormatInteger(parameter.DefaultInt);
        }

        return FormatNumber(parameter.DefaultFloat);
    }

    private string GetContainerKindText(AssetPreviewAnimatorControllerContainerKind kind)
    {
        return kind switch
        {
            AssetPreviewAnimatorControllerContainerKind.Layer => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindLayer),
            AssetPreviewAnimatorControllerContainerKind.Parameter => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindParameter),
            AssetPreviewAnimatorControllerContainerKind.StateMachine => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindStateMachine),
            AssetPreviewAnimatorControllerContainerKind.State => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindState),
            AssetPreviewAnimatorControllerContainerKind.Transition => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindTransition),
            AssetPreviewAnimatorControllerContainerKind.AnyStateTransition => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindAnyStateTransition),
            AssetPreviewAnimatorControllerContainerKind.BlendTree => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindBlendTree),
            AssetPreviewAnimatorControllerContainerKind.Value => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindValue),
            _ => GetString(ResourceKeys.InspectorAnimatorControllerPreviewContainerKindUnknown)
        };
    }

    private string FormatReferenceLabel(AssetPreviewAnimatorControllerReferenceDto reference) =>
        string.IsNullOrWhiteSpace(reference.Path) ? FormatText(reference.Name) : reference.Path.Trim();

    private static string ResolveAssetName(AssetPreviewAnimatorControllerDto animatorController, string assetName)
    {
        if (!string.IsNullOrWhiteSpace(animatorController.Name))
        {
            return animatorController.Name.Trim();
        }

        return string.IsNullOrWhiteSpace(assetName)
            ? string.Empty
            : assetName.Trim();
    }

    private static string FormatReferenceTargetName(AssetPreviewAnimatorControllerReferenceDto reference)
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

public sealed record AnimatorControllerPreviewDisplayState(
    string AssetNameText,
    string StructureText,
    string StateSummaryText,
    string ReferenceSummaryText,
    IReadOnlyList<AnimatorControllerPreviewSummaryCardDisplayState> SummaryCards,
    IReadOnlyList<InspectorInfoCardViewModel>? DetailCards = null);

public sealed record AnimatorControllerPreviewSummaryCardDisplayState(
    string TitleText,
    string ValueText,
    string DetailText);
