using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

/// <summary>
/// 基于缓存索引构建工作区会话的注册表实现。
/// </summary>
/// <remarks>
/// 当前阶段只要求资源包列表摆脱假数据，因此该实现优先消费
/// `cache/bundle-index` 中的读模型，并在资产级真实解析接入前保持轻量可扩展。
/// </remarks>
public sealed partial class CachedWorkspaceSessionRegistry : IWorkspaceSessionRegistry
{
    private readonly object _syncRoot = new();
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly Dictionary<string, WorkspaceSessionState> _workspaceStates = new(StringComparer.Ordinal);

    public CachedWorkspaceSessionRegistry(IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(request.WorkspaceDataRootPath);
        var cacheEntries = bundleIndexCacheStore.LoadAll(request.WorkspaceKey)
            .OrderBy(entry => entry.LogicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var serializedFileBundleLookup = BuildSerializedFileBundleLookup(cacheEntries);
        var assetDependencyTargetLookup = BuildAssetDependencyTargetLookup(cacheEntries);
        var bundleStates = cacheEntries
            .Select(entry => MapBundleState(entry, serializedFileBundleLookup, assetDependencyTargetLookup))
            .ToArray();

        var workspaceSummary = new WorkspaceSummaryDto(
            WorkspaceId: WorkspaceId.New(),
            DisplayName: NormalizeWorkspaceDisplayName(request.WorkspaceDisplayName, request.WorkspacePath, request.WorkspaceKey),
            SourcePath: string.IsNullOrWhiteSpace(request.WorkspacePath) ? string.Empty : request.WorkspacePath.Trim(),
            Mode: request.InitialMode,
            ActivePaneId: PaneId.New(),
            BundleCount: bundleStates.Length,
            AssetCount: bundleStates.Sum(static bundle => bundle.Assets.Count(static asset => asset.IncludeInAssetList)),
            OpenedAtUtc: DateTimeOffset.UtcNow);

        var workspaceState = new WorkspaceSessionState(
            summary: workspaceSummary,
            bundleTree: CreateBundleTree(bundleStates),
            bundlesById: bundleStates.ToDictionary(static bundle => bundle.BundleId.Value, StringComparer.Ordinal),
            assetsById: BuildAssetGraphNodes(bundleStates));
        workspaceState.InitializeDependencyIndexes();

        lock (_syncRoot)
        {
            _workspaceStates[workspaceSummary.WorkspaceId.Value] = workspaceState;
        }

        return workspaceSummary;
    }

    public bool ContainsWorkspace(WorkspaceId workspaceId)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            return false;
        }

