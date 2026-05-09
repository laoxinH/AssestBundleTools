using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private static readonly ISpineAnimationRuntimePreviewService NoOpRuntimePreviewService =
        new NoOpSpineAnimationRuntimePreviewService();

    private readonly ISpineAnimationRuntimePreviewService _runtimePreviewService;

    private async Task<SpineAnimationRuntimePreviewResultDto> TryBuildRuntimePreviewAsync(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        string? skeletonJson,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        byte[]? skeletonBinaryContent,
        CancellationToken cancellationToken)
    {
        var runtime = SpineAnimationRuntimeResolver.Resolve(null);
        string? spineVersion = null;
        if (!string.IsNullOrWhiteSpace(skeletonJson) &&
            (!TryResolveRuntime(skeletonJson, out spineVersion, out runtime) ||
             runtime.SupportKind != SpineAnimationRuntimeSupportKind.OfficialAdapterAvailable))
        {
            return SpineAnimationRuntimePreviewResultDto.NotHandled;
        }

        var result = await _runtimePreviewService.TryBuildPreviewAsync(
            new SpineAnimationRuntimePreviewRequestDto(
                groupKey,
                skeletonAsset,
                skeletonJson,
                runtime,
                atlasPages,
                atlasTexts,
                skeletonBinaryContent),
            cancellationToken);
        if (result.IsCancelled ||
            !result.IsHandled ||
            result.Preview is null)
        {
            return result;
        }

        return result with
        {
            Preview = result.Preview with
            {
                SpineVersion = result.Preview.SpineVersion ?? spineVersion,
                Runtime = result.Preview.Runtime ?? runtime
            }
        };
    }

    private static bool TryResolveRuntime(
        string skeletonJson,
        out string? spineVersion,
        out SpineAnimationRuntimeDto runtime)
    {
        spineVersion = null;
        runtime = null!;
        try
        {
            using var document = JsonDocument.Parse(skeletonJson);
            spineVersion = ResolveSkeletonMetadataText(document.RootElement, "spine");
            runtime = SpineAnimationRuntimeResolver.Resolve(spineVersion);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private sealed class NoOpSpineAnimationRuntimePreviewService : ISpineAnimationRuntimePreviewService
    {
        public Task<SpineAnimationRuntimePreviewResultDto> TryBuildPreviewAsync(
            SpineAnimationRuntimePreviewRequestDto request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(cancellationToken.IsCancellationRequested
                ? SpineAnimationRuntimePreviewResultDto.Cancelled
                : SpineAnimationRuntimePreviewResultDto.NotHandled);
    }
}
