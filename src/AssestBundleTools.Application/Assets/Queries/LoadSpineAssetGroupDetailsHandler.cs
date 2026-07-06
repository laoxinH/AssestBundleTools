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
    private readonly AssetPreviewStructuredTextLoader _structuredTextLoader;
    private readonly SpineSkeletonSummaryBuilder _skeletonSummaryBuilder;
    private readonly SpineAnimationPreviewBuilder _animationPreviewBuilder;

    public LoadSpineAssetGroupDetailsHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler,
        ISpineAnimationRuntimePreviewService? spineAnimationRuntimePreviewService)
        : this(
            workspaceSessionRegistry,
            assetPreviewQueryHandler,
            loadPreviewTextChunkHandler: null,
            spineAnimationRuntimePreviewService)
    {
    }

    public LoadSpineAssetGroupDetailsHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>> assetPreviewQueryHandler,
        IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>>? loadPreviewTextChunkHandler = null,
        ISpineAnimationRuntimePreviewService? spineAnimationRuntimePreviewService = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _assetPreviewQueryHandler = assetPreviewQueryHandler ?? throw new ArgumentNullException(nameof(assetPreviewQueryHandler));
        _textureDiagnosticBuilder = new SpineAssetGroupTextureDiagnosticBuilder(_assetPreviewQueryHandler);
        _structuredTextLoader = new AssetPreviewStructuredTextLoader(
            loadPreviewTextChunkHandler ?? new PreviewResultInlineTextChunkHandler());
        _skeletonSummaryBuilder = new SpineSkeletonSummaryBuilder(
            _assetPreviewQueryHandler,
            _structuredTextLoader);
        _animationPreviewBuilder = new SpineAnimationPreviewBuilder(
            _assetPreviewQueryHandler,
            _structuredTextLoader,
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

        group = SpineAssetGroupPartDetailsHydrator.HydrateGroup(
            _workspaceSessionRegistry,
            query.WorkspaceId,
            group,
            cancellationToken);

        var atlasPages = new List<SpineAtlasPageDto>();
        var atlasTexts = new List<string>();
        var atlasTextSources = new List<AssetPreviewTextSourceDto>();
        var diagnostics = new List<SpineAssetGroupDiagnosticDto>(group.Diagnostics);
        var skeletonSummaryResult = await BuildSkeletonSummariesAsync(
            query,
            group,
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
                SpineAssetGroupPreviewQueryFactory.Create(query, atlasAsset),
                cancellationToken);
            if (atlasPreviewResult.IsCancelled)
            {
                return Result<SpineAssetGroupDetailsDto>.Cancelled();
            }

            if (!TryCreateAtlasTextSource(query, atlasPreviewResult, out var atlasTextSource))
            {
                diagnostics.Add(CreateDiagnostic(SpineAssetGroupDiagnosticKind.AtlasPreviewUnavailable, group.GroupKey, atlasAsset));
                continue;
            }

            var parsedPages = SpineAtlasTexturePageMatcher.AttachTexturePageAssets(
                _structuredTextLoader.ParseAtlas(atlasTextSource, cancellationToken),
                group.TexturePageAssets);
            if (parsedPages.Count == 0)
            {
                diagnostics.Add(CreateDiagnostic(SpineAssetGroupDiagnosticKind.AtlasParseFailed, group.GroupKey, atlasAsset));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(atlasTextSource.InlineText))
            {
                atlasTexts.Add(atlasTextSource.InlineText);
            }

            atlasTextSources.Add(atlasTextSource);
            if (ShouldBuildTextureDiagnostics(query))
            {
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
            }

            atlasPages.AddRange(parsedPages);
        }

        var animationPreviewResult = await BuildAnimationPreviewAsync(
            query,
            group.GroupKey,
            group.SkeletonAssets,
            atlasPages,
            atlasTexts,
            atlasTextSources,
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

    private async Task<SpineAnimationPreviewBuildResult> BuildAnimationPreviewAsync(
        LoadSpineAssetGroupDetailsQuery query,
        string groupKey,
        IReadOnlyList<SpineAssetGroupPartDto> skeletonAssets,
        IReadOnlyList<SpineAtlasPageDto> atlasPages,
        IReadOnlyList<string> atlasTexts,
        IReadOnlyList<AssetPreviewTextSourceDto> atlasTextSources,
        CancellationToken cancellationToken)
    {
        if (query.LoadMode == SpineAssetGroupDetailsLoadMode.ExtractionPlan)
        {
            return SpineAnimationPreviewBuildResult.Empty;
        }

        return await _animationPreviewBuilder.BuildAsync(
            query,
            groupKey,
            skeletonAssets,
            atlasPages,
            atlasTexts,
            atlasTextSources,
            cancellationToken);
    }

    private async Task<SpineSkeletonSummaryBuilder.SkeletonSummaryResult> BuildSkeletonSummariesAsync(
        LoadSpineAssetGroupDetailsQuery query,
        SpineAssetGroupDto group,
        CancellationToken cancellationToken)
    {
        if (!ShouldBuildSkeletonSummaries(query))
        {
            return SpineSkeletonSummaryBuilder.SkeletonSummaryResult.Empty;
        }

        return await _skeletonSummaryBuilder.BuildAsync(
            query,
            group.GroupKey,
            group.SkeletonAssets,
            cancellationToken);
    }

    private static bool ShouldBuildSkeletonSummaries(LoadSpineAssetGroupDetailsQuery query) =>
        query.LoadMode is not SpineAssetGroupDetailsLoadMode.InteractivePreview
            and not SpineAssetGroupDetailsLoadMode.ExtractionPlan;

    private static bool ShouldBuildTextureDiagnostics(LoadSpineAssetGroupDetailsQuery query) =>
        query.LoadMode is not SpineAssetGroupDetailsLoadMode.InteractivePreview
            and not SpineAssetGroupDetailsLoadMode.ExtractionPlan;

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

    private bool TryCreateAtlasTextSource(
        LoadSpineAssetGroupDetailsQuery query,
        Result<AssetPreviewDto> atlasPreviewResult,
        out AssetPreviewTextSourceDto atlasTextSource) =>
        _structuredTextLoader.TryCreateSource(
            atlasPreviewResult,
            query.WorkspaceDataRootPath,
            out atlasTextSource);

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

    private sealed class PreviewResultInlineTextChunkHandler :
        IQueryHandler<LoadPreviewTextChunkQuery, Result<AssetPreviewTextChunkDto>>
    {
        public Task<Result<AssetPreviewTextChunkDto>> HandleAsync(
            LoadPreviewTextChunkQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result<AssetPreviewTextChunkDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentReferenceInvalid()));
    }
}
