using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 通过预览管线读取 Spine skeleton TextAsset，并提取安全的轻量结构计数。
/// </summary>
internal sealed class SpineSkeletonSummaryBuilder
{
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewQueryHandler;

    public SpineSkeletonSummaryBuilder(
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler)
    {
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
    }

    public async Task<SkeletonSummaryResult> BuildAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> skeletonAssets,
        CancellationToken cancellationToken)
    {
        var summaries = new List<SpineSkeletonSummaryDto>();
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>();
        foreach (var skeletonAsset in skeletonAssets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SkeletonSummaryResult.Cancelled;
            }

            if (IsBinarySkeleton(skeletonAsset))
            {
                summaries.Add(CreateBinarySummary(skeletonAsset));
                continue;
            }

            var previewResult = await _assetPreviewQueryHandler.HandleAsync(
                new GetAssetPreviewQuery(
                    query.WorkspaceId,
                    query.WorkspaceKey,
                    query.WorkspaceDataRootPath,
                    skeletonAsset.BundleId,
                    skeletonAsset.AssetId),
                cancellationToken);
            if (previewResult.IsCancelled)
            {
                return SkeletonSummaryResult.Cancelled;
            }

            if (!TryReadSkeletonJson(previewResult, out var skeletonJson))
            {
                diagnostics.Add(CreateDiagnostic(
                    SpineAssetGroupDiagnosticKind.SkeletonPreviewUnavailable,
                    groupKey,
                    skeletonAsset));
                continue;
            }

            if (TryCreateJsonSummary(skeletonAsset, skeletonJson, out var summary))
            {
                summaries.Add(summary);
                continue;
            }

            diagnostics.Add(CreateDiagnostic(
                SpineAssetGroupDiagnosticKind.SkeletonJsonParseFailed,
                groupKey,
                skeletonAsset));
        }

        return new SkeletonSummaryResult(false, summaries, diagnostics);
    }

    private static bool TryReadSkeletonJson(
        Result<AssetPreviewDto> previewResult,
        out string skeletonJson)
    {
        skeletonJson = string.Empty;
        if (!previewResult.IsSuccess ||
            previewResult.Value is not { Kind: AssetPreviewKind.Text } preview ||
            string.IsNullOrWhiteSpace(preview.TextContent))
        {
            return false;
        }

        skeletonJson = preview.TextContent;
        return true;
    }

    private static bool TryCreateJsonSummary(
        SpineAssetGroupPartDto skeletonAsset,
        string skeletonJson,
        out SpineSkeletonSummaryDto summary)
    {
        summary = null!;
        try
        {
            using var document = JsonDocument.Parse(
                skeletonJson,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var root = document.RootElement;
            summary = new SpineSkeletonSummaryDto(
                skeletonAsset,
                SpineSkeletonAssetFormat.Json,
                ResolveSkeletonMetadataText(root, "spine"),
                CountCollection(root, "bones"),
                CountCollection(root, "slots"),
                CountCollection(root, "skins"),
                CountCollection(root, "animations"),
                CountCollection(root, "events"),
                CountCollection(root, "ik"),
                CountCollection(root, "transform"),
                CountCollection(root, "path"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ResolveSkeletonMetadataText(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty("skeleton", out var skeletonMetadata) ||
            skeletonMetadata.ValueKind != JsonValueKind.Object ||
            !skeletonMetadata.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int CountCollection(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var collection))
        {
            return 0;
        }

        return collection.ValueKind switch
        {
            JsonValueKind.Array => collection.GetArrayLength(),
            JsonValueKind.Object => CountObjectProperties(collection),
            _ => 0
        };
    }

    private static int CountObjectProperties(JsonElement value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateObject())
        {
            count++;
        }

        return count;
    }

    private static SpineSkeletonSummaryDto CreateBinarySummary(
        SpineAssetGroupPartDto skeletonAsset) =>
        new(
            skeletonAsset,
            SpineSkeletonAssetFormat.Binary,
            SpineVersion: null,
            BoneCount: null,
            SlotCount: null,
            SkinCount: null,
            AnimationCount: null,
            EventCount: null,
            IkConstraintCount: null,
            TransformConstraintCount: null,
            PathConstraintCount: null);

    private static SpineAssetGroupDiagnosticDto CreateDiagnostic(
        SpineAssetGroupDiagnosticKind kind,
        string groupKey,
        SpineAssetGroupPartDto skeletonAsset) =>
        new(kind, groupKey, [skeletonAsset]);

    private static bool IsBinarySkeleton(SpineAssetGroupPartDto skeletonAsset)
    {
        var path = ResolvePartPath(skeletonAsset);
        return path.EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePartPath(SpineAssetGroupPartDto skeletonAsset)
    {
        if (!string.IsNullOrWhiteSpace(skeletonAsset.AssetPath))
        {
            return skeletonAsset.AssetPath.Trim().Replace('\\', '/');
        }

        return string.IsNullOrWhiteSpace(skeletonAsset.Name)
            ? skeletonAsset.AssetId.Value
            : skeletonAsset.Name.Trim().Replace('\\', '/');
    }

    public sealed record SkeletonSummaryResult(
        bool IsCancelled,
        IReadOnlyList<SpineSkeletonSummaryDto> Summaries,
        IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
    {
        public static SkeletonSummaryResult Cancelled { get; } =
            new(true, Array.Empty<SpineSkeletonSummaryDto>(), Array.Empty<SpineAssetGroupDiagnosticDto>());
    }
}
