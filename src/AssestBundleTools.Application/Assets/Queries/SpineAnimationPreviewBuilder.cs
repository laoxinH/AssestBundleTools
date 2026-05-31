using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewQueryHandler;

    public SpineAnimationPreviewBuilder(
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler,
        ISpineAnimationRuntimePreviewService? runtimePreviewService = null)
    {
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
        _runtimePreviewService = runtimePreviewService ?? NoOpRuntimePreviewService;
    }

    public async Task<SpineAnimationPreviewBuildResult> BuildAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> skeletonAssets,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>();
        if (atlasPages.Count == 0)
        {
            return SpineAnimationPreviewBuildResult.Empty;
        }

        var jsonSkeletonAsset = skeletonAssets.FirstOrDefault(static asset => !IsBinarySkeleton(asset));
        if (jsonSkeletonAsset is not null)
        {
            return await BuildJsonSkeletonPreviewAsync(
                query,
                groupKey,
                jsonSkeletonAsset,
                atlasPages,
                atlasTexts,
                cancellationToken);
        }

        var binarySkeletonAsset = skeletonAssets.FirstOrDefault(static asset => IsBinarySkeleton(asset));
        if (binarySkeletonAsset is null)
        {
            return SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(diagnostics);
        }

        return await BuildBinarySkeletonPreviewAsync(
            query,
            groupKey,
            binarySkeletonAsset,
            atlasPages,
            atlasTexts,
            cancellationToken);
    }

    private async Task<SpineAnimationPreviewBuildResult> BuildJsonSkeletonPreviewAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        SpineAssetGroupPartDto skeletonAsset,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        CancellationToken cancellationToken)
    {
        var previewResult = await _assetPreviewQueryHandler.HandleAsync(
            SpineAssetGroupPreviewQueryFactory.Create(query, skeletonAsset),
            cancellationToken);
        if (previewResult.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (!TryReadSkeletonJson(previewResult, out var skeletonJson))
        {
            return SpineAnimationPreviewBuildResult.Empty;
        }

        return await BuildJsonSkeletonPreviewFromTextAsync(
            skeletonAsset,
            groupKey,
            skeletonJson,
            atlasPages,
            atlasTexts,
            cancellationToken);
    }

    private async Task<SpineAnimationPreviewBuildResult> BuildJsonSkeletonPreviewFromTextAsync(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        string skeletonJson,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        CancellationToken cancellationToken)
    {
        var runtimePreviewResult = await TryBuildRuntimePreviewAsync(
            skeletonAsset,
            groupKey,
            skeletonJson,
            atlasPages,
            atlasTexts,
            skeletonBinaryContent: null,
            cancellationToken);
        if (runtimePreviewResult.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (runtimePreviewResult.IsHandled)
        {
            return new SpineAnimationPreviewBuildResult(
                false,
                runtimePreviewResult.Preview,
                runtimePreviewResult.Diagnostics);
        }

        return TryBuildPreview(
            skeletonAsset,
            groupKey,
            skeletonJson,
            atlasPages,
            atlasTexts,
            out var preview,
            out var previewDiagnostics)
            ? new SpineAnimationPreviewBuildResult(false, preview, previewDiagnostics)
            : SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(previewDiagnostics);
    }

    private async Task<SpineAnimationPreviewBuildResult> BuildBinarySkeletonPreviewAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        SpineAssetGroupPartDto skeletonAsset,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        CancellationToken cancellationToken)
    {
        var previewResult = await _assetPreviewQueryHandler.HandleAsync(
            SpineAssetGroupPreviewQueryFactory.Create(query, skeletonAsset),
            cancellationToken);
        if (previewResult.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (TryReadSkeletonBinary(previewResult, out var skeletonBinaryContent))
        {
            var runtimePreviewResult = await TryBuildRuntimePreviewAsync(
                skeletonAsset,
                groupKey,
                skeletonJson: null,
                atlasPages,
                atlasTexts,
                skeletonBinaryContent,
                cancellationToken);
            if (runtimePreviewResult.IsCancelled)
            {
                return SpineAnimationPreviewBuildResult.Cancelled;
            }

            if (runtimePreviewResult.IsHandled)
            {
                return new SpineAnimationPreviewBuildResult(
                    false,
                    runtimePreviewResult.Preview,
                    runtimePreviewResult.Diagnostics);
            }
        }
        else if (TryReadSkeletonJson(previewResult, out var skeletonJson) &&
                 LooksLikeJsonObject(skeletonJson))
        {
            var jsonPreviewResult = await BuildJsonSkeletonPreviewFromTextAsync(
                skeletonAsset,
                groupKey,
                skeletonJson,
                atlasPages,
                atlasTexts,
                cancellationToken);
            if (jsonPreviewResult.IsCancelled ||
                jsonPreviewResult.Preview is not null ||
                jsonPreviewResult.Diagnostics.Count > 0)
            {
                return jsonPreviewResult;
            }
        }

        return SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(
        [
            CreateUnsupportedFeatureDiagnostic(
                groupKey,
                skeletonAsset,
                SpineAnimationUnsupportedFeatureKind.BinarySkeleton)
        ]);
    }

    private static bool TryBuildPreview(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        string skeletonJson,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        out SpineAnimationPreviewDto? preview,
        out IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics)
    {
        preview = null;
        diagnostics = Array.Empty<SpineAssetGroupDiagnosticDto>();
        try
        {
            using var document = JsonDocument.Parse(skeletonJson);
            var root = document.RootElement;
            var spineVersion = ResolveSkeletonMetadataText(root, "spine");
            var runtime = SpineAnimationRuntimeResolver.Resolve(spineVersion);
            var unsupportedFeatureDiagnostics = BuildUnsupportedFeatureDiagnostics(skeletonAsset, groupKey, root);
            var runtimeDiagnostics = BuildRuntimeDiagnostics(skeletonAsset, groupKey, runtime);
            var atlasRegions = CreateAtlasRegionMap(atlasPages);
            var bones = ParseBones(root);
            var slots = ParseSlots(root);
            var skinAttachmentSets = ParseSkinAttachmentSets(root, atlasRegions);
            var animations = ParseAnimations(root);
            if (bones.Count == 0 || slots.Count == 0 || skinAttachmentSets.Count == 0 || animations.Count == 0)
            {
                return false;
            }

            diagnostics = unsupportedFeatureDiagnostics
                .Concat(runtimeDiagnostics)
                .ToArray();
            var skinPreviews = BuildSampledSkinPreviews(bones, slots, skinAttachmentSets, animations);
            var activeSkinPreview = ResolveInitialSkinPreview(skinPreviews);
            preview = new SpineAnimationPreviewDto(
                skeletonAsset,
                spineVersion,
                bones,
                slots,
                activeSkinPreview.Attachments,
                activeSkinPreview.Animations,
                runtime)
            {
                SkinNames = ParseSkinNames(root),
                SetupFrame = activeSkinPreview.SetupFrame,
                SkinPreviews = skinPreviews,
                PlaybackSource = new SpineAnimationRuntimePlaybackSourceDto(
                    groupKey,
                    skeletonAsset,
                    skeletonJson,
                    runtime,
                    atlasPages,
                    atlasTexts,
                    SampledFallback: new SpineAnimationSampledPlaybackSourceDto(
                        skinPreviews,
                        activeSkinPreview.SetupFrame,
                        activeSkinPreview.Animations))
            };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeJsonObject(string text)
    {
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                continue;
            }

            return character == '{';
        }

        return false;
    }

}

internal sealed record SpineAnimationPreviewBuildResult(
    bool IsCancelled,
    SpineAnimationPreviewDto? Preview,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationPreviewBuildResult Empty { get; } =
        new(false, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationPreviewBuildResult Cancelled { get; } =
        new(true, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationPreviewBuildResult EmptyWithDiagnostics(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics) =>
        new(false, null, diagnostics);
}
