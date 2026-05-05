using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Assets.Queries;

internal static class AssetPreviewDtoMapper
{
    private static readonly JsonSerializerOptions SidecarJsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static AssetPreviewDto MapPreviewDto(
        GetAssetPreviewQuery query,
        PreviewCacheDescriptorDto descriptor,
        byte[] content,
        byte[]? imageFocusContent,
        string? previewPayloadFilePath,
        bool isFromCache,
        AssetPreviewDiagnosticDto diagnostic)
    {
        var previewKind = Enum.TryParse<AssetPreviewKind>(descriptor.PreviewKind, ignoreCase: true, out var parsedPreviewKind)
            ? parsedPreviewKind
            : AssetPreviewKind.Summary;

        return previewKind switch
        {
            AssetPreviewKind.Mesh => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: TryDeserialize<AssetPreviewMeshGeometryDto>(content),
                material: null,
                animationClip: null,
                font: null,
                gameObject: null,
                monoBehaviour: null,
                animatorController: null),
            AssetPreviewKind.Material => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: null,
                material: TryDeserialize<AssetPreviewMaterialDto>(content),
                animationClip: null,
                font: null,
                gameObject: null,
                monoBehaviour: null,
                animatorController: null),
            AssetPreviewKind.AnimationClip => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: null,
                material: null,
                animationClip: TryDeserialize<AssetPreviewAnimationClipDto>(content),
                font: null,
                gameObject: null,
                monoBehaviour: null,
                animatorController: null),
            AssetPreviewKind.Font => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: null,
                material: null,
                animationClip: null,
                font: TryDeserialize<AssetPreviewFontDto>(content),
                gameObject: null,
                monoBehaviour: null,
                animatorController: null),
            AssetPreviewKind.GameObject => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: null,
                material: null,
                animationClip: null,
                font: null,
                gameObject: TryDeserialize<AssetPreviewGameObjectDto>(content),
                monoBehaviour: null,
                animatorController: null),
            AssetPreviewKind.MonoBehaviour => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: null,
                material: null,
                animationClip: null,
                font: null,
                gameObject: null,
                monoBehaviour: TryDeserialize<AssetPreviewMonoBehaviourDto>(content),
                animatorController: null),
            AssetPreviewKind.AnimatorController => CreateStructuredPreview(
                query,
                descriptor,
                previewKind,
                previewPayloadFilePath,
                isFromCache,
                diagnostic,
                imageFocusContent,
                meshGeometry: null,
                material: null,
                animationClip: null,
                font: null,
                gameObject: null,
                monoBehaviour: null,
                animatorController: TryDeserialize<AssetPreviewAnimatorControllerDto>(content)),
            _ => PreviewKindUsesBinaryPayload(previewKind)
                ? CreateBinaryPreview(
                    query,
                    descriptor,
                    content,
                    imageFocusContent,
                    previewKind,
                    previewPayloadFilePath,
                    isFromCache,
                    diagnostic)
                : CreateTextPreview(
                    query,
                    descriptor,
                    content,
                    imageFocusContent,
                    previewKind,
                    previewPayloadFilePath,
                    isFromCache,
                    diagnostic)
        };
    }

    private static AssetPreviewDto CreateStructuredPreview(
        GetAssetPreviewQuery query,
        PreviewCacheDescriptorDto descriptor,
        AssetPreviewKind previewKind,
        string? previewPayloadFilePath,
        bool isFromCache,
        AssetPreviewDiagnosticDto diagnostic,
        byte[]? imageFocusContent,
        AssetPreviewMeshGeometryDto? meshGeometry,
        AssetPreviewMaterialDto? material,
        AssetPreviewAnimationClipDto? animationClip,
        AssetPreviewFontDto? font,
        AssetPreviewGameObjectDto? gameObject,
        AssetPreviewMonoBehaviourDto? monoBehaviour,
        AssetPreviewAnimatorControllerDto? animatorController)
    {
        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: previewKind,
            ContentType: descriptor.ContentType,
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems: descriptor.PreviewInfoItems,
            PreviewMessage: descriptor.PreviewMessage,
            PreferredDisplaySizeInBytes: descriptor.PreferredDisplaySizeInBytes,
            IsFromCache: isFromCache,
            GeneratedAtUtc: descriptor.GeneratedAtUtc,
            Diagnostic: diagnostic,
            ImageOverlay: descriptor.ImageOverlay,
            ImageFocus: HydrateImageFocus(descriptor.ImageFocus, imageFocusContent),
            MeshGeometry: meshGeometry,
            Material: material,
            AnimationClip: animationClip,
            Font: font,
            GameObject: gameObject,
            MonoBehaviour: monoBehaviour,
            AnimatorController: animatorController);
    }

    private static AssetPreviewDto CreateBinaryPreview(
        GetAssetPreviewQuery query,
        PreviewCacheDescriptorDto descriptor,
        byte[] content,
        byte[]? imageFocusContent,
        AssetPreviewKind previewKind,
        string? previewPayloadFilePath,
        bool isFromCache,
        AssetPreviewDiagnosticDto diagnostic)
    {
        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: previewKind,
            ContentType: descriptor.ContentType,
            BinaryContent: content,
            TextContent: null,
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems: descriptor.PreviewInfoItems,
            PreviewMessage: descriptor.PreviewMessage,
            PreferredDisplaySizeInBytes: descriptor.PreferredDisplaySizeInBytes,
            IsFromCache: isFromCache,
            GeneratedAtUtc: descriptor.GeneratedAtUtc,
            Diagnostic: diagnostic,
            ImageOverlay: descriptor.ImageOverlay,
            ImageFocus: HydrateImageFocus(descriptor.ImageFocus, imageFocusContent));
    }

    private static AssetPreviewDto CreateTextPreview(
        GetAssetPreviewQuery query,
        PreviewCacheDescriptorDto descriptor,
        byte[] content,
        byte[]? imageFocusContent,
        AssetPreviewKind previewKind,
        string? previewPayloadFilePath,
        bool isFromCache,
        AssetPreviewDiagnosticDto diagnostic)
    {
        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: previewKind,
            ContentType: descriptor.ContentType,
            BinaryContent: null,
            TextContent: Encoding.UTF8.GetString(content),
            PreviewPayloadFilePath: previewPayloadFilePath,
            PreviewInfoItems: descriptor.PreviewInfoItems,
            PreviewMessage: descriptor.PreviewMessage,
            PreferredDisplaySizeInBytes: descriptor.PreferredDisplaySizeInBytes,
            IsFromCache: isFromCache,
            GeneratedAtUtc: descriptor.GeneratedAtUtc,
            Diagnostic: diagnostic,
            ImageOverlay: descriptor.ImageOverlay,
            ImageFocus: HydrateImageFocus(descriptor.ImageFocus, imageFocusContent));
    }

    private static T? TryDeserialize<T>(byte[] content)
        where T : class
    {
        if (content is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(
                content,
                SidecarJsonSerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AssetPreviewImageFocusDto? HydrateImageFocus(
        AssetPreviewImageFocusDto? imageFocus,
        byte[]? content)
    {
        if (imageFocus is null || content is not { Length: > 0 })
        {
            return null;
        }

        return imageFocus with { BinaryContent = content };
    }

    private static bool PreviewKindUsesBinaryPayload(AssetPreviewKind previewKind)
    {
        return previewKind is AssetPreviewKind.Image or AssetPreviewKind.Audio;
    }
}
