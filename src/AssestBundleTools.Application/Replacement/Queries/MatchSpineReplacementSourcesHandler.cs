using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Replacement.Queries;

/// <summary>
/// 按组成角色、名称键与 skeleton 真实格式生成 Spine 替换映射。
/// </summary>
public sealed class MatchSpineReplacementSourcesHandler :
    IQueryHandler<MatchSpineReplacementSourcesQuery, Result<IReadOnlyList<SpineReplacementSourceMatchDto>>>
{
    private readonly IReplacementSourcePathExpander _sourcePathExpander;
    private readonly SpineSkeletonReplacementCompatibilityService _skeletonCompatibilityService;

    public MatchSpineReplacementSourcesHandler(
        IReplacementSourcePathExpander sourcePathExpander,
        SpineSkeletonReplacementCompatibilityService skeletonCompatibilityService)
    {
        _sourcePathExpander = sourcePathExpander ?? throw new ArgumentNullException(nameof(sourcePathExpander));
        _skeletonCompatibilityService = skeletonCompatibilityService ?? throw new ArgumentNullException(nameof(skeletonCompatibilityService));
    }

    public async Task<Result<IReadOnlyList<SpineReplacementSourceMatchDto>>> HandleAsync(
        MatchSpineReplacementSourcesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var sourcePathsResult = await _sourcePathExpander.ExpandAsync(query.SourcePaths, cancellationToken);
        if (sourcePathsResult.IsCancelled)
        {
            return Result<IReadOnlyList<SpineReplacementSourceMatchDto>>.Cancelled();
        }

        if (!sourcePathsResult.IsSuccess || sourcePathsResult.Value is null)
        {
            return Result<IReadOnlyList<SpineReplacementSourceMatchDto>>.Failure(sourcePathsResult.Errors);
        }

        var candidates = sourcePathsResult.Value
            .Select(static path => new SourceCandidate(
                path,
                EnumerateMatchKeys(Path.GetFileName(path)).ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        var matches = new List<SpineReplacementSourceMatchDto>();
        foreach (var part in EnumerateParts(query.Group))
        {
            var partKeys = EnumeratePartMatchKeys(part).ToArray();
            foreach (var candidate in candidates)
            {
                if (!IsRoleCompatible(part, candidate.FilePath) ||
                    !partKeys.Any(candidate.MatchKeys.Contains))
                {
                    continue;
                }

                if (part.Role == SpineAssetGroupPartRole.Skeleton)
                {
                    var compatibility = await _skeletonCompatibilityService.CheckAsync(
                        query.WorkspaceId,
                        query.WorkspaceKey,
                        query.WorkspaceDataRootPath,
                        part.BundleId,
                        part.AssetId,
                        candidate.FilePath,
                        cancellationToken);
                    if (compatibility.IsCancelled)
                    {
                        return Result<IReadOnlyList<SpineReplacementSourceMatchDto>>.Cancelled();
                    }

                    if (!compatibility.IsSuccess || compatibility.Value?.IsCompatible != true)
                    {
                        continue;
                    }
                }

                matches.Add(new SpineReplacementSourceMatchDto(part.AssetId, candidate.FilePath));
                break;
            }
        }

        return Result<IReadOnlyList<SpineReplacementSourceMatchDto>>.Success(matches);
    }

    private static IEnumerable<SpineAssetGroupPartDto> EnumerateParts(SpineAssetGroupDto group) =>
        group.SkeletonAssets.Concat(group.AtlasAssets).Concat(group.TexturePageAssets);

    private static IEnumerable<string> EnumeratePartMatchKeys(SpineAssetGroupPartDto part) =>
        EnumerateMatchKeys(part.Name)
            .Concat(EnumerateMatchKeys(Path.GetFileName(NormalizePathSeparators(part.AssetPath))))
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

    private static bool IsRoleCompatible(SpineAssetGroupPartDto part, string sourceFilePath) =>
        part.Role switch
        {
            SpineAssetGroupPartRole.Skeleton => HasExtension(sourceFilePath, ".skel") ||
                                                HasExtension(sourceFilePath, ".json") ||
                                                HasExtension(sourceFilePath, ".bytes"),
            SpineAssetGroupPartRole.Atlas => HasExtension(sourceFilePath, ".atlas") ||
                                             HasExtension(sourceFilePath, ".txt"),
            SpineAssetGroupPartRole.TexturePage => ReplacementFileTypeCatalog.IsFilePathCompatible(
                AssetType.Texture2D,
                sourceFilePath),
            _ => ReplacementFileTypeCatalog.IsFilePathCompatible(part.AssetType, sourceFilePath)
        };

    private static string NormalizeMatchKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool HasExtension(string sourceFilePath, string extension) =>
        string.Equals(Path.GetExtension(sourceFilePath), extension, StringComparison.OrdinalIgnoreCase);

    private static string NormalizePathSeparators(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('/', Path.DirectorySeparatorChar);

    private sealed record SourceCandidate(string FilePath, IReadOnlySet<string> MatchKeys);
}
