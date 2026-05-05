using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class MaterialPreviewDisplayFormatter
{
    private const string MainColorPropertyName = "_Color";

    private readonly ILocalizationService _localizationService;
    private readonly MaterialPreviewInfoCardBuilder _infoCardBuilder;

    public MaterialPreviewDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _infoCardBuilder = new MaterialPreviewInfoCardBuilder(_localizationService);
    }

    public MaterialPreviewDisplayState BuildDisplayState(
        AssetPreviewMaterialDto material,
        AssetDetailsDto details,
        IReadOnlyDictionary<string, ImageSource>? textureThumbnailSources = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(details);

        var resolvedShaderDependency = MaterialShaderDependencyResolver.ResolveShaderDependency(material.Shader, details);
        var openButtonText = _localizationService.GetRequiredString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = _localizationService.GetRequiredString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        var shaderNameText = BuildShaderNameText(material.Shader);
        var shaderReferenceText = BuildShaderReferenceText(material.Shader);
        var shaderResolutionText = BuildShaderResolutionText(material.Shader, resolvedShaderDependency);
        var textureReferenceSummaryText = BuildTextureReferenceSummaryText(material, details);
        var truncatedText = BuildTruncatedText(material);
        var mainTextureTint = BuildMainTextureTint(material);
        var appearance = MaterialPreviewAppearanceStateBuilder.Build(material, _localizationService);
        var semanticBadges = MaterialPreviewSemanticHintBuilder.Build(material, _localizationService);
        var textureRows = BuildTextureRows(material, details, openButtonText, openButtonToolTipText);
        var colorRows = material.ColorProperties.Select(BuildColorRow).ToArray();
        var numberRows = material.FloatProperties
            .Select(static property => new MaterialPreviewNumberRowDisplayState(
                property.Name,
                property.Value.ToString("0.###", CultureInfo.CurrentCulture)))
            .Concat(material.IntegerProperties.Select(static property => new MaterialPreviewNumberRowDisplayState(
                property.Name,
                property.Value.ToString(CultureInfo.CurrentCulture))))
            .ToArray();

        return new MaterialPreviewDisplayState(
            AssetNameText: string.IsNullOrWhiteSpace(details.Name) ? string.Empty : details.Name.Trim(),
            ShaderNameText: shaderNameText,
            ShaderReferenceText: shaderReferenceText,
            ShaderResolutionText: shaderResolutionText,
            ResolvedShaderAssetId: resolvedShaderDependency?.AssetId.Value,
            PropertyCountText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewPropertyCountFormat,
                material.TextureProperties.Count.ToString(CultureInfo.CurrentCulture),
                material.ColorProperties.Count.ToString(CultureInfo.CurrentCulture),
                (material.FloatProperties.Count + material.IntegerProperties.Count).ToString(CultureInfo.CurrentCulture)),
            MainTexturePreviewTitleText: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewMainTextureTitleText),
            MainTexturePreviewLimitText: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewMainTextureLimitText),
            MainTextureTint: mainTextureTint,
            Appearance: appearance,
            SemanticSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewSemanticHintsTitle),
            SemanticBadges: semanticBadges,
            TextureSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTexturesTitle),
            TextureReferenceSummaryText: textureReferenceSummaryText,
            ColorSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewColorsTitle),
            NumberSectionTitle: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewNumbersTitle),
            EmptySectionText: _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewEmptySectionText),
            TruncatedText: truncatedText,
            TextureRows: textureRows,
            ColorRows: colorRows,
            NumberRows: numberRows,
            ShaderTarget: resolvedShaderDependency is null ? null : CreateNavigationTarget(resolvedShaderDependency),
            OpenResolvedAssetButtonText: openButtonText,
            OpenResolvedAssetButtonToolTipText: openButtonToolTipText,
            SummaryCards: BuildSummaryCards(material, textureReferenceSummaryText, semanticBadges),
            DetailCards: _infoCardBuilder.Build(
                shaderNameText,
                shaderReferenceText,
                shaderResolutionText,
                resolvedShaderDependency,
                appearance,
                semanticBadges,
                textureRows,
                colorRows,
                numberRows,
                truncatedText,
                openButtonText,
                openButtonToolTipText,
                mainTextureTint,
                textureThumbnailSources));
    }

    private IReadOnlyList<MaterialPreviewSummaryCardDisplayState> BuildSummaryCards(
        AssetPreviewMaterialDto material,
        string textureReferenceSummaryText,
        IReadOnlyList<MaterialPreviewSemanticBadgeDisplayState> semanticBadges)
    {
        return
        [
            new(
                _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewTexturesTitle),
                material.TextureProperties.Count.ToString(CultureInfo.CurrentCulture),
                textureReferenceSummaryText),
            new(
                _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewColorsTitle),
                material.ColorProperties.Count.ToString(CultureInfo.CurrentCulture),
                BuildPropertyNameSummary(material.ColorProperties.Select(static property => property.Name))),
            new(
                _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewNumbersTitle),
                (material.FloatProperties.Count + material.IntegerProperties.Count).ToString(CultureInfo.CurrentCulture),
                BuildPropertyNameSummary(
                    material.FloatProperties.Select(static property => property.Name)
                        .Concat(material.IntegerProperties.Select(static property => property.Name)))),
            new(
                _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewSemanticHintsTitle),
                semanticBadges.Count.ToString(CultureInfo.CurrentCulture),
                BuildPropertyNameSummary(semanticBadges.Select(static badge => badge.Text)))
        ];
    }

    private static string BuildPropertyNameSummary(IEnumerable<string> propertyNames)
    {
        var summary = string.Join(
            ", ",
            propertyNames
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Select(static name => name.Trim())
                .Take(3));

        return summary;
    }

    private IReadOnlyList<MaterialPreviewTextureRowDisplayState> BuildTextureRows(
        AssetPreviewMaterialDto material,
        AssetDetailsDto details,
        string openButtonText,
        string openButtonToolTipText)
    {
        var mainTextureProperty = MaterialTextureDependencyResolver.SelectMainTextureProperty(material);
        return material.TextureProperties
            .Select(property => BuildTextureRow(
                property,
                details,
                ReferenceEquals(property, mainTextureProperty),
                openButtonText,
                openButtonToolTipText))
            .ToArray();
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

    private string BuildShaderResolutionText(
        AssetPreviewMaterialShaderDto? shader,
        AssetDependencyDto? resolvedShaderDependency)
    {
        if (shader is null)
        {
            return string.Empty;
        }

        if (shader.PathId == 0)
        {
            return _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewShaderMissingText);
        }

        if (resolvedShaderDependency is not null)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewShaderResolvedFormat,
                resolvedShaderDependency.Name,
                ResolveAssetTypeDisplayName(resolvedShaderDependency));
        }

        return shader.IsExternal
            ? _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewShaderExternalUnresolvedText)
            : _localizationService.GetRequiredString(ResourceKeys.InspectorMaterialPreviewShaderInternalUnresolvedText);
    }

    private MaterialPreviewTextureRowDisplayState BuildTextureRow(
        AssetPreviewMaterialTexturePropertyDto property,
        AssetDetailsDto details,
        bool isMainTextureCandidate,
        string openButtonText,
        string openButtonToolTipText)
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
            IsMainTextureCandidate: isMainTextureCandidate,
            TransformText: transformText,
            Target: resolvedTextureDependency is null ? null : CreateNavigationTarget(resolvedTextureDependency),
            OpenButtonText: openButtonText,
            OpenButtonToolTipText: openButtonToolTipText);
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
            ValueText: BuildColorValueText(property));
    }

    private string BuildTextureReferenceSummaryText(
        AssetPreviewMaterialDto material,
        AssetDetailsDto details)
    {
        var textureProperties = material.TextureProperties
            .Where(static property => property.PathId is { } pathId && pathId != 0)
            .ToArray();
        if (textureProperties.Length == 0)
        {
            return string.Empty;
        }

        var resolvedCount = 0;
        var externalUnresolvedCount = 0;
        var internalUnresolvedCount = 0;
        foreach (var property in textureProperties)
        {
            if (MaterialTextureDependencyResolver.ResolveTextureDependency(property, details) is not null)
            {
                resolvedCount++;
            }
            else if (property.IsExternal)
            {
                externalUnresolvedCount++;
            }
            else
            {
                internalUnresolvedCount++;
            }
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMaterialPreviewTextureReferenceSummaryFormat,
            resolvedCount.ToString(CultureInfo.CurrentCulture),
            textureProperties.Length.ToString(CultureInfo.CurrentCulture),
            externalUnresolvedCount.ToString(CultureInfo.CurrentCulture),
            internalUnresolvedCount.ToString(CultureInfo.CurrentCulture));
    }

    private MaterialPreviewTintDisplayState? BuildMainTextureTint(AssetPreviewMaterialDto material)
    {
        var tintProperty = material.ColorProperties.FirstOrDefault(static property =>
            string.Equals(property.Name, MainColorPropertyName, StringComparison.OrdinalIgnoreCase));
        if (tintProperty is null)
        {
            return null;
        }

        return new MaterialPreviewTintDisplayState(
            Text: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMaterialPreviewMainTextureTintFormat,
                tintProperty.Name,
                BuildColorValueText(tintProperty)),
            R: tintProperty.R,
            G: tintProperty.G,
            B: tintProperty.B,
            A: tintProperty.A);
    }

    private string BuildColorValueText(AssetPreviewMaterialColorPropertyDto property)
    {
        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMaterialPreviewColorValueFormat,
            FormatNumber(property.R),
            FormatNumber(property.G),
            FormatNumber(property.B),
            FormatNumber(property.A));
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

    private static ResolvedAssetNavigationTarget CreateNavigationTarget(AssetDependencyDto dependency)
    {
        return new ResolvedAssetNavigationTarget(
            dependency.AssetId,
            dependency.BundleId,
            dependency.PathId,
            dependency.Name,
            ResolveAssetTypeDisplayName(dependency));
    }

}

