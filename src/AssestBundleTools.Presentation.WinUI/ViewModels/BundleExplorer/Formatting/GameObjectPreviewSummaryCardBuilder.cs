using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class GameObjectPreviewSummaryCardBuilder
{
    private const int MaxCardCount = 5;

    private readonly ILocalizationService _localizationService;

    public GameObjectPreviewSummaryCardBuilder(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public IReadOnlyList<GameObjectPreviewSummaryCardDisplayState> Build(
        AssetPreviewGameObjectDto gameObject,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        ArgumentNullException.ThrowIfNull(details);

        var cards = new List<GameObjectPreviewSummaryCardDisplayState>(MaxCardCount)
        {
            CreateComponentOverviewCard(gameObject)
        };

        AddReferenceCard(cards, gameObject, details);
        AddComponentHighlightCards(cards, gameObject.Components);

        return cards;
    }

    private GameObjectPreviewSummaryCardDisplayState CreateComponentOverviewCard(
        AssetPreviewGameObjectDto gameObject)
    {
        return new GameObjectPreviewSummaryCardDisplayState(
            GetString(ResourceKeys.InspectorGameObjectPreviewSummaryCardComponentsTitle),
            gameObject.ComponentCount.ToString(CultureInfo.CurrentCulture),
            BuildComponentTypeSummary(gameObject.Components));
    }

    private void AddReferenceCard(
        ICollection<GameObjectPreviewSummaryCardDisplayState> cards,
        AssetPreviewGameObjectDto gameObject,
        AssetDetailsDto details)
    {
        var references = gameObject.Components
            .SelectMany(static component => component.References ?? Array.Empty<AssetPreviewGameObjectReferenceDto>())
            .ToArray();
        if (references.Length == 0)
        {
            return;
        }

        var resolvedReferenceCount = references
            .Count(reference => GameObjectPreviewDependencyResolver.ResolveReference(reference, details) is not null);
        var roleSummary = BuildReferenceRoleSummary(references);
        AddCard(
            cards,
            GetString(ResourceKeys.InspectorGameObjectPreviewSummaryCardReferencesTitle),
            FormatField(
                ResourceKeys.InspectorGameObjectPreviewSummaryResolvedReferenceLabel,
                resolvedReferenceCount.ToString(CultureInfo.CurrentCulture)),
            roleSummary);
    }

    private void AddComponentHighlightCards(
        ICollection<GameObjectPreviewSummaryCardDisplayState> cards,
        IReadOnlyList<AssetPreviewGameObjectComponentDto> components)
    {
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => typeName.EndsWith("Renderer", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardRendererTitle,
            [AssetPreviewGameObjectComponentSummaryKey.MaterialCount, AssetPreviewGameObjectComponentSummaryKey.SpriteReference],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled, AssetPreviewGameObjectComponentSummaryKey.CastShadows, AssetPreviewGameObjectComponentSummaryKey.ReceiveShadows]);
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => string.Equals(typeName, "MeshFilter", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardMeshTitle,
            [AssetPreviewGameObjectComponentSummaryKey.MeshReference],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled]);
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => string.Equals(typeName, "MonoBehaviour", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardScriptTitle,
            [AssetPreviewGameObjectComponentSummaryKey.Script],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled]);
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => typeName.Contains("Animator", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardAnimatorTitle,
            [AssetPreviewGameObjectComponentSummaryKey.Controller, AssetPreviewGameObjectComponentSummaryKey.Avatar],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled, AssetPreviewGameObjectComponentSummaryKey.RootMotion]);
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => typeName.Contains("AudioSource", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardAudioTitle,
            [AssetPreviewGameObjectComponentSummaryKey.AudioClip],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled, AssetPreviewGameObjectComponentSummaryKey.Loop, AssetPreviewGameObjectComponentSummaryKey.PlayOnAwake, AssetPreviewGameObjectComponentSummaryKey.Volume]);
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => typeName.Contains("ParticleSystem", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardParticleTitle,
            [AssetPreviewGameObjectComponentSummaryKey.ParticleDuration],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled, AssetPreviewGameObjectComponentSummaryKey.ParticleLooping, AssetPreviewGameObjectComponentSummaryKey.ParticlePrewarm]);
        AddFirstMatchingComponentCard(
            cards,
            components,
            static component => HasTypeName(component, static typeName => typeName.Contains("Collider", StringComparison.OrdinalIgnoreCase)),
            ResourceKeys.InspectorGameObjectPreviewSummaryCardColliderTitle,
            [AssetPreviewGameObjectComponentSummaryKey.IsTrigger],
            [AssetPreviewGameObjectComponentSummaryKey.Enabled]);
    }

    private void AddFirstMatchingComponentCard(
        ICollection<GameObjectPreviewSummaryCardDisplayState> cards,
        IReadOnlyList<AssetPreviewGameObjectComponentDto> components,
        Func<AssetPreviewGameObjectComponentDto, bool> predicate,
        string titleResourceKey,
        IReadOnlyList<AssetPreviewGameObjectComponentSummaryKey> primaryKeys,
        IReadOnlyList<AssetPreviewGameObjectComponentSummaryKey> detailKeys)
    {
        if (cards.Count >= MaxCardCount)
        {
            return;
        }

        var component = components.FirstOrDefault(predicate);
        if (component is null)
        {
            return;
        }

        var primaryText = CreateSummaryText(component, primaryKeys);
        var detailText = CreateSummaryText(component, detailKeys);
        if (string.IsNullOrWhiteSpace(primaryText))
        {
            primaryText = FormatComponentName(component);
        }

        AddCard(cards, GetString(titleResourceKey), primaryText, detailText);
    }

    private string CreateSummaryText(
        AssetPreviewGameObjectComponentDto component,
        IReadOnlyList<AssetPreviewGameObjectComponentSummaryKey> keys)
    {
        var summaries = component.Summaries ?? Array.Empty<AssetPreviewGameObjectComponentSummaryDto>();
        var parts = keys
            .Select(key => summaries.FirstOrDefault(summary => summary.Key == key))
            .Where(static summary => summary is not null)
            .Select(summary => FormatSummary(summary!))
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        return string.Join(" · ", parts);
    }

    private string BuildReferenceRoleSummary(IReadOnlyList<AssetPreviewGameObjectReferenceDto> references)
    {
        var parts = references
            .Where(static reference => !string.IsNullOrWhiteSpace(reference.Role))
            .GroupBy(static reference => reference.Role.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => FormatField(
                ResolveReferenceRoleLabel(group.Key),
                group.Count().ToString(CultureInfo.CurrentCulture)))
            .ToArray();

        return string.Join(" · ", parts);
    }

    private string FormatSummary(AssetPreviewGameObjectComponentSummaryDto summary)
    {
        var value = FormatSummaryValue(summary);
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : FormatField(ResolveSummaryLabel(summary.Key), value);
    }

    private string FormatSummaryValue(AssetPreviewGameObjectComponentSummaryDto summary)
    {
        return summary.ValueKind switch
        {
            AssetPreviewInfoValueKind.Integer => FormatInteger(summary.Int64Value),
            AssetPreviewInfoValueKind.Decimal => FormatNumber(summary.DoubleValue),
            AssetPreviewInfoValueKind.Boolean => FormatBoolean(summary.BooleanValue),
            _ => FormatText(summary.StringValue)
        };
    }

    private void AddCard(
        ICollection<GameObjectPreviewSummaryCardDisplayState> cards,
        string title,
        string value,
        string detail)
    {
        if (cards.Count >= MaxCardCount)
        {
            return;
        }

        cards.Add(new GameObjectPreviewSummaryCardDisplayState(
            title,
            string.IsNullOrWhiteSpace(value) ? UnknownValue : value.Trim(),
            string.IsNullOrWhiteSpace(detail) ? string.Empty : detail.Trim()));
    }

    private string FormatField(string labelResourceKey, string value) =>
        _localizationService.GetFormattedString(
            ResourceKeys.InspectorGameObjectPreviewComponentSummaryFieldFormat,
            GetString(labelResourceKey),
            value);

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

    private string FormatComponentName(AssetPreviewGameObjectComponentDto component)
    {
        if (!string.IsNullOrWhiteSpace(component.ScriptName))
        {
            return component.ScriptName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(component.Name))
        {
            return component.Name.Trim();
        }

        return FormatText(component.TypeName);
    }

    private string ResolveSummaryLabel(AssetPreviewGameObjectComponentSummaryKey key) =>
        key switch
        {
            AssetPreviewGameObjectComponentSummaryKey.MaterialCount => ResourceKeys.InspectorGameObjectPreviewReferenceRoleMaterial,
            AssetPreviewGameObjectComponentSummaryKey.MeshReference => ResourceKeys.InspectorGameObjectPreviewReferenceRoleMesh,
            AssetPreviewGameObjectComponentSummaryKey.Script => ResourceKeys.InspectorGameObjectPreviewReferenceRoleScript,
            AssetPreviewGameObjectComponentSummaryKey.Enabled => ResourceKeys.InspectorGameObjectPreviewComponentSummaryEnabledLabel,
            AssetPreviewGameObjectComponentSummaryKey.CastShadows => ResourceKeys.InspectorGameObjectPreviewComponentSummaryCastShadowsLabel,
            AssetPreviewGameObjectComponentSummaryKey.ReceiveShadows => ResourceKeys.InspectorGameObjectPreviewComponentSummaryReceiveShadowsLabel,
            AssetPreviewGameObjectComponentSummaryKey.Controller => ResourceKeys.InspectorGameObjectPreviewReferenceRoleController,
            AssetPreviewGameObjectComponentSummaryKey.Avatar => ResourceKeys.InspectorGameObjectPreviewReferenceRoleAvatar,
            AssetPreviewGameObjectComponentSummaryKey.AudioClip => ResourceKeys.InspectorGameObjectPreviewReferenceRoleAudioClip,
            AssetPreviewGameObjectComponentSummaryKey.Loop => ResourceKeys.InspectorGameObjectPreviewComponentSummaryLoopLabel,
            AssetPreviewGameObjectComponentSummaryKey.PlayOnAwake => ResourceKeys.InspectorGameObjectPreviewComponentSummaryPlayOnAwakeLabel,
            AssetPreviewGameObjectComponentSummaryKey.Volume => ResourceKeys.InspectorGameObjectPreviewComponentSummaryVolumeLabel,
            AssetPreviewGameObjectComponentSummaryKey.IsTrigger => ResourceKeys.InspectorGameObjectPreviewComponentSummaryTriggerLabel,
            AssetPreviewGameObjectComponentSummaryKey.RootMotion => ResourceKeys.InspectorGameObjectPreviewComponentSummaryRootMotionLabel,
            AssetPreviewGameObjectComponentSummaryKey.ParticleLooping => ResourceKeys.InspectorGameObjectPreviewComponentSummaryLoopLabel,
            AssetPreviewGameObjectComponentSummaryKey.ParticlePrewarm => ResourceKeys.InspectorGameObjectPreviewComponentSummaryPrewarmLabel,
            AssetPreviewGameObjectComponentSummaryKey.ParticleDuration => ResourceKeys.InspectorGameObjectPreviewComponentSummaryDurationLabel,
            AssetPreviewGameObjectComponentSummaryKey.SpriteReference => ResourceKeys.InspectorGameObjectPreviewReferenceRoleSprite,
            _ => ResourceKeys.InspectorGameObjectPreviewComponentSummaryUnknownLabel
        };

    private string ResolveReferenceRoleLabel(string role) =>
        role switch
        {
            "mesh" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleMesh,
            "material" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleMaterial,
            "sprite" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleSprite,
            "controller" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleController,
            "avatar" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleAvatar,
            "audio-clip" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleAudioClip,
            "script" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleScript,
            _ => ResourceKeys.InspectorGameObjectPreviewComponentSummaryUnknownLabel
        };

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
        value.HasValue ? value.Value.ToString(CultureInfo.CurrentCulture) : UnknownValue;

    private static string FormatNumber(double? value) =>
        value.HasValue ? value.Value.ToString("0.###", CultureInfo.CurrentCulture) : UnknownValue;

    private static string FormatText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? UnknownValue : value.Trim();

    private static bool HasTypeName(
        AssetPreviewGameObjectComponentDto component,
        Func<string, bool> predicate) =>
        !string.IsNullOrWhiteSpace(component.TypeName) && predicate(component.TypeName.Trim());

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);

    private const string UnknownValue = "-";
}

public sealed record GameObjectPreviewSummaryCardDisplayState(
    string TitleText,
    string ValueText,
    string DetailText);
