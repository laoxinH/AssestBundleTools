using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

internal sealed partial class SpineAnimationPreviewBuilder
{
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewQueryHandler;
    private readonly AssetPreviewStructuredTextLoader _structuredTextLoader;

    public SpineAnimationPreviewBuilder(
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler,
        AssetPreviewStructuredTextLoader structuredTextLoader,
        ISpineAnimationRuntimePreviewService? runtimePreviewService = null)
    {
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
        _structuredTextLoader = structuredTextLoader ?? throw new ArgumentNullException(nameof(structuredTextLoader));
        _runtimePreviewService = runtimePreviewService ?? NoOpRuntimePreviewService;
    }

    public async Task<SpineAnimationPreviewBuildResult> BuildAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> skeletonAssets,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto> atlasTextSources,
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
                atlasTextSources,
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
            atlasTextSources,
            cancellationToken);
    }

    private async Task<SpineAnimationPreviewBuildResult> BuildJsonSkeletonPreviewAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        SpineAssetGroupPartDto skeletonAsset,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto> atlasTextSources,
        CancellationToken cancellationToken)
    {
        var previewResult = await _assetPreviewQueryHandler.HandleAsync(
            SpineAssetGroupPreviewQueryFactory.Create(query, skeletonAsset),
            cancellationToken);
        if (previewResult.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (!TryCreateSkeletonTextSource(query, previewResult, out var skeletonJsonSource))
        {
            return SpineAnimationPreviewBuildResult.Empty;
        }

        return await BuildJsonSkeletonPreviewFromTextAsync(
            skeletonAsset,
            groupKey,
            skeletonJsonSource,
            atlasPages,
            atlasTexts,
            atlasTextSources,
            query.LoadMode,
            cancellationToken);
    }

    private async Task<SpineAnimationPreviewBuildResult> BuildJsonSkeletonPreviewFromTextAsync(
        SpineAssetGroupPartDto skeletonAsset,
        string groupKey,
        AssetPreviewTextSourceDto skeletonJsonSource,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto> atlasTextSources,
        SpineAssetGroupDetailsLoadMode loadMode,
        CancellationToken cancellationToken)
    {
        var runtimePreviewResult = await TryBuildRuntimePreviewAsync(
            skeletonAsset,
            groupKey,
            skeletonJsonSource.InlineText,
            skeletonJsonSource,
            atlasPages,
            atlasTexts,
            atlasTextSources,
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

        if (loadMode == SpineAssetGroupDetailsLoadMode.InteractivePreview &&
            runtimePreviewResult.Diagnostics.Count > 0)
        {
            return SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(runtimePreviewResult.Diagnostics);
        }

        var skeletonJson = ReadSkeletonJsonForApplicationFallback(skeletonJsonSource, cancellationToken);
        if (skeletonJson.IsCancelled)
        {
            return SpineAnimationPreviewBuildResult.Cancelled;
        }

        if (!string.IsNullOrWhiteSpace(skeletonJson.Text) &&
            TryBuildPreview(
            skeletonAsset,
            groupKey,
            skeletonJson.Text,
            skeletonJsonSource,
            atlasPages,
            atlasTexts,
            atlasTextSources,
            out var preview,
            out var previewDiagnostics))
        {
            return new SpineAnimationPreviewBuildResult(false, preview, previewDiagnostics);
        }

        return SpineAnimationPreviewBuildResult.Empty;
    }

    private async Task<SpineAnimationPreviewBuildResult> BuildBinarySkeletonPreviewAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        SpineAssetGroupPartDto skeletonAsset,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto> atlasTextSources,
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
                skeletonJsonSource: null,
                atlasPages,
                atlasTexts,
                atlasTextSources,
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
        else if (TryCreateSkeletonTextSource(query, previewResult, out var skeletonJsonSource))
        {
            var skeletonJson = ReadSkeletonJsonForApplicationFallback(skeletonJsonSource, cancellationToken);
            if (skeletonJson.IsCancelled)
            {
                return SpineAnimationPreviewBuildResult.Cancelled;
            }

            if (string.IsNullOrWhiteSpace(skeletonJson.Text) ||
                !LooksLikeJsonObject(skeletonJson.Text))
            {
                return SpineAnimationPreviewBuildResult.EmptyWithDiagnostics(
                [
                    CreateUnsupportedFeatureDiagnostic(
                        groupKey,
                        skeletonAsset,
                        SpineAnimationUnsupportedFeatureKind.BinarySkeleton)
                ]);
            }

            var jsonPreviewResult = await BuildJsonSkeletonPreviewFromTextAsync(
                skeletonAsset,
                groupKey,
                skeletonJsonSource,
                atlasPages,
                atlasTexts,
                atlasTextSources,
                query.LoadMode,
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
        AssetPreviewTextSourceDto skeletonJsonSource,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto> atlasTextSources,
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
                        activeSkinPreview.Animations),
                    SkeletonJsonSource: skeletonJsonSource,
                    AtlasTextSources: atlasTextSources)
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

    private bool TryCreateSkeletonTextSource(
        LoadSpineAssetGroupDetailsQuery query,
        Result<AssetPreviewDto> previewResult,
        out AssetPreviewTextSourceDto skeletonJsonSource) =>
        _structuredTextLoader.TryCreateSource(
            previewResult,
            query.WorkspaceDataRootPath,
            out skeletonJsonSource);

    private SkeletonJsonReadResult ReadSkeletonJsonForApplicationFallback(
        AssetPreviewTextSourceDto skeletonJsonSource,
        CancellationToken cancellationToken)
    {
        var readResult = _structuredTextLoader.ReadText(
            skeletonJsonSource,
            AssetPreviewStructuredTextLoader.MaxStructuredJsonCharacters,
            cancellationToken);
        return readResult.IsCancelled
            ? SkeletonJsonReadResult.Cancelled
            : new SkeletonJsonReadResult(false, readResult.Text ?? string.Empty);
    }

    private sealed record SkeletonJsonReadResult(bool IsCancelled, string Text)
    {
        public static SkeletonJsonReadResult Cancelled { get; } =
            new(true, string.Empty);
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
