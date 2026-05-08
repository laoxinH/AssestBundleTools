using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private static IReadOnlyList<SpineAssetGroupDiagnosticDto> BuildUnsupportedFeatureDiagnostics(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        JsonElement root)
    {
        var features = new HashSet<SpineAnimationUnsupportedFeatureKind>();
        AddSkinDiagnostics(root, features);
        AddSkinAttachmentDiagnostics(root, features);
        AddConstraintDiagnostics(root, features);
        AddAnimationTimelineDiagnostics(root, features);

        return features
            .OrderBy(static feature => feature)
            .Select(feature => CreateUnsupportedFeatureDiagnostic(groupKey, skeletonAsset, feature))
            .ToArray();
    }

    private static SpineAssetGroupDiagnosticDto CreateUnsupportedFeatureDiagnostic(
        string groupKey,
        SpineAssetGroupPartDto skeletonAsset,
        SpineAnimationUnsupportedFeatureKind feature) =>
        new(
            SpineAssetGroupDiagnosticKind.AnimationPreviewUnsupportedFeature,
            groupKey,
            [skeletonAsset],
            AnimationUnsupportedFeature: feature);

    private static IReadOnlyList<SpineAssetGroupDiagnosticDto> BuildRuntimeDiagnostics(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        SpineAnimationRuntimeDto runtime)
    {
        if (runtime.SupportKind == SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable)
        {
            return Array.Empty<SpineAssetGroupDiagnosticDto>();
        }

        return
        [
            new SpineAssetGroupDiagnosticDto(
                SpineAssetGroupDiagnosticKind.AnimationPreviewRuntimeUnsupported,
                groupKey,
                [skeletonAsset],
                SpineVersion: runtime.SpineVersion,
                RuntimeAdapterKey: runtime.AdapterKey)
        ];
    }

    private static void AddSkinDiagnostics(
        JsonElement root,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (!root.TryGetProperty("skins", out var skinsElement))
        {
            return;
        }

        var hasNonDefaultSkin = skinsElement.ValueKind switch
        {
            JsonValueKind.Object => HasObjectNonDefaultSkin(skinsElement),
            JsonValueKind.Array => HasArrayNonDefaultSkin(skinsElement),
            _ => false
        };
        if (hasNonDefaultSkin)
        {
            features.Add(SpineAnimationUnsupportedFeatureKind.NonDefaultSkin);
        }
    }

    private static bool HasObjectNonDefaultSkin(JsonElement skinsElement)
    {
        var skinNames = skinsElement.EnumerateObject()
            .Select(static skin => skin.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        return skinNames.Length > 1 ||
               skinNames.Any(static name => !string.Equals(name, "default", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasArrayNonDefaultSkin(JsonElement skinsElement)
    {
        var skinNames = skinsElement.EnumerateArray()
            .Select(static skin => GetString(skin, "name"))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToArray();
        return skinNames.Length > 1 ||
               skinNames.Any(static name => !string.Equals(name, "default", StringComparison.OrdinalIgnoreCase));
    }

    private static void AddSkinAttachmentDiagnostics(
        JsonElement root,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        foreach (var attachmentsRoot in EnumerateSkinAttachments(root))
        {
            foreach (var slotProperty in attachmentsRoot.EnumerateObject())
            {
                if (slotProperty.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var attachmentProperty in slotProperty.Value.EnumerateObject())
                {
                    AddAttachmentDiagnostics(attachmentProperty.Value, features);
                }
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateSkinAttachments(JsonElement root)
    {
        if (!root.TryGetProperty("skins", out var skinsElement))
        {
            yield break;
        }

        if (skinsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var skinProperty in skinsElement.EnumerateObject())
            {
                if (skinProperty.Value.ValueKind == JsonValueKind.Object)
                {
                    yield return skinProperty.Value;
                }
            }

            yield break;
        }

        if (skinsElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var skin in skinsElement.EnumerateArray())
        {
            if (skin.ValueKind == JsonValueKind.Object &&
                skin.TryGetProperty("attachments", out var attachmentsRoot) &&
                attachmentsRoot.ValueKind == JsonValueKind.Object)
            {
                yield return attachmentsRoot;
            }
        }
    }

    private static void AddAttachmentDiagnostics(
        JsonElement attachmentElement,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (attachmentElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = GetString(attachmentElement, "type");
        if (string.IsNullOrWhiteSpace(type) ||
            string.Equals(type, "region", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        switch (type.Trim().ToLowerInvariant())
        {
            case "mesh":
                features.Add(SpineAnimationUnsupportedFeatureKind.MeshAttachment);
                break;
            case "linkedmesh":
                features.Add(SpineAnimationUnsupportedFeatureKind.LinkedMeshAttachment);
                break;
            case "boundingbox":
                features.Add(SpineAnimationUnsupportedFeatureKind.BoundingBoxAttachment);
                break;
            case "path":
                features.Add(SpineAnimationUnsupportedFeatureKind.PathAttachment);
                break;
            case "clipping":
                features.Add(SpineAnimationUnsupportedFeatureKind.ClippingAttachment);
                break;
            case "point":
                features.Add(SpineAnimationUnsupportedFeatureKind.PointAttachment);
                break;
            default:
                features.Add(SpineAnimationUnsupportedFeatureKind.UnknownAttachment);
                break;
        }

        if (TryGetArray(attachmentElement, "bones", out _) ||
            HasWeightedVertices(attachmentElement))
        {
            features.Add(SpineAnimationUnsupportedFeatureKind.WeightedVertices);
        }
    }

    private static bool HasWeightedVertices(JsonElement attachmentElement)
    {
        if (!TryGetArray(attachmentElement, "vertices", out var verticesElement))
        {
            return false;
        }

        return verticesElement.GetArrayLength() > 0 &&
               TryGetArray(attachmentElement, "bones", out var bonesElement) &&
               bonesElement.GetArrayLength() > 0;
    }

    private static void AddConstraintDiagnostics(
        JsonElement root,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        AddArrayFeature(root, "ik", SpineAnimationUnsupportedFeatureKind.IkConstraint, features);
        AddArrayFeature(root, "transform", SpineAnimationUnsupportedFeatureKind.TransformConstraint, features);
        AddArrayFeature(root, "path", SpineAnimationUnsupportedFeatureKind.PathConstraint, features);
        AddArrayFeature(root, "physics", SpineAnimationUnsupportedFeatureKind.PhysicsConstraint, features);
    }

    private static void AddAnimationTimelineDiagnostics(
        JsonElement root,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (!TryGetObject(root, "animations", out var animationsElement))
        {
            return;
        }

        foreach (var animationProperty in animationsElement.EnumerateObject())
        {
            var animationElement = animationProperty.Value;
            AddAnimationFeature(animationElement, "drawOrder", SpineAnimationUnsupportedFeatureKind.DrawOrderTimeline, features);
            AddAnimationFeature(animationElement, "draworder", SpineAnimationUnsupportedFeatureKind.DrawOrderTimeline, features);
            AddAnimationFeature(animationElement, "deform", SpineAnimationUnsupportedFeatureKind.DeformTimeline, features);
            AddAnimationFeature(animationElement, "ffd", SpineAnimationUnsupportedFeatureKind.DeformTimeline, features);
            AddAnimationFeature(animationElement, "events", SpineAnimationUnsupportedFeatureKind.EventTimeline, features);
            AddAnimationFeature(animationElement, "ik", SpineAnimationUnsupportedFeatureKind.IkConstraint, features);
            AddAnimationFeature(animationElement, "transform", SpineAnimationUnsupportedFeatureKind.TransformConstraint, features);
            AddAnimationFeature(animationElement, "path", SpineAnimationUnsupportedFeatureKind.PathConstraint, features);
            AddAnimationFeature(animationElement, "physics", SpineAnimationUnsupportedFeatureKind.PhysicsConstraint, features);
            AddBoneTimelineDiagnostics(animationElement, features);
            AddSlotTimelineDiagnostics(animationElement, features);
            AddCurveDiagnostics(animationElement, features);
        }
    }

    private static void AddBoneTimelineDiagnostics(
        JsonElement animationElement,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (!TryGetObject(animationElement, "bones", out var bonesElement))
        {
            return;
        }

        foreach (var boneProperty in bonesElement.EnumerateObject())
        {
            AddAnimationFeature(boneProperty.Value, "shear", SpineAnimationUnsupportedFeatureKind.BoneShearTimeline, features);
        }
    }

    private static void AddSlotTimelineDiagnostics(
        JsonElement animationElement,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (!TryGetObject(animationElement, "slots", out var slotsElement))
        {
            return;
        }

        foreach (var slotProperty in slotsElement.EnumerateObject())
        {
            var slotElement = slotProperty.Value;
            AddAnimationFeature(slotElement, "color", SpineAnimationUnsupportedFeatureKind.SlotColorTimeline, features);
            AddAnimationFeature(slotElement, "rgb", SpineAnimationUnsupportedFeatureKind.SlotColorTimeline, features);
            AddAnimationFeature(slotElement, "rgba", SpineAnimationUnsupportedFeatureKind.SlotColorTimeline, features);
            AddAnimationFeature(slotElement, "alpha", SpineAnimationUnsupportedFeatureKind.SlotColorTimeline, features);
            AddAnimationFeature(slotElement, "twoColor", SpineAnimationUnsupportedFeatureKind.SlotTwoColorTimeline, features);
            AddAnimationFeature(slotElement, "rgb2", SpineAnimationUnsupportedFeatureKind.SlotTwoColorTimeline, features);
            AddAnimationFeature(slotElement, "rgba2", SpineAnimationUnsupportedFeatureKind.SlotTwoColorTimeline, features);
        }
    }

    private static void AddCurveDiagnostics(
        JsonElement element,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("curve", out var curveElement) &&
                    IsBezierCurve(curveElement))
                {
                    features.Add(SpineAnimationUnsupportedFeatureKind.BezierCurve);
                }

                foreach (var property in element.EnumerateObject())
                {
                    AddCurveDiagnostics(property.Value, features);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AddCurveDiagnostics(item, features);
                }

                break;
        }
    }

    private static bool IsBezierCurve(JsonElement curveElement)
    {
        return curveElement.ValueKind switch
        {
            JsonValueKind.Array => true,
            JsonValueKind.String => !string.Equals(curveElement.GetString(), "linear", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(curveElement.GetString(), "stepped", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static void AddArrayFeature(
        JsonElement element,
        string propertyName,
        SpineAnimationUnsupportedFeatureKind feature,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (TryGetArray(element, propertyName, out var property) &&
            property.GetArrayLength() > 0)
        {
            features.Add(feature);
        }
    }

    private static void AddAnimationFeature(
        JsonElement element,
        string propertyName,
        SpineAnimationUnsupportedFeatureKind feature,
        HashSet<SpineAnimationUnsupportedFeatureKind> features)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
        {
            features.Add(feature);
        }
    }
}
