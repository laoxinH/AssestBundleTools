using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal static class MaterialPreviewSemanticHintBuilder
{
    private const double Epsilon = 0.0001d;

    private static readonly string[] MainTextureProperties = ["_MainTex"];
    private static readonly string[] NormalTextureProperties = ["_BumpMap", "_NormalMap"];
    private static readonly string[] EmissionTextureProperties = ["_EmissionMap"];
    private static readonly string[] EmissionColorProperties = ["_EmissionColor"];
    private static readonly string[] MetallicTextureProperties = ["_MetallicGlossMap"];
    private static readonly string[] MetallicNumberProperties = ["_Metallic", "_Glossiness", "_Smoothness"];
    private static readonly string[] AlphaCutoffProperties = ["_Cutoff"];
    private static readonly string[] EffectTextureProperties = ["_MaskTex", "_DissolveTex", "_DisturbTex", "_DistortionTex"];

    public static IReadOnlyList<MaterialPreviewSemanticBadgeDisplayState> Build(
        AssetPreviewMaterialDto material,
        ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(localizationService);

        var badges = new List<MaterialPreviewSemanticBadgeDisplayState>(capacity: 7);

        AddIf(
            badges,
            localizationService,
            HasBoundTexture(material, MainTextureProperties),
            ResourceKeys.InspectorMaterialPreviewSemanticMainTextureDetectedText);
        AddIf(
            badges,
            localizationService,
            HasBoundTexture(material, NormalTextureProperties),
            ResourceKeys.InspectorMaterialPreviewSemanticNormalMapCandidateText);
        AddIf(
            badges,
            localizationService,
            HasBoundTexture(material, EmissionTextureProperties) || HasVisibleColor(material, EmissionColorProperties),
            ResourceKeys.InspectorMaterialPreviewSemanticEmissionCandidateText);
        AddIf(
            badges,
            localizationService,
            HasNumberGreaterThan(material, AlphaCutoffProperties, 0d),
            ResourceKeys.InspectorMaterialPreviewSemanticAlphaCutoffCandidateText);
        AddIf(
            badges,
            localizationService,
            HasBoundTexture(material, MetallicTextureProperties) || HasAnyNumber(material, MetallicNumberProperties),
            ResourceKeys.InspectorMaterialPreviewSemanticMetallicSmoothnessCandidateText);
        AddIf(
            badges,
            localizationService,
            HasBoundTexture(material, EffectTextureProperties),
            ResourceKeys.InspectorMaterialPreviewSemanticDissolveDistortionCandidateText);
        AddIf(
            badges,
            localizationService,
            HasTransparentBlendCandidate(material),
            ResourceKeys.InspectorMaterialPreviewSemanticTransparentBlendCandidateText);

        return badges;
    }

    private static void AddIf(
        ICollection<MaterialPreviewSemanticBadgeDisplayState> badges,
        ILocalizationService localizationService,
        bool condition,
        string resourceKey)
    {
        if (condition)
        {
            badges.Add(new MaterialPreviewSemanticBadgeDisplayState(
                localizationService.GetRequiredString(resourceKey)));
        }
    }

    private static bool HasBoundTexture(AssetPreviewMaterialDto material, IReadOnlyList<string> propertyNames)
    {
        return material.TextureProperties.Any(property =>
            HasName(property.Name, propertyNames) &&
            property.PathId is { } pathId &&
            pathId != 0);
    }

    private static bool HasVisibleColor(AssetPreviewMaterialDto material, IReadOnlyList<string> propertyNames)
    {
        return material.ColorProperties.Any(property =>
            HasName(property.Name, propertyNames) &&
            (Math.Abs(property.R) > Epsilon ||
             Math.Abs(property.G) > Epsilon ||
             Math.Abs(property.B) > Epsilon));
    }

    private static bool HasAnyNumber(AssetPreviewMaterialDto material, IReadOnlyList<string> propertyNames) =>
        material.FloatProperties.Any(property => HasName(property.Name, propertyNames)) ||
        material.IntegerProperties.Any(property => HasName(property.Name, propertyNames));

    private static bool HasNumberGreaterThan(
        AssetPreviewMaterialDto material,
        IReadOnlyList<string> propertyNames,
        double threshold)
    {
        return material.FloatProperties.Any(property =>
                   HasName(property.Name, propertyNames) &&
                   property.Value > threshold) ||
               material.IntegerProperties.Any(property =>
                   HasName(property.Name, propertyNames) &&
                   property.Value > threshold);
    }

    private static bool HasTransparentBlendCandidate(AssetPreviewMaterialDto material)
    {
        var mode = FindNumber(material, "_Mode");
        if (mode is >= 2d)
        {
            return true;
        }

        var sourceBlend = FindNumber(material, "_SrcBlend");
        if (sourceBlend.HasValue && Math.Abs(sourceBlend.Value - 1d) > Epsilon)
        {
            return true;
        }

        var destinationBlend = FindNumber(material, "_DstBlend");
        if (destinationBlend.HasValue && Math.Abs(destinationBlend.Value) > Epsilon)
        {
            return true;
        }

        var zWrite = FindNumber(material, "_ZWrite");
        return zWrite.HasValue && Math.Abs(zWrite.Value) <= Epsilon;
    }

    private static double? FindNumber(AssetPreviewMaterialDto material, string propertyName)
    {
        var floatProperty = material.FloatProperties.FirstOrDefault(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        if (floatProperty is not null)
        {
            return floatProperty.Value;
        }

        var integerProperty = material.IntegerProperties.FirstOrDefault(property =>
            string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase));
        return integerProperty?.Value;
    }

    private static bool HasName(string propertyName, IReadOnlyList<string> candidates) =>
        candidates.Any(candidate => string.Equals(propertyName, candidate, StringComparison.OrdinalIgnoreCase));
}
