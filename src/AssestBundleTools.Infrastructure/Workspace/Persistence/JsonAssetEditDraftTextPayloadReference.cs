using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

internal readonly record struct JsonAssetEditDraftTextPayloadReference(
    PreviewCacheDescriptorDto Descriptor,
    string? PayloadFilePath,
    long PayloadLength,
    JsonAssetEditDraftTextPatchManifestDto? PatchManifest,
    JsonAssetEditDraftTextCompositeReader? CompositeReader);
