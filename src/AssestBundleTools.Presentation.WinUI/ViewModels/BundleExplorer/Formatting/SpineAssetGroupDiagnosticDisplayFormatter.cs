using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

/// <summary>
/// 负责把 Spine 资源组结构化诊断投影为检查器可显示文本。
/// </summary>
internal sealed class SpineAssetGroupDiagnosticDisplayFormatter
{
    private readonly ILocalizationService _localizationService;

    public SpineAssetGroupDiagnosticDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string BuildDiagnosticText(SpineAssetGroupDiagnosticDto diagnostic)
    {
        var summaryText = _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupDiagnosticFormat,
            ResolveDiagnosticKindText(diagnostic.Kind),
            diagnostic.GroupKey,
            diagnostic.RelatedParts.Count.ToString(CultureInfo.CurrentCulture));
        var detailText = BuildDiagnosticDetailText(diagnostic);
        return string.IsNullOrWhiteSpace(detailText)
            ? summaryText
            : _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupDiagnosticWithDetailFormat,
                summaryText,
                detailText);
    }

    private string ResolveDiagnosticKindText(SpineAssetGroupDiagnosticKind kind)
    {
        return kind switch
        {
            SpineAssetGroupDiagnosticKind.MissingSkeleton => GetString(ResourceKeys.InspectorSpineGroupDiagnosticMissingSkeleton),
            SpineAssetGroupDiagnosticKind.MissingAtlas => GetString(ResourceKeys.InspectorSpineGroupDiagnosticMissingAtlas),
            SpineAssetGroupDiagnosticKind.MissingTexturePage => GetString(ResourceKeys.InspectorSpineGroupDiagnosticMissingTexturePage),
            SpineAssetGroupDiagnosticKind.MultipleSkeletons => GetString(ResourceKeys.InspectorSpineGroupDiagnosticMultipleSkeletons),
            SpineAssetGroupDiagnosticKind.MultipleAtlases => GetString(ResourceKeys.InspectorSpineGroupDiagnosticMultipleAtlases),
            SpineAssetGroupDiagnosticKind.AtlasPreviewUnavailable => GetString(ResourceKeys.InspectorSpineGroupDiagnosticAtlasPreviewUnavailable),
            SpineAssetGroupDiagnosticKind.AtlasParseFailed => GetString(ResourceKeys.InspectorSpineGroupDiagnosticAtlasParseFailed),
            SpineAssetGroupDiagnosticKind.AtlasPageTextureUnmatched => GetString(ResourceKeys.InspectorSpineGroupDiagnosticAtlasPageTextureUnmatched),
            SpineAssetGroupDiagnosticKind.TexturePreviewUnavailable => GetString(ResourceKeys.InspectorSpineGroupDiagnosticTexturePreviewUnavailable),
            SpineAssetGroupDiagnosticKind.AtlasPageTextureSizeMismatch => GetString(ResourceKeys.InspectorSpineGroupDiagnosticAtlasPageTextureSizeMismatch),
            SpineAssetGroupDiagnosticKind.SkeletonPreviewUnavailable => GetString(ResourceKeys.InspectorSpineGroupDiagnosticSkeletonPreviewUnavailable),
            SpineAssetGroupDiagnosticKind.SkeletonJsonParseFailed => GetString(ResourceKeys.InspectorSpineGroupDiagnosticSkeletonJsonParseFailed),
            SpineAssetGroupDiagnosticKind.AnimationPreviewUnsupportedFeature => GetString(ResourceKeys.InspectorSpineGroupDiagnosticAnimationPreviewUnsupportedFeature),
            SpineAssetGroupDiagnosticKind.AnimationPreviewRuntimeUnsupported => GetString(ResourceKeys.InspectorSpineGroupDiagnosticAnimationPreviewRuntimeUnsupported),
            _ => kind.ToString()
        };
    }

    private string BuildDiagnosticDetailText(SpineAssetGroupDiagnosticDto diagnostic)
    {
        if (diagnostic.AnimationUnsupportedFeature is { } unsupportedFeature)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupDiagnosticAnimationUnsupportedFeatureDetailFormat,
                ResolveAnimationUnsupportedFeatureText(unsupportedFeature));
        }

        if (diagnostic.Kind == SpineAssetGroupDiagnosticKind.AnimationPreviewRuntimeUnsupported)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupDiagnosticRuntimeDetailFormat,
                string.IsNullOrWhiteSpace(diagnostic.SpineVersion) ? "-" : diagnostic.SpineVersion,
                string.IsNullOrWhiteSpace(diagnostic.RuntimeAdapterKey) ? "-" : diagnostic.RuntimeAdapterKey);
        }

        if (string.IsNullOrWhiteSpace(diagnostic.AtlasPageName))
        {
            return string.Empty;
        }

        if (diagnostic is { AtlasPageWidth: > 0, AtlasPageHeight: > 0, TextureWidth: > 0, TextureHeight: > 0 })
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupDiagnosticTextureSizeDetailFormat,
                diagnostic.AtlasPageName,
                diagnostic.AtlasPageWidth.Value.ToString(CultureInfo.CurrentCulture),
                diagnostic.AtlasPageHeight.Value.ToString(CultureInfo.CurrentCulture),
                diagnostic.TextureWidth.Value.ToString(CultureInfo.CurrentCulture),
                diagnostic.TextureHeight.Value.ToString(CultureInfo.CurrentCulture));
        }

        if (diagnostic is { AtlasPageWidth: > 0, AtlasPageHeight: > 0 })
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpineGroupDiagnosticPageSizeDetailFormat,
                diagnostic.AtlasPageName,
                diagnostic.AtlasPageWidth.Value.ToString(CultureInfo.CurrentCulture),
                diagnostic.AtlasPageHeight.Value.ToString(CultureInfo.CurrentCulture));
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.InspectorSpineGroupDiagnosticPageDetailFormat,
            diagnostic.AtlasPageName);
    }

    private string ResolveAnimationUnsupportedFeatureText(SpineAnimationUnsupportedFeatureKind feature)
    {
        return feature switch
        {
            SpineAnimationUnsupportedFeatureKind.BinarySkeleton => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureBinarySkeleton),
            SpineAnimationUnsupportedFeatureKind.NonDefaultSkin => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureNonDefaultSkin),
            SpineAnimationUnsupportedFeatureKind.MeshAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureMeshAttachment),
            SpineAnimationUnsupportedFeatureKind.WeightedVertices => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureWeightedVertices),
            SpineAnimationUnsupportedFeatureKind.LinkedMeshAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureLinkedMeshAttachment),
            SpineAnimationUnsupportedFeatureKind.BoundingBoxAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureBoundingBoxAttachment),
            SpineAnimationUnsupportedFeatureKind.PathAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeaturePathAttachment),
            SpineAnimationUnsupportedFeatureKind.ClippingAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureClippingAttachment),
            SpineAnimationUnsupportedFeatureKind.PointAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeaturePointAttachment),
            SpineAnimationUnsupportedFeatureKind.UnknownAttachment => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureUnknownAttachment),
            SpineAnimationUnsupportedFeatureKind.SlotColorTimeline => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureSlotColorTimeline),
            SpineAnimationUnsupportedFeatureKind.SlotTwoColorTimeline => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureSlotTwoColorTimeline),
            SpineAnimationUnsupportedFeatureKind.BoneShearTimeline => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureBoneShearTimeline),
            SpineAnimationUnsupportedFeatureKind.DeformTimeline => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureDeformTimeline),
            SpineAnimationUnsupportedFeatureKind.DrawOrderTimeline => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureDrawOrderTimeline),
            SpineAnimationUnsupportedFeatureKind.EventTimeline => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureEventTimeline),
            SpineAnimationUnsupportedFeatureKind.IkConstraint => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureIkConstraint),
            SpineAnimationUnsupportedFeatureKind.TransformConstraint => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureTransformConstraint),
            SpineAnimationUnsupportedFeatureKind.PathConstraint => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeaturePathConstraint),
            SpineAnimationUnsupportedFeatureKind.PhysicsConstraint => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeaturePhysicsConstraint),
            SpineAnimationUnsupportedFeatureKind.BezierCurve => GetString(ResourceKeys.InspectorSpineGroupAnimationUnsupportedFeatureBezierCurve),
            _ => feature.ToString()
        };
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
