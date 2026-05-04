using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class MaterialPreviewDisplayFormatter
{
    private readonly ILocalizationService _localizationService;

    public MaterialPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public MaterialPreviewDisplayState BuildDisplayState(
        AssetPreviewMaterialDto material,
        AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(details);

        return new MaterialPreviewDisplayState(
            AssetNameText: string.IsNullOrWhiteSpace(details.Name) ? string.Empty : details.Name.Trim(),
            ShaderNameText: BuildShaderNameText(material.Shader),
            ShaderReferenceText: BuildShaderReferenceText(material.Shader),
            PropertyCountText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewPropertyCountFormat,
                material.TextureProperties.Count.ToString(CultureInfo.CurrentCulture),
                material.ColorProperties.Count.ToString(CultureInfo.CurrentCulture),
                (material.FloatProperties.Count + material.IntegerProperties.Count).ToString(CultureInfo.CurrentCulture)),
            TextureSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTexturesTitle),
            ColorSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewColorsTitle),
            NumberSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewNumbersTitle),
            EmptySectionText: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewEmptySectionText),
            TruncatedText: BuildTruncatedText(material),
            TextureRows: material.TextureProperties.Select(property => BuildTextureRow(property, details)).ToArray(),
            ColorRows: material.ColorProperties.Select(BuildColorRow).ToArray(),
            NumberRows: material.FloatProperties
                .Select(static property => new MaterialPreviewNumberRowDisplayState(
                    property.Name,
                    property.Value.ToString("0.###", CultureInfo.CurrentCulture)))
                .Concat(material.IntegerProperties.Select(static property => new MaterialPreviewNumberRowDisplayState(
                    property.Name,
                    property.Value.ToString(CultureInfo.CurrentCulture))))
                .ToArray());
    }

    private string BuildShaderNameText(AssetPreviewMaterialShaderDto? shader)
    {
        if (!string.IsNullOrWhiteSpace(shader?.Name))
        {
            return shader.Name.Trim();
        }

        return _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewShaderUnknownText);
    }

    private string BuildShaderReferenceText(AssetPreviewMaterialShaderDto? shader)
    {
        if (shader is null)
        {
            return string.Empty;
        }

        var source = !string.IsNullOrWhiteSpace(shader.ExternalReferencePath)
            ? shader.ExternalReferencePath!
            : shader.SerializedFileName ?? string.Empty;
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMaterialPreviewShaderReferenceFormat,
            string.IsNullOrWhiteSpace(source) ? "-" : source,
            shader.PathId.ToString(CultureInfo.CurrentCulture));
    }

    private MaterialPreviewTextureRowDisplayState BuildTextureRow(
        AssetPreviewMaterialTexturePropertyDto property,
        AssetDetailsDto details)
    {
        var targetText = BuildTextureTargetText(property);
        var resolvedTextureDependency = MaterialTextureDependencyResolver.ResolveTextureDependency(property, details);
        var resolutionText = BuildTextureResolutionText(property, resolvedTextureDependency);
        var transformText = property.ScaleX.HasValue ||
                            property.ScaleY.HasValue ||
                            property.OffsetX.HasValue ||
                            property.OffsetY.HasValue
            ? _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewTextureTransformFormat,
                FormatNumber(property.ScaleX),
                FormatNumber(property.ScaleY),
                FormatNumber(property.OffsetX),
                FormatNumber(property.OffsetY))
            : string.Empty;

        return new MaterialPreviewTextureRowDisplayState(
            PropertyName: property.Name,
            TargetText: targetText,
            ResolutionText: resolutionText,
            ResolvedTextureAssetId: resolvedTextureDependency?.AssetId.Value,
            TransformText: transformText);
    }

    private string BuildTextureTargetText(AssetPreviewMaterialTexturePropertyDto property)
    {
        if (!property.PathId.HasValue || property.PathId.Value == 0)
        {
            return _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTextureMissingText);
        }

        var source = !string.IsNullOrWhiteSpace(property.ExternalReferencePath)
            ? property.ExternalReferencePath!
            : property.SerializedFileName ?? string.Empty;
        var formatKey = property.IsExternal
            ? ResourceKeys.InspectorMaterialPreviewTextureExternalFormat
            : ResourceKeys.InspectorMaterialPreviewTextureInternalFormat;
        return _localizationService.GetFormattedString(
            formatKey,
            string.IsNullOrWhiteSpace(source) ? "-" : source,
            property.PathId.Value.ToString(CultureInfo.CurrentCulture));
    }

    private string BuildTextureResolutionText(
        AssetPreviewMaterialTexturePropertyDto property,
        AssetDependencyDto? resolvedTextureDependency)
    {
        if (!property.PathId.HasValue || property.PathId.Value == 0)
        {
            return string.Empty;
        }

        if (resolvedTextureDependency is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewTextureResolvedFormat,
                resolvedTextureDependency.Name,
                ResolveAssetTypeDisplayName(resolvedTextureDependency));
        }

        return property.IsExternal
            ? _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTextureExternalUnresolvedText)
            : _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTextureInternalUnresolvedText);
    }

    private MaterialPreviewColorRowDisplayState BuildColorRow(AssetPreviewMaterialColorPropertyDto property)
    {
        return new MaterialPreviewColorRowDisplayState(
            PropertyName: property.Name,
            R: property.R,
            G: property.G,
            B: property.B,
            A: property.A,
            ValueText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewColorValueFormat,
                FormatNumber(property.R),
                FormatNumber(property.G),
                FormatNumber(property.B),
                FormatNumber(property.A)));
    }

    private string BuildTruncatedText(AssetPreviewMaterialDto material)
    {
        return material.IsTexturePropertyListTruncated ||
               material.IsFloatPropertyListTruncated ||
               material.IsIntegerPropertyListTruncated ||
               material.IsColorPropertyListTruncated
            ? _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTruncatedText)
            : string.Empty;
    }

    private static string FormatNumber(double? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.CurrentCulture)
            : "-";
    }

    private static string ResolveAssetTypeDisplayName(AssetDependencyDto dependency)
    {
        if (!string.IsNullOrWhiteSpace(dependency.AssetTypeDisplayName))
        {
            return dependency.AssetTypeDisplayName.Trim();
        }

        return dependency.AssetType.ToString();
    }
}

public sealed record MaterialPreviewDisplayState(
    string AssetNameText,
    string ShaderNameText,
    string ShaderReferenceText,
    string PropertyCountText,
    string TextureSectionTitle,
    string ColorSectionTitle,
    string NumberSectionTitle,
    string EmptySectionText,
    string TruncatedText,
    IReadOnlyList<MaterialPreviewTextureRowDisplayState> TextureRows,
    IReadOnlyList<MaterialPreviewColorRowDisplayState> ColorRows,
    IReadOnlyList<MaterialPreviewNumberRowDisplayState> NumberRows);

public sealed record MaterialPreviewTextureRowDisplayState(
    string PropertyName,
    string TargetText,
    string ResolutionText,
    string? ResolvedTextureAssetId,
    string TransformText);

public sealed record MaterialPreviewColorRowDisplayState(
    string PropertyName,
    double R,
    double G,
    double B,
    double A,
    string ValueText);

public sealed record MaterialPreviewNumberRowDisplayState(
    string PropertyName,
    string ValueText);
