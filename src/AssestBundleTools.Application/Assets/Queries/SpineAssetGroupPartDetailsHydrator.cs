using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 为已识别出的 Spine 组成资产补齐资产详情中的操作记录和当前替换状态。
/// </summary>
internal static class SpineAssetGroupPartDetailsHydrator
{
    public static SpineAssetGroupListResultDto HydrateResult(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        SpineAssetGroupListResultDto result,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);
        ArgumentNullException.ThrowIfNull(result);

        var detailsCache = new Dictionary<string, AssetDetailsDto>(StringComparer.OrdinalIgnoreCase);
        var items = result.Items
            .Select(group => HydrateGroup(workspaceSessionRegistry, workspaceId, group, detailsCache, cancellationToken))
            .ToArray();

        return result with { Items = items };
    }

    public static SpineAssetGroupDto HydrateGroup(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        SpineAssetGroupDto group,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspaceSessionRegistry);
        ArgumentNullException.ThrowIfNull(group);

        return HydrateGroup(
            workspaceSessionRegistry,
            workspaceId,
            group,
            new Dictionary<string, AssetDetailsDto>(StringComparer.OrdinalIgnoreCase),
            cancellationToken);
    }

    private static SpineAssetGroupDto HydrateGroup(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        SpineAssetGroupDto group,
        IDictionary<string, AssetDetailsDto> detailsCache,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return group;
        }

        var hydratedSkeletons = HydrateParts(
            workspaceSessionRegistry,
            workspaceId,
            group.SkeletonAssets,
            detailsCache,
            cancellationToken);
        var hydratedAtlases = HydrateParts(
            workspaceSessionRegistry,
            workspaceId,
            group.AtlasAssets,
            detailsCache,
            cancellationToken);
        var hydratedTexturePages = HydrateParts(
            workspaceSessionRegistry,
            workspaceId,
            group.TexturePageAssets,
            detailsCache,
            cancellationToken);
        var hydratedPartsByKey = hydratedSkeletons
            .Concat(hydratedAtlases)
            .Concat(hydratedTexturePages)
            .GroupBy(CreateAssetKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static grouped => grouped.Key, static grouped => grouped.First(), StringComparer.OrdinalIgnoreCase);

        return group with
        {
            SkeletonAssets = hydratedSkeletons,
            AtlasAssets = hydratedAtlases,
            TexturePageAssets = hydratedTexturePages,
            Diagnostics = HydrateDiagnostics(group.Diagnostics, hydratedPartsByKey)
        };
    }

    private static IReadOnlyList<SpineAssetGroupPartDto> HydrateParts(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        IReadOnlyList<SpineAssetGroupPartDto> parts,
        IDictionary<string, AssetDetailsDto> detailsCache,
        CancellationToken cancellationToken)
    {
        if (parts.Count == 0 || cancellationToken.IsCancellationRequested)
        {
            return parts;
        }

        return parts
            .Select(part => HydratePart(workspaceSessionRegistry, workspaceId, part, detailsCache, cancellationToken))
            .ToArray();
    }

    private static SpineAssetGroupPartDto HydratePart(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        SpineAssetGroupPartDto part,
        IDictionary<string, AssetDetailsDto> detailsCache,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested ||
            !TryGetAssetDetails(workspaceSessionRegistry, workspaceId, part, detailsCache, out var details) ||
            details is null)
        {
            return part;
        }

        return part with
        {
            LastExtractedAtUtc = details.LastExtractedAtUtc ?? part.LastExtractedAtUtc,
            FileReplacementSourceFileName = details.FileReplacementSourceFileName,
            FileReplacementSourcePath = details.FileReplacementSourcePath,
            FileReplacementSourceAssetType = details.FileReplacementSourceAssetType,
            ChangeRecords = details.ChangeRecords
        };
    }

    private static bool TryGetAssetDetails(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        WorkspaceId workspaceId,
        SpineAssetGroupPartDto part,
        IDictionary<string, AssetDetailsDto> detailsCache,
        out AssetDetailsDto? details)
    {
        var assetKey = CreateAssetKey(part);
        if (detailsCache.TryGetValue(assetKey, out details))
        {
            return true;
        }

        if (!workspaceSessionRegistry.TryGetAssetDetails(
                workspaceId,
                part.BundleId,
                part.AssetId,
                out details) ||
            details is null)
        {
            return false;
        }

        detailsCache[assetKey] = details;
        return true;
    }

    private static IReadOnlyList<SpineAssetGroupDiagnosticDto> HydrateDiagnostics(
        IReadOnlyList<SpineAssetGroupDiagnosticDto> diagnostics,
        IReadOnlyDictionary<string, SpineAssetGroupPartDto> hydratedPartsByKey)
    {
        if (diagnostics.Count == 0 || hydratedPartsByKey.Count == 0)
        {
            return diagnostics;
        }

        return diagnostics
            .Select(diagnostic => diagnostic with
            {
                RelatedParts = diagnostic.RelatedParts
                    .Select(part => hydratedPartsByKey.TryGetValue(CreateAssetKey(part), out var hydratedPart)
                        ? hydratedPart
                        : part)
                    .ToArray()
            })
            .ToArray();
    }

    private static string CreateAssetKey(SpineAssetGroupPartDto part) =>
        SpineAssetGroupDetector.CreateAssetKey(part.BundleId, part.AssetId);
}