        lock (_syncRoot)
        {
            return _workspaceStates.ContainsKey(workspaceId.Value);
        }
    }

    public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId)
    {
        if (string.IsNullOrWhiteSpace(bundleId.Value))
        {
            return false;
        }

        lock (_syncRoot)
        {
            return TryGetWorkspaceStateUnsafe(workspaceId, out var workspaceState) &&
                   workspaceState.BundlesById.ContainsKey(bundleId.Value);
        }
    }

    public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId.Value))
        {
            return false;
        }

        lock (_syncRoot)
        {
            return TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out _, out _, out _);
        }
    }

    public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
    {
        lock (_syncRoot)
        {
            if (TryGetWorkspaceStateUnsafe(workspaceId, out var workspaceState))
            {
                bundleTree = workspaceState.BundleTree;
                return true;
            }
        }

        bundleTree = Array.Empty<BundleTreeNodeDto>();
        return false;
    }

    public bool TryGetAssetList(
        WorkspaceId workspaceId,
        BundleId bundleId,
        string? searchKeyword,
        AssetType? assetTypeFilter,
        out AssetListResultDto? assetListResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetBundleStateUnsafe(workspaceId, bundleId, out var bundleState, out var workspaceState))
            {
                assetListResult = null;
                return false;
            }

            var filteredItems = bundleState.Assets
                .Where(static asset => asset.IncludeInAssetList)
                .Where(asset => MatchesSearch(asset, searchKeyword))
                .Where(asset => !assetTypeFilter.HasValue || asset.AssetType == assetTypeFilter.Value)
                .Select(asset => new AssetListItemDto(
                    AssetId: asset.AssetId,
                    BundleId: bundleState.BundleId,
                    Name: asset.Name,
                    AssetPath: asset.AssetPath,
                    AssetType: asset.AssetType,
                    SizeInBytes: asset.SizeInBytes,
                    PreferredDisplaySizeInBytes: asset.PreferredDisplaySizeInBytes,
                    HasPreview: asset.HasPreview,
                    IsModified: asset.IsModified,
                    AssetTypeDisplayName: asset.AssetTypeDisplayName))
                .ToArray();

            assetListResult = new AssetListResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                BundleDisplayName: bundleState.DisplayName,
                SearchKeyword: NormalizeSearchKeyword(searchKeyword),
                AssetTypeFilter: assetTypeFilter,
                TotalCount: bundleState.Assets.Count(static asset => asset.IncludeInAssetList),
                FilteredCount: filteredItems.Length,
                Items: filteredItems);

            return true;
        }
    }

    public bool TryGetAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetDetailsDto? assetDetails)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState))
            {
                assetDetails = null;
                return false;
            }

            assetDetails = new AssetDetailsDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                Name: assetState.Name,
                AssetPath: assetState.AssetPath,
                ContainerPath: assetState.ContainerPath,
                AssetType: assetState.AssetType,
                SizeInBytes: assetState.SizeInBytes,
                PreferredDisplaySizeInBytes: assetState.PreferredDisplaySizeInBytes,
                PathId: assetState.PathId,
                IsModified: assetState.IsModified,
                CanExtract: assetState.CanExtract,
                CanReplaceFromFile: assetState.CanReplace,
                CanReplaceFromWorkspace: assetState.CanReplace,
                WorkspaceReplacementSourceBundleDisplayName: assetState.AppliedWorkspaceReplacement?.SourceBundleDisplayName,
                WorkspaceReplacementSourceAssetName: assetState.AppliedWorkspaceReplacement?.SourceAssetName,
                WorkspaceReplacementSourceAssetType: assetState.AppliedWorkspaceReplacement?.SourceAssetType,
                FileReplacementSourceFileName: assetState.AppliedFileReplacement?.SourceFileName,
                FileReplacementSourcePath: assetState.AppliedFileReplacement?.SourceFilePath,
                FileReplacementSourceAssetType: assetState.AppliedFileReplacement?.SourceAssetType,
                LastExtractedAtUtc: assetState.LastExtractedAtUtc,
                Dependencies: assetState.Dependencies,
                ReferencedBy: workspaceState.TryGetReverseDependencies(assetState.AssetId, out var reverseDependencies)
                    ? reverseDependencies
                    : Array.Empty<AssetDependencyDto>(),
                DependencyClosure: BuildDependencyClosure(assetState, workspaceState.AssetsById),
                AssetTypeDisplayName: assetState.AssetTypeDisplayName,
                BundleExternalDependencies: bundleState.BundleExternalDependencies,
                SerializedFileName: assetState.SerializedFileName,
                TextureAssociations: workspaceState.TryGetTextureAssociations(assetState.AssetId, out var textureAssociations)
                    ? textureAssociations
                    : Array.Empty<AssetDependencyDto>());

            return true;
        }
    }

    public bool TryOpenAssetInPane(
        WorkspaceId workspaceId, 
        BundleId bundleId,
        AssetId assetId,
        PaneId paneId,
        out AssetPaneContextDto? paneContext)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState))
            {
                paneContext = null;
                return false;
            }

            paneContext = new AssetPaneContextDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                PaneId: paneId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                AssetName: assetState.Name,
                WorkspaceMode: workspaceState.Summary.Mode);

            return true;
        }
    }

    private static string NormalizeWorkspaceDisplayName(string? workspaceDisplayName, string? workspacePath, string workspaceKey)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDisplayName))
        {
            return workspaceDisplayName.Trim();
        }

        var normalizedWorkspacePath = workspacePath?.Trim().TrimEnd('\\', '/');
        if (!string.IsNullOrWhiteSpace(normalizedWorkspacePath))
        {
            var lastSlashIndex = normalizedWorkspacePath.LastIndexOfAny(['\\', '/']);
            return lastSlashIndex >= 0
                ? normalizedWorkspacePath[(lastSlashIndex + 1)..]
                : normalizedWorkspacePath;
        }

        return workspaceKey;
    }

    private static string NormalizeLogicalPath(string logicalPath, string displayName)
    {
        if (string.IsNullOrWhiteSpace(logicalPath))
        {
            return displayName;
        }

        return logicalPath.Trim().Replace('\\', '/');
    }

    private static AssetType ParseAssetType(string assetType)
    {
        return Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var parsedAssetType)
            ? parsedAssetType
            : AssetType.Unknown;
    }

    private static string ResolveAssetTypeDisplayName(string? assetTypeDisplayName, string assetType)
    {
        if (!string.IsNullOrWhiteSpace(assetTypeDisplayName))
        {
            return assetTypeDisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(assetType)
            ? AssetType.Unknown.ToString()
            : assetType.Trim();
    }

    private static bool MatchesSearch(CachedAssetState asset, string? searchKeyword)
    {
        var normalizedSearchKeyword = NormalizeSearchKeyword(searchKeyword);
        if (normalizedSearchKeyword is null)
        {
            return true;
        }

        return asset.Name.Contains(normalizedSearchKeyword, StringComparison.OrdinalIgnoreCase) ||
               asset.AssetPath.Contains(normalizedSearchKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeSearchKeyword(string? searchKeyword) =>
        string.IsNullOrWhiteSpace(searchKeyword) ? null : searchKeyword.Trim();

    private static string BuildSuggestedOutputPath(string bundleDisplayName, string assetName, AssetType assetType)
    {
        return $"Exports/{bundleDisplayName}/{assetName}{GetAssetExtension(assetType)}";
    }

    private static WorkspaceChangeItemDto? BuildWorkspaceChangeItem(CachedBundleState bundleState, CachedAssetState assetState)
    {
        var workspaceReplacementAppliedAtUtc = assetState.AppliedWorkspaceReplacement?.AppliedAtUtc;
        var fileReplacementAppliedAtUtc = assetState.AppliedFileReplacement?.AppliedAtUtc;
        var lastChangedAtUtc = new[] { assetState.LastExtractedAtUtc, workspaceReplacementAppliedAtUtc, fileReplacementAppliedAtUtc }
            .Where(static timestamp => timestamp.HasValue)
            .Select(static timestamp => timestamp!.Value)
            .OrderByDescending(static timestamp => timestamp)
            .FirstOrDefault();

        var hasTrackedChange = assetState.IsModified ||
                               assetState.LastExtractedAtUtc.HasValue ||
                               workspaceReplacementAppliedAtUtc.HasValue ||
                               fileReplacementAppliedAtUtc.HasValue;
        if (!hasTrackedChange)
        {
            return null;
        }

        return new WorkspaceChangeItemDto(
            BundleId: bundleState.BundleId,
            AssetId: assetState.AssetId,
            BundleDisplayName: bundleState.DisplayName,
            AssetName: assetState.Name,
            AssetType: assetState.AssetType,
            IsModified: assetState.IsModified,
            LastExtractedAtUtc: assetState.LastExtractedAtUtc,
            WorkspaceReplacementSourceBundleDisplayName: assetState.AppliedWorkspaceReplacement?.SourceBundleDisplayName,
            WorkspaceReplacementSourceAssetName: assetState.AppliedWorkspaceReplacement?.SourceAssetName,
            WorkspaceReplacementAppliedAtUtc: workspaceReplacementAppliedAtUtc,
            FileReplacementSourceFileName: assetState.AppliedFileReplacement?.SourceFileName,
            FileReplacementSourcePath: assetState.AppliedFileReplacement?.SourceFilePath,
            FileReplacementAppliedAtUtc: fileReplacementAppliedAtUtc,
            LastChangedAtUtc: lastChangedAtUtc == default ? null : lastChangedAtUtc);
    }

    private static string GetAssetExtension(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D => ".png",
            AssetType.Sprite => ".png",
            AssetType.TextAsset => ".txt",
            AssetType.AudioClip => ".wav",
            AssetType.Mesh => ".mesh",
            AssetType.Material => ".mat",
            _ => ".bin"
        };

    private bool TryGetWorkspaceStateUnsafe(WorkspaceId workspaceId, out WorkspaceSessionState workspaceState)
    {
        if (string.IsNullOrWhiteSpace(workspaceId.Value))
        {
            workspaceState = null!;
            return false;
        }

        return _workspaceStates.TryGetValue(workspaceId.Value, out workspaceState!);
    }

    private bool TryGetBundleStateUnsafe(
        WorkspaceId workspaceId,
        BundleId bundleId,
        out CachedBundleState bundleState,
        out WorkspaceSessionState workspaceState)
    {
        if (!TryGetWorkspaceStateUnsafe(workspaceId, out workspaceState) ||
            string.IsNullOrWhiteSpace(bundleId.Value) ||
            !workspaceState.BundlesById.TryGetValue(bundleId.Value, out bundleState!))
        {
            bundleState = null!;
            workspaceState = null!;
            return false;
        }

        return true;
    }

    private bool TryGetAssetStateUnsafe(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out CachedAssetState assetState,
        out CachedBundleState bundleState,
        out WorkspaceSessionState workspaceState)
    {
        if (!TryGetBundleStateUnsafe(workspaceId, bundleId, out bundleState, out workspaceState) ||
            string.IsNullOrWhiteSpace(assetId.Value))
        {
            assetState = null!;
            bundleState = null!;
            workspaceState = null!;
            return false;
        }

        foreach (var candidate in bundleState.Assets)
        {
            if (string.Equals(candidate.AssetId.Value, assetId.Value, StringComparison.Ordinal))
            {
                assetState = candidate;
                return true;
            }
        }

        assetState = null!;
        bundleState = null!;
        workspaceState = null!;
        return false;
    }

}
