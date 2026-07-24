using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 展开普通资产拖入来源，并按精确落点或批量名称、类型及 skeleton 真实格式生成确定性匹配计划。
/// </summary>
public sealed class MatchAssetReplacementSourcesHandler :
    IQueryHandler<MatchAssetReplacementSourcesQuery, Result<AssetReplacementSourceMatchPlanDto>>
{
    private readonly IReplacementSourcePathExpander _sourcePathExpander;
    private readonly SpineSkeletonReplacementCompatibilityService _skeletonCompatibilityService;
    private readonly IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? _loadSpineAssetGroupsHandler;

    public MatchAssetReplacementSourcesHandler(
        IReplacementSourcePathExpander sourcePathExpander,
        SpineSkeletonReplacementCompatibilityService skeletonCompatibilityService,
        IQueryHandler<LoadSpineAssetGroupsQuery, Result<SpineAssetGroupListResultDto>>? loadSpineAssetGroupsHandler = null)
    {
        _sourcePathExpander = sourcePathExpander ?? throw new ArgumentNullException(nameof(sourcePathExpander));
        _skeletonCompatibilityService = skeletonCompatibilityService ?? throw new ArgumentNullException(nameof(skeletonCompatibilityService));
        _loadSpineAssetGroupsHandler = loadSpineAssetGroupsHandler;
    }

    public async Task<Result<AssetReplacementSourceMatchPlanDto>> HandleAsync(
        MatchAssetReplacementSourcesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sourcePathsResult = await _sourcePathExpander.ExpandAsync(query.SourcePaths, cancellationToken);
        if (sourcePathsResult.IsCancelled)
        {
            return Result<AssetReplacementSourceMatchPlanDto>.Cancelled();
        }

        if (!sourcePathsResult.IsSuccess || sourcePathsResult.Value is null)
        {
            return Result<AssetReplacementSourceMatchPlanDto>.Failure(sourcePathsResult.Errors);
        }

        var candidates = sourcePathsResult.Value
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(static path => new SourceCandidate(
                path,
                EnumerateMatchKeys(Path.GetFileName(path)).ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        var spineGroupsResult = await ResolveSpineGroupsAsync(query, cancellationToken);
        if (spineGroupsResult.IsCancelled)
        {
            return Result<AssetReplacementSourceMatchPlanDto>.Cancelled();
        }

        if (!spineGroupsResult.IsSuccess)
        {
            return Result<AssetReplacementSourceMatchPlanDto>.Failure(spineGroupsResult.Errors);
        }

        var spineGroups = spineGroupsResult.Value ?? Array.Empty<SpineAssetGroupDto>();
        var skeletonTargetIds = spineGroups
            .SelectMany(static group => group.SkeletonAssets)
            .Select(static part => CreateAssetKey(part.BundleId.Value, part.AssetId.Value))
            .ToHashSet(StringComparer.Ordinal);
        var atlasTargetIds = spineGroups
            .SelectMany(static group => group.AtlasAssets)
            .Select(static part => CreateAssetKey(part.BundleId.Value, part.AssetId.Value))
            .ToHashSet(StringComparer.Ordinal);

        var directTarget = ResolveDirectTarget(query, candidates);
        if (directTarget is not null)
        {
            return await MatchDirectTargetAsync(
                query,
                directTarget,
                candidates[0].FilePath,
                skeletonTargetIds,
                atlasTargetIds,
                cancellationToken);
        }

        var matches = new List<AssetReplacementSourceMatchDto>();
        var conflicts = new List<AssetReplacementSourceConflictDto>();

        foreach (var target in query.Targets)
        {
            var targetKeys = EnumerateTargetMatchKeys(target).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var isSkeleton = skeletonTargetIds.Contains(CreateAssetKey(target.BundleId.Value, target.AssetId.Value));
            var compatibleCandidates = new List<SourceCandidate>();
            foreach (var candidate in candidates)
            {
                if (!targetKeys.Overlaps(candidate.MatchKeys) ||
                    !HasCompatibleSemanticExtension(target, candidate.FilePath, isSkeleton) ||
                    !IsTypeCompatible(target.AssetType, candidate.FilePath, isSkeleton))
                {
                    continue;
                }

                if (isSkeleton)
                {
                    var compatibility = await _skeletonCompatibilityService.CheckAsync(
                        query.WorkspaceId,
                        query.WorkspaceKey,
                        query.WorkspaceDataRootPath,
                        target.BundleId,
                        target.AssetId,
                        candidate.FilePath,
                        cancellationToken);
                    if (compatibility.IsCancelled)
                    {
                        return Result<AssetReplacementSourceMatchPlanDto>.Cancelled();
                    }

                    if (!compatibility.IsSuccess || compatibility.Value?.IsCompatible != true)
                    {
                        continue;
                    }
                }

                compatibleCandidates.Add(candidate);
            }

            if (compatibleCandidates.Count > 1)
            {
                conflicts.Add(new AssetReplacementSourceConflictDto(
                    target,
                    compatibleCandidates.Select(static candidate => candidate.FilePath).ToArray()));
                continue;
            }

            if (compatibleCandidates.Count == 1)
            {
                var candidate = compatibleCandidates[0];
                matches.Add(new AssetReplacementSourceMatchDto(
                    target,
                    candidate.FilePath,
                    Path.GetFileName(candidate.FilePath),
                    isSkeleton));
            }
        }

        MoveSharedSourceMatchesToConflicts(matches, conflicts);

        return Result<AssetReplacementSourceMatchPlanDto>.Success(
            new AssetReplacementSourceMatchPlanDto(matches, conflicts));
    }

    private async Task<Result<AssetReplacementSourceMatchPlanDto>> MatchDirectTargetAsync(
        MatchAssetReplacementSourcesQuery query,
        AssetListItemDto target,
        string sourceFilePath,
        IReadOnlySet<string> skeletonTargetIds,
        IReadOnlySet<string> atlasTargetIds,
        CancellationToken cancellationToken)
    {
        var targetKey = CreateAssetKey(target.BundleId.Value, target.AssetId.Value);
        var isSkeleton = skeletonTargetIds.Contains(targetKey);
        var isAtlas = atlasTargetIds.Contains(targetKey);
        if (!IsDirectTypeCompatible(target.AssetType, sourceFilePath, isSkeleton, isAtlas))
        {
            return CreateEmptyPlan();
        }

        if (isSkeleton)
        {
            var compatibility = await _skeletonCompatibilityService.CheckAsync(
                query.WorkspaceId,
                query.WorkspaceKey,
                query.WorkspaceDataRootPath,
                target.BundleId,
                target.AssetId,
                sourceFilePath,
                cancellationToken);
            if (compatibility.IsCancelled)
            {
                return Result<AssetReplacementSourceMatchPlanDto>.Cancelled();
            }

            if (!compatibility.IsSuccess || compatibility.Value?.IsCompatible != true)
            {
                return CreateEmptyPlan();
            }
        }

        return Result<AssetReplacementSourceMatchPlanDto>.Success(
            new AssetReplacementSourceMatchPlanDto(
                [new AssetReplacementSourceMatchDto(
                    target,
                    sourceFilePath,
                    Path.GetFileName(sourceFilePath),
                    isSkeleton)],
                []));
    }

    private static AssetListItemDto? ResolveDirectTarget(
        MatchAssetReplacementSourcesQuery query,
        IReadOnlyList<SourceCandidate> candidates)
    {
        if (query.DirectDropTarget is null ||
            query.SourcePaths.Count != 1 ||
            candidates.Count != 1 ||
            !string.Equals(
                query.SourcePaths[0].Trim(),
                candidates[0].FilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var directTargetKey = CreateAssetKey(
            query.DirectDropTarget.BundleId.Value,
            query.DirectDropTarget.AssetId.Value);
        return query.Targets.FirstOrDefault(target =>
            string.Equals(
                CreateAssetKey(target.BundleId.Value, target.AssetId.Value),
                directTargetKey,
                StringComparison.Ordinal));
    }

    private static Result<AssetReplacementSourceMatchPlanDto> CreateEmptyPlan() =>
        Result<AssetReplacementSourceMatchPlanDto>.Success(
            new AssetReplacementSourceMatchPlanDto([], []));

    private static void MoveSharedSourceMatchesToConflicts(
        List<AssetReplacementSourceMatchDto> matches,
        ICollection<AssetReplacementSourceConflictDto> conflicts)
    {
        var sharedSources = matches
            .GroupBy(static match => match.SourceFilePath, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .ToArray();
        if (sharedSources.Length == 0)
        {
            return;
        }

        var sharedPaths = sharedSources
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var match in sharedSources.SelectMany(static group => group))
        {
            conflicts.Add(new AssetReplacementSourceConflictDto(match.Target, [match.SourceFilePath]));
        }

        matches.RemoveAll(match => sharedPaths.Contains(match.SourceFilePath));
    }

    private async Task<Result<IReadOnlyList<SpineAssetGroupDto>>> ResolveSpineGroupsAsync(
        MatchAssetReplacementSourcesQuery query,
        CancellationToken cancellationToken)
    {
        var groups = query.SpineGroups.ToList();
        if (_loadSpineAssetGroupsHandler is null)
        {
            return Result<IReadOnlyList<SpineAssetGroupDto>>.Success(groups);
        }

        foreach (var bundleId in query.Targets.Select(static target => target.BundleId).Distinct())
        {
            var result = await _loadSpineAssetGroupsHandler.HandleAsync(
                new LoadSpineAssetGroupsQuery(
                    query.WorkspaceId,
                    bundleId,
                    WorkspaceDataRootPath: query.WorkspaceDataRootPath),
                cancellationToken);
            if (result.IsCancelled)
            {
                return Result<IReadOnlyList<SpineAssetGroupDto>>.Cancelled();
            }

            if (!result.IsSuccess)
            {
                return Result<IReadOnlyList<SpineAssetGroupDto>>.Failure(result.Errors);
            }

            if (result.Value is not null)
            {
                groups.AddRange(result.Value.Items);
            }
        }

        return Result<IReadOnlyList<SpineAssetGroupDto>>.Success(
            groups
                .GroupBy(
                    static group => string.Concat(group.BundleId.Value, "\n", group.GroupId),
                    StringComparer.Ordinal)
                .Select(static group => group.First())
                .ToArray());
    }

    private static IEnumerable<string> EnumerateTargetMatchKeys(AssetListItemDto target) =>
        EnumerateMatchKeys(target.Name)
            .Concat(EnumerateMatchKeys(target.DisplayName))
            .Concat(EnumerateMatchKeys(target.ExportFileName))
            .Concat(EnumerateMatchKeys(Path.GetFileName(NormalizePathSeparators(target.AssetPath))))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateMatchKeys(string? value)
    {
        var current = NormalizeMatchKey(value);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (!string.IsNullOrEmpty(current) && seen.Add(current))
        {
            yield return current;
            var withoutExtension = NormalizeMatchKey(Path.GetFileNameWithoutExtension(current));
            if (string.Equals(current, withoutExtension, StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            current = withoutExtension;
        }
    }

    private static bool IsTypeCompatible(AssetType targetType, string sourceFilePath, bool isSkeleton) =>
        isSkeleton
            ? targetType == AssetType.TextAsset && IsSkeletonSource(sourceFilePath)
            : ReplacementFileTypeCatalog.IsFilePathCompatible(targetType, sourceFilePath);

    private static bool IsDirectTypeCompatible(
        AssetType targetType,
        string sourceFilePath,
        bool isSkeleton,
        bool isAtlas)
    {
        if (isSkeleton)
        {
            return targetType == AssetType.TextAsset && IsSkeletonSource(sourceFilePath);
        }

        if (isAtlas)
        {
            return targetType == AssetType.TextAsset &&
                   (HasExtension(sourceFilePath, ".atlas") || HasExtension(sourceFilePath, ".txt"));
        }

        return ReplacementFileTypeCatalog.IsFilePathCompatible(targetType, sourceFilePath);
    }

    private static bool HasCompatibleSemanticExtension(
        AssetListItemDto target,
        string sourceFilePath,
        bool isSkeleton)
    {
        if (isSkeleton || target.AssetType != AssetType.TextAsset)
        {
            return true;
        }

        var targetExtension = Path.GetExtension(target.Name);
        return string.IsNullOrWhiteSpace(targetExtension) ||
               string.Equals(targetExtension, Path.GetExtension(sourceFilePath), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSkeletonSource(string sourceFilePath) =>
        HasExtension(sourceFilePath, ".skel") ||
        HasExtension(sourceFilePath, ".json") ||
        HasExtension(sourceFilePath, ".bytes") ||
        HasExtension(sourceFilePath, ".txt");

    private static bool HasExtension(string sourceFilePath, string extension) =>
        string.Equals(Path.GetExtension(sourceFilePath), extension, StringComparison.OrdinalIgnoreCase);

    private static string CreateAssetKey(string bundleId, string assetId) =>
        string.Concat(bundleId, "\n", assetId);

    private static string NormalizeMatchKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string NormalizePathSeparators(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('/', Path.DirectorySeparatorChar);

    private sealed record SourceCandidate(string FilePath, IReadOnlySet<string> MatchKeys);
}
