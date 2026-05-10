using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 处理 Spine 资源组 atlas 结构化详情加载请求。
/// </summary>
public sealed class LoadSpineAssetGroupDetailsHandler : IQueryHandler<LoadSpineAssetGroupDetailsQuery, Result<SpineAssetGroupDetailsDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> _assetPreviewQueryHandler;
    private readonly SpineAssetGroupTextureDiagnosticBuilder _textureDiagnosticBuilder;
    private readonly SpineSkeletonSummaryBuilder _skeletonSummaryBuilder;
    private readonly SpineAnimationPreviewBuilder _animationPreviewBuilder;

    public LoadSpineAssetGroupDetailsHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler,
        ISpineAnimationRuntimePreviewService? spineAnimationRuntimePreviewService = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
        _textureDiagnosticBuilder = new SpineAssetGroupTextureDiagnosticBuilder(_assetPreviewQueryHandler);
        _skeletonSummaryBuilder = new SpineSkeletonSummaryBuilder(_assetPreviewQueryHandler);
        _animationPreviewBuilder = new SpineAnimationPreviewBuilder(
            _assetPreviewQueryHandler,
            spineAnimationRuntimePreviewService);
    }

    public async Task<Result<SpineAssetGroupDetailsDto>> HandleAsync(
        LoadSpineAssetGroupDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<SpineAssetGroupDetailsDto>.Cancelled();
        }

        var validationResult = ValidateQuery(query);
        if (validationResult is not null)
        {
            return validationResult;
        }

        if (!_workspaceSessionRegistry.TryGetBundleTree(query.WorkspaceId, out var bundleTree))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId));
        }

        var assetLists = LoadWorkspaceAssetLists(query, bundleTree, cancellationToken);
        if (assetLists.IsCancelled)
        {
            return Result<SpineAssetGroupDetailsDto>.Cancelled();
        }

        var assetDetailsByAssetKey = SpineAssetGroupDetectionDetailsCollector.Collect(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            assetLists.Value,
            cancellationToken);
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<SpineAssetGroupDetailsDto>.Cancelled();
        }

        var groupList = SpineAssetGroupDetector.CreateResult(
            query.WorkspaceId,
            query.BundleId,
            targetBundleDisplayName: query.BundleId.Value,
            searchKeyword: null,
            assetLists.Value,
            assetDetailsByAssetKey);
        var group = groupList.Items.FirstOrDefault(item =>
            string.Equals(item.GroupId, query.GroupId.Trim(), StringComparison.Ordinal));
        if (group is null)
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.SpineAssetGroupNotFound(query.GroupId.Trim()));
        }

        var atlasPages = new List<SpineAtlasPageDto>();
        var atlasTexts = new List<string>();
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>(group.Diagnostics);
        var skeletonSummaryResult = await _skeletonSummaryBuilder.BuildAsync(
            query,
            group.GroupKey,
            group.SkeletonAssets,
            cancellationToken);
        if (skeletonSummaryResult.IsCancelled)
        {
            return Result<SpineAssetGroupDetailsDto>.Cancelled();
        }

        diagnostics.AddRange(skeletonSummaryResult.Diagnostics);
        foreach (var atlasAsset in group.AtlasAssets)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result<SpineAssetGroupDetailsDto>.Cancelled();
            }

            var atlasPreviewResult = await _assetPreviewQueryHandler.HandleAsync(
                new GetAssetPreviewQuery(
                    query.WorkspaceId,
                    query.WorkspaceKey,
                    query.WorkspaceDataRootPath,
                    atlasAsset.BundleId,
                    atlasAsset.AssetId),
                cancellationToken);
            if (atlasPreviewResult.IsCancelled)
            {
                return Result<SpineAssetGroupDetailsDto>.Cancelled();
            }

            if (!TryReadAtlasText(atlasPreviewResult, out var atlasText))
            {
                diagnostics.Add(CreateDiagnostic(SpineAssetGroupDiagnosticKind.AtlasPreviewUnavailable, group.GroupKey, atlasAsset));
                continue;
            }

            var parsedPages = SpineAtlasTexturePageMatcher.AttachTexturePageAssets(
                SpineAtlasTextParser.Parse(atlasText),
                group.TexturePageAssets);
            if (parsedPages.Count == 0)
            {
                diagnostics.Add(CreateDiagnostic(SpineAssetGroupDiagnosticKind.AtlasParseFailed, group.GroupKey, atlasAsset));
                continue;
            }

            atlasTexts.Add(atlasText);
            var textureDiagnostics = await _textureDiagnosticBuilder.BuildAsync(
                query,
                group.GroupKey,
                atlasAsset,
                parsedPages,
                cancellationToken);
            if (textureDiagnostics.IsCancelled)
            {
                return Result<SpineAssetGroupDetailsDto>.Cancelled();
            }

            diagnostics.AddRange(textureDiagnostics.Diagnostics);
            atlasPages.AddRange(parsedPages);
        }

        var animationPreviewResult = await _animationPreviewBuilder.BuildAsync(
            query,
            group.GroupKey,
            group.SkeletonAssets,
            atlasPages,
            atlasTexts,
            cancellationToken);
        if (animationPreviewResult.IsCancelled)
        {
            return Result<SpineAssetGroupDetailsDto>.Cancelled();
        }

        diagnostics.AddRange(animationPreviewResult.Diagnostics);
        return Result<SpineAssetGroupDetailsDto>.Success(new SpineAssetGroupDetailsDto(
            group,
            skeletonSummaryResult.Summaries,
            atlasPages,
            diagnostics,
            animationPreviewResult.Preview));
    }

    private Result<SpineAssetGroupDetailsDto>? ValidateQuery(LoadSpineAssetGroupDetailsQuery query)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceKey))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.WorkspaceCatalogKeyRequired());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.WorkspaceOpenDataRootPathRequired());
        }

        if (string.IsNullOrWhiteSpace(query.BundleId.Value))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.BundleIdRequired());
        }

        if (string.IsNullOrWhiteSpace(query.GroupId))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.SpineAssetGroupIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.BundleId))
        {
            return Result<SpineAssetGroupDetailsDto>.Failure(ApplicationErrors.BundleNotFound(query.BundleId));
        }

        return null;
    }

    private AssetListCollectionResult LoadWorkspaceAssetLists(
        LoadSpineAssetGroupDetailsQuery query,
        IReadOnlyList<BundleTreeNodeDto> bundleTree,
        CancellationToken cancellationToken)
    {
        var bundleNodes = FlattenBundleNodes(bundleTree)
            .Where(static node => node.CanOpenAssets)
            .ToArray();
        var assetLists = new List<AssetListResultDto>(bundleNodes.Length);
        foreach (var bundleNode in bundleNodes)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return AssetListCollectionResult.Cancelled;
            }

            if (_workspaceSessionRegistry.TryGetAssetList(
                    query.WorkspaceId,
                    bundleNode.BundleId,
                    searchKeyword: null,
                    assetTypeFilter: null,
                    assetTypeDisplayNameFilter: null,
                    out var assetList) &&
                assetList is not null)
            {
                assetLists.Add(assetList);
            }
        }

        return new AssetListCollectionResult(false, assetLists);
    }

    private static bool TryReadAtlasText(
        Result<AssetPreviewDto> atlasPreviewResult,
        out string atlasText) =>
        AssetPreviewStructuredTextReader.TryReadText(atlasPreviewResult, out atlasText);

    private static SpineAssetGroupDiagnosticDto CreateDiagnostic(
        SpineAssetGroupDiagnosticKind kind,
        string groupKey,
        SpineAssetGroupPartDto atlasAsset) =>
        new(kind, groupKey, [atlasAsset]);

    private static IEnumerable<BundleTreeNodeDto> FlattenBundleNodes(IEnumerable<BundleTreeNodeDto> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in FlattenBundleNodes(node.Children))
            {
                yield return child;
            }
        }
    }

    private sealed record AssetListCollectionResult(
        bool IsCancelled,
        IReadOnlyList<AssetListResultDto> Value)
    {
        public static AssetListCollectionResult Cancelled { get; } =
            new(true, Array.Empty<AssetListResultDto>());
    }
}
