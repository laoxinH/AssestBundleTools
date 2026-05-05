using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class GameObjectPreviewReferenceCardBuilder
{
    private const string UnknownValue = "-";

    private readonly ILocalizationService _localizationService;

    public GameObjectPreviewReferenceCardBuilder(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public IReadOnlyList<InspectorInfoRowViewModel> CreateRows(
        AssetPreviewGameObjectDto gameObject,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        ArgumentNullException.ThrowIfNull(details);

        var rows = new List<InspectorInfoRowViewModel>();
        var openButtonText = GetString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = GetString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        foreach (var component in gameObject.Components)
        {
            foreach (var reference in component.References ?? Array.Empty<AssetPreviewGameObjectReferenceDto>())
            {
                var resolvedDependency = GameObjectPreviewDependencyResolver.ResolveReference(reference, details);
                rows.Add(new InspectorInfoRowViewModel(
                    _localizationService.GetFormattedString(
                        ResourceKeys.InspectorGameObjectPreviewComponentReferenceLabelFormat,
                        (component.Index + 1).ToString(CultureInfo.CurrentCulture),
                        FormatReferenceRole(reference.Role)),
                    FormatComponentReferenceValue(reference, resolvedDependency),
                    resolvedDependency is null
                        ? null
                        : GameObjectPreviewDependencyResolver.CreateNavigationTarget(resolvedDependency),
                    openButtonText,
                    openButtonToolTipText));
            }
        }

        return rows;
    }

    private string FormatComponentReferenceValue(
        AssetPreviewGameObjectReferenceDto reference,
        AssetDependencyDto? resolvedDependency)
    {
        var targetText = _localizationService.GetFormattedString(
            ResourceKeys.InspectorGameObjectPreviewComponentReferenceTargetFormat,
            FormatReferenceTargetName(reference),
            FormatInteger(reference.PathId));
        if (resolvedDependency is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorGameObjectPreviewComponentReferenceResolvedFormat,
                targetText,
                resolvedDependency.Name,
                GameObjectPreviewDependencyResolver.ResolveAssetTypeDisplayName(resolvedDependency));
        }

        return _localizationService.GetFormattedString(
            reference.IsExternal
                ? ResourceKeys.InspectorGameObjectPreviewComponentReferenceExternalUnresolvedFormat
                : ResourceKeys.InspectorGameObjectPreviewComponentReferenceInternalUnresolvedFormat,
            targetText);
    }

    private string FormatReferenceRole(string role)
    {
        var resourceKey = role switch
        {
            "mesh" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleMesh,
            "material" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleMaterial,
            "sprite" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleSprite,
            "controller" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleController,
            "avatar" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleAvatar,
            "audio-clip" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleAudioClip,
            "script" => ResourceKeys.InspectorGameObjectPreviewReferenceRoleScript,
            _ => null
        };

        return resourceKey is null
            ? FormatText(role)
            : GetString(resourceKey);
    }

    private static string FormatReferenceTargetName(AssetPreviewGameObjectReferenceDto reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.Name))
        {
            return reference.Name.Trim();
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

    private string GetString(string resourceKey) =>
        _localizationService.GetRequiredString(resourceKey);
}
