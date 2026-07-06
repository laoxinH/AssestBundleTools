using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using Runtime = AssestBundleTools.Infrastructure.ThirdParty.Spine.V42;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed partial class V42SpineAnimationRuntimeAdapter
{
    private static Runtime.SkeletonData? ReadSkeletonData(
        SpineAnimationRuntimePreviewRequestDto request,
        IAssetPreviewTextSourceReader textSourceReader)
    {
        var atlases = CreateAtlases(request.AtlasTexts, request.AtlasTextSources, textSourceReader);
        if (request.SkeletonBinaryContent is { Length: > 0 } skeletonBinaryContent)
        {
            var binaryLoader = new Runtime.SkeletonBinary(atlases);
            using var stream = new MemoryStream(skeletonBinaryContent);
            return binaryLoader.ReadSkeletonData(stream);
        }

        if (string.IsNullOrWhiteSpace(request.SkeletonJson) &&
            request.SkeletonJsonSource is null)
        {
            throw new InvalidDataException("Skeleton JSON or binary content is required.");
        }

        var jsonLoader = new Runtime.SkeletonJson(atlases);
        using var reader = OpenSkeletonJsonReader(
            request.SkeletonJson,
            request.SkeletonJsonSource,
            textSourceReader);
        return jsonLoader.ReadSkeletonData(reader);
    }

    private static Runtime.Atlas[] CreateAtlases(
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto>? atlasSources,
        IAssetPreviewTextSourceReader textSourceReader)
    {
        if (atlasSources is { Count: > 0 })
        {
            return atlasSources
                .Where(static source => source.Reference is not null || !string.IsNullOrWhiteSpace(source.InlineText))
                .Select(source =>
                {
                    using var reader = textSourceReader.OpenTextReader(source);
                    return new Runtime.Atlas(reader, string.Empty, new NoOpTextureLoader());
                })
                .ToArray();
        }

        return atlasTexts
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .Select(static text => new Runtime.Atlas(
                new StringReader(text),
                string.Empty,
                new NoOpTextureLoader()))
            .ToArray();
    }

    private static TextReader OpenSkeletonJsonReader(
        string? skeletonJson,
        AssetPreviewTextSourceDto? skeletonJsonSource,
        IAssetPreviewTextSourceReader textSourceReader) =>
        skeletonJsonSource is not null
            ? textSourceReader.OpenTextReader(skeletonJsonSource)
            : new StringReader(skeletonJson ?? string.Empty);
}
