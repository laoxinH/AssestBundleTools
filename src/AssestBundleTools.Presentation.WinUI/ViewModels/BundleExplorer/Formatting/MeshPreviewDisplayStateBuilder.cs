using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class MeshPreviewDisplayStateBuilder
{
    private readonly ILocalizationService _localizationService;

    public MeshPreviewDisplayStateBuilder(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public MeshPreviewDisplayState Build(
        AssetPreviewDto preview,
        AssetDetailsDto? details)
    {
        ArgumentNullException.ThrowIfNull(preview);

        var geometry = preview.MeshGeometry;
        if (preview.Kind != AssetPreviewKind.Mesh || geometry is null)
        {
            return new MeshPreviewDisplayState(string.Empty);
        }

        var textureAssociationReferences = BuildTextureAssociationReferences(details);
        return new MeshPreviewDisplayState(
            BadgeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMeshPreviewBadgeFormat,
                geometry.SourceVertexCount.ToString(CultureInfo.CurrentCulture),
                geometry.Edges.Count.ToString(CultureInfo.CurrentCulture)),
            UvBadgeText: _localizationService.GetFormattedString(
                ResourceKeys.InspectorMeshPreviewUvBadgeFormat,
                (geometry.Uv0?.Count ?? 0).ToString(CultureInfo.CurrentCulture),
                geometry.Edges.Count.ToString(CultureInfo.CurrentCulture)),
            TextureAssociationText: BuildTextureAssociationText(textureAssociationReferences),
            TextureAssociationReferences: textureAssociationReferences);
    }

    private string BuildTextureAssociationText(
        IReadOnlyList<ResolvedAssetReferenceDisplayState> references)
    {
        if (references.Count == 0)
        {
            return string.Empty;
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorMeshPreviewTextureAssociationFormat,
            string.Join(", ", references.Select(static reference => reference.Text)));
    }

    private IReadOnlyList<ResolvedAssetReferenceDisplayState> BuildTextureAssociationReferences(
        AssetDetailsDto? details)
    {
        var references = new List<ResolvedAssetReferenceDisplayState>();
        var openButtonText = _localizationService.GetRequiredString(ResourceKeys.InspectorResolvedAssetOpenButton);
        var openButtonToolTipText = _localizationService.GetRequiredString(ResourceKeys.InspectorResolvedAssetOpenButtonToolTip);
        foreach (var dependency in MeshTextureAssociationCandidateResolver.CollectDisplayCandidates(details, maxCount: 4))
        {
            var assetTypeText = ResolveAssociationTypeText(dependency);
            references.Add(new ResolvedAssetReferenceDisplayState(
                Text: _localizationService.GetFormattedString(
                    ResourceKeys.InspectorMeshPreviewTextureAssociationItemFormat,
                    dependency.Name,
                    assetTypeText),
                Target: new ResolvedAssetNavigationTarget(
                    dependency.AssetId,
                    dependency.BundleId,
                    dependency.PathId,
                    dependency.Name,
                    assetTypeText),
                OpenButtonText: openButtonText,
                OpenButtonToolTipText: openButtonToolTipText));
        }

        return references;
    }

    private string ResolveAssociationTypeText(AssetDependencyDto dependency)
    {
        if (!string.IsNullOrWhiteSpace(dependency.AssetTypeDisplayName))
        {
            return dependency.AssetTypeDisplayName.Trim();
        }

        return dependency.AssetType switch
        {
            AssetType.Material => _localizationService.GetRequiredString(ResourceKeys.AssetTypeDisplayMaterial),
            AssetType.Texture2D => _localizationService.GetRequiredString(ResourceKeys.AssetTypeDisplayTexture2D),
            AssetType.Sprite => _localizationService.GetRequiredString(ResourceKeys.AssetTypeDisplaySprite),
            _ => dependency.AssetType.ToString()
        };
    }
}