public sealed record MaterialPreviewDisplayState(
    string AssetNameText,
    string ShaderNameText,
    string ShaderReferenceText,
    string ShaderResolutionText,
    string? ResolvedShaderAssetId,
    string PropertyCountText,
    string MainTexturePreviewTitleText,
    string MainTexturePreviewLimitText,
    MaterialPreviewTintDisplayState? MainTextureTint,
    string SemanticSectionTitle,
    IReadOnlyList<MaterialPreviewSemanticBadgeDisplayState> SemanticBadges,
    string TextureSectionTitle,
    string ColorSectionTitle,
    string NumberSectionTitle,
    string EmptySectionText,
    string TruncatedText,
    IReadOnlyList<MaterialPreviewTextureRowDisplayState> TextureRows,
    IReadOnlyList<MaterialPreviewColorRowDisplayState> ColorRows,
    IReadOnlyList<MaterialPreviewNumberRowDisplayState> NumberRows,
    MaterialPreviewAppearanceDisplayState? Appearance = null,
    string TextureReferenceSummaryText = "",
    ResolvedAssetNavigationTarget? ShaderTarget = null,
    string OpenResolvedAssetButtonText = "",
    string OpenResolvedAssetButtonToolTipText = "",
    IReadOnlyList<MaterialPreviewSummaryCardDisplayState>? SummaryCards = null,
    IReadOnlyList<InspectorInfoCardViewModel>? DetailCards = null);

public sealed record MaterialPreviewSummaryCardDisplayState(
    string TitleText,
    string ValueText,
    string DetailText);

public sealed record MaterialPreviewAppearanceDisplayState(
    string TitleText,
    string NoteText,
    string BaseColorText,
    string AlphaText,
    string MetallicText,
    string SmoothnessText,
    double R,
    double G,
    double B,
    double A,
    double Metallic,
    double Smoothness,
    double EmissionStrength);

public sealed record MaterialPreviewTextureRowDisplayState(
    string PropertyName,
    string TargetText,
    string ResolutionText,
    string? ResolvedTextureAssetId,
    bool IsMainTextureCandidate,
    string TransformText,
    ResolvedAssetNavigationTarget? Target = null,
    string OpenButtonText = "",
    string OpenButtonToolTipText = "");

public sealed record MaterialPreviewSemanticBadgeDisplayState(string Text);

public sealed record MaterialPreviewTintDisplayState(
    string Text,
    double R,
    double G,
    double B,
    double A);

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
