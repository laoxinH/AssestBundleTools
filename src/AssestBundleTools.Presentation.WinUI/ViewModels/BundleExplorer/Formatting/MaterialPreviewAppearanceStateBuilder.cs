using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MaterialPreviewAppearanceStateBuilder
{
    private const string MainColorPropertyName = "_Color";
    private static readonly string[] BaseColorPropertyNames = ["_Color", "_BaseColor", "_TintColor"];
    private static readonly string[] MetallicPropertyNames = ["_Metallic", "_MetallicFactor"];
    private static readonly string[] SmoothnessPropertyNames = ["_Smoothness", "_Glossiness"];
    private static readonly string[] RoughnessPropertyNames = ["_Roughness"];
    private static readonly string[] EmissionColorPropertyNames = ["_EmissionColor"];

    public static MaterialPreviewAppearanceDisplayState Build(
        AssetPreviewMaterialDto material,
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(localizationService);

        var baseColor = FindColor(material, BaseColorPropertyNames) ??
                        new AssetPreviewMaterialColorPropertyDto(MainColorPropertyName, 0.74d, 0.74d, 0.74d, 1d);
        var metallic = Clamp01(FindNumber(material, MetallicPropertyNames) ?? 0d);
        var smoothness = Clamp01(
            FindNumber(material, SmoothnessPropertyNames) ??
            (FindNumber(material, RoughnessPropertyNames) is { } roughness ? 1d - roughness : 0.35d));
        var emission = FindColor(material, EmissionColorPropertyNames);
        var emissionStrength = emission is null
            ? 0d
            : Clamp01(Math.Max(Math.Max(emission.R, emission.G), emission.B));

        return new MaterialPreviewAppearanceDisplayState(
            TitleText: localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewAppearanceTitle),
            NoteText: localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewAppearanceNote),
            BaseColorText: localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewAppearanceBaseColorFormat,
                BuildColorValue(localizationService, baseColor)),
            AlphaText: localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewAppearanceAlphaFormat,
                FormatRawNumber(baseColor.A)),
            MetallicText: localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewAppearanceMetallicFormat,
                FormatNumber(metallic)),
            SmoothnessText: localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewAppearanceSmoothnessFormat,
                FormatNumber(smoothness)),
            R: Clamp01(baseColor.R),
            G: Clamp01(baseColor.G),
            B: Clamp01(baseColor.B),
            A: Clamp01(baseColor.A),
            Metallic: metallic,
            Smoothness: smoothness,
            EmissionStrength: emissionStrength);
    }

    private static AssetPreviewMaterialColorPropertyDto? FindColor(
        AssetPreviewMaterialDto material,
        IReadOnlyList<string> propertyNames)
    {
        return material.ColorProperties.FirstOrDefault(property =>
            propertyNames.Any(candidate => string.Equals(property.Name, candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private static double? FindNumber(AssetPreviewMaterialDto material, IReadOnlyList<string> propertyNames)
    {
        var floatProperty = material.FloatProperties.FirstOrDefault(property =>
            propertyNames.Any(candidate => string.Equals(property.Name, candidate, StringComparison.OrdinalIgnoreCase)));
        if (floatProperty is not null)
        {
            return floatProperty.Value;
        }

        var integerProperty = material.IntegerProperties.FirstOrDefault(property =>
            propertyNames.Any(candidate => string.Equals(property.Name, candidate, StringComparison.OrdinalIgnoreCase)));
        return integerProperty?.Value;
    }

    private static string BuildColorValue(
        ILocalizationService localizationService,
        AssetPreviewMaterialColorPropertyDto color)
    {
        return localizationService.GetFormattedString(
            ResourceKeys.InspectorMaterialPreviewColorValueFormat,
            FormatRawNumber(color.R),
            FormatRawNumber(color.G),
            FormatRawNumber(color.B),
            FormatRawNumber(color.A));
    }

    private static string FormatNumber(double value) => Clamp01(value).ToString("0.###", CultureInfo.CurrentCulture);

    private static string FormatRawNumber(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);

    private static double Clamp01(double value) => Math.Clamp(value, 0d, 1d);
}
