using System.IO;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Infrastructure.Bundles.Internal;
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
public sealed class CachedWorkspaceSessionRegistry : IWorkspaceSessionRegistry
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
        var bundleStates = cacheEntries
            .Select(entry => MapBundleState(entry, serializedFileBundleLookup))
            .ToArray();

        var workspaceSummary = new WorkspaceSummaryDto(
            WorkspaceId: WorkspaceId.New(),
            DisplayName: NormalizeWorkspaceDisplayName(request.WorkspaceDisplayName, request.WorkspacePath, request.WorkspaceKey),
            SourcePath: string.IsNullOrWhiteSpace(request.WorkspacePath) ? string.Empty : request.WorkspacePath.Trim(),
            Mode: request.InitialMode,
            ActivePaneId: PaneId.New(),
            BundleCount: bundleStates.Length,
            AssetCount: bundleStates.Sum(static bundle => bundle.Assets.Count),
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
                TotalCount: bundleState.Assets.Count,
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
                BundleExternalDependencies: bundleState.BundleExternalDependencies);

            return true;
        }
    }

    public bool TryGetWorkspaceChangeSummary(
        WorkspaceId workspaceId,
        out WorkspaceChangeSummaryDto? changeSummary)
    {
        lock (_syncRoot)
        {
            if (!TryGetWorkspaceStateUnsafe(workspaceId, out var workspaceState))
            {
                changeSummary = null;
                return false;
            }

            var items = workspaceState.BundlesById.Values
                .SelectMany(bundleState => bundleState.Assets.Select(assetState => BuildWorkspaceChangeItem(bundleState, assetState)))
                .Where(static item => item is not null)
                .Select(static item => item!)
                .OrderByDescending(static item => item.LastChangedAtUtc)
                .ThenBy(item => item.BundleDisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.AssetName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            changeSummary = new WorkspaceChangeSummaryDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                WorkspaceDisplayName: workspaceState.Summary.DisplayName,
                SourcePath: workspaceState.Summary.SourcePath,
                BundleCount: workspaceState.Summary.BundleCount,
                AssetCount: workspaceState.Summary.AssetCount,
                TotalChangeCount: items.Length,
                ModifiedAssetCount: items.Count(static item => item.IsModified),
                ExtractedAssetCount: items.Count(static item => item.LastExtractedAtUtc.HasValue),
                WorkspaceReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName)),
                FileReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)),
                Items: items);

            return true;
        }
    }

    public bool TryExtractAsset(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetExtractionResultDto? extractionResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                !assetState.CanExtract)
            {
                extractionResult = null;
                return false;
            }

            var extractedAtUtc = DateTimeOffset.UtcNow;
            assetState.LastExtractedAtUtc = extractedAtUtc;

            extractionResult = new AssetExtractionResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                AssetName: assetState.Name,
                SuggestedOutputPath: BuildSuggestedOutputPath(bundleState.DisplayName, assetState.Name, assetState.AssetType),
                ExtractedAtUtc: extractedAtUtc);

            return true;
        }
    }

    public bool TryGetWorkspaceReplacementCandidates(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out _, out var workspaceState) ||
                !targetAssetState.CanReplace)
            {
                candidates = Array.Empty<WorkspaceReplacementCandidateDto>();
                return false;
            }

            candidates = workspaceState.BundlesById.Values
                .SelectMany(bundle => bundle.Assets.Select(asset => (Bundle: bundle, Asset: asset)))
                .Where(item => !item.Asset.AssetId.Equals(targetAssetId))
                .Where(item => item.Asset.AssetType == targetAssetState.AssetType)
                .Where(item => item.Asset.CanReplace)
                .OrderBy(item => item.Bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Asset.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new WorkspaceReplacementCandidateDto(
                    SourceBundleId: item.Bundle.BundleId,
                    SourceAssetId: item.Asset.AssetId,
                    SourceBundleDisplayName: item.Bundle.DisplayName,
                    SourceAssetName: item.Asset.Name,
                    SourceAssetType: item.Asset.AssetType))
                .ToArray();

            return true;
        }
    }

    public bool TryReplaceAssetFromFile(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        string sourceFilePath,
        AssetType sourceAssetType,
        out AssetFileReplacementResultDto? replacementResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out var targetBundleState, out var workspaceState) ||
                !targetAssetState.CanReplace ||
                targetAssetState.AssetType != sourceAssetType)
            {
                replacementResult = null;
                return false;
            }

            var appliedAtUtc = DateTimeOffset.UtcNow;
            var normalizedSourcePath = sourceFilePath.Trim();
            var sourceFileName = Path.GetFileName(normalizedSourcePath);
            if (string.IsNullOrWhiteSpace(sourceFileName))
            {
                sourceFileName = normalizedSourcePath;
            }

            targetAssetState.IsModified = true;
            targetAssetState.AppliedWorkspaceReplacement = null;
            targetAssetState.AppliedFileReplacement = new AppliedFileReplacementState(
                normalizedSourcePath,
                sourceFileName,
                sourceAssetType,
                appliedAtUtc);

            replacementResult = new AssetFileReplacementResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: targetBundleState.BundleId,
                TargetAssetId: targetAssetState.AssetId,
                TargetAssetName: targetAssetState.Name,
                SourceFilePath: normalizedSourcePath,
                SourceFileName: sourceFileName,
                SourceAssetType: sourceAssetType,
                ReplacedAtUtc: appliedAtUtc);

            return true;
        }
    }

    public bool TryReplaceAssetFromWorkspace(
        WorkspaceId workspaceId,
        BundleId targetBundleId,
        AssetId targetAssetId,
        BundleId sourceBundleId,
        AssetId sourceAssetId,
        out AssetReplacementResultDto? replacementResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, targetBundleId, targetAssetId, out var targetAssetState, out var targetBundleState, out var workspaceState) ||
                !TryGetAssetStateUnsafe(workspaceId, sourceBundleId, sourceAssetId, out var sourceAssetState, out var sourceBundleState, out _) ||
                !targetAssetState.CanReplace ||
                targetAssetState.AssetType != sourceAssetState.AssetType)
            {
                replacementResult = null;
                return false;
            }

            var replacedAtUtc = DateTimeOffset.UtcNow;
            targetAssetState.IsModified = true;
            targetAssetState.AppliedFileReplacement = null;
            targetAssetState.AppliedWorkspaceReplacement = new AppliedWorkspaceReplacementState(
                sourceBundleState.BundleId,
                sourceAssetState.AssetId,
                sourceBundleState.DisplayName,
                sourceAssetState.Name,
                sourceAssetState.AssetType,
                replacedAtUtc);

            replacementResult = new AssetReplacementResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: targetBundleState.BundleId,
                TargetAssetId: targetAssetState.AssetId,
                TargetAssetName: targetAssetState.Name,
                SourceBundleId: sourceBundleState.BundleId,
                SourceAssetId: sourceAssetState.AssetId,
                SourceBundleDisplayName: sourceBundleState.DisplayName,
                SourceAssetName: sourceAssetState.Name,
                SourceAssetType: sourceAssetState.AssetType,
                ReplacedAtUtc: replacedAtUtc);

            return true;
        }
    }

    public bool TryUndoAssetReplacement(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetReplacementUndoResultDto? undoResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                (assetState.AppliedWorkspaceReplacement is null && assetState.AppliedFileReplacement is null))
            {
                undoResult = null;
                return false;
            }

            assetState.AppliedWorkspaceReplacement = null;
            assetState.AppliedFileReplacement = null;
            assetState.IsModified = false;

            undoResult = new AssetReplacementUndoResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                TargetBundleId: bundleState.BundleId,
                TargetAssetId: assetState.AssetId,
                TargetAssetName: assetState.Name);

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

    private static CachedBundleState MapBundleState(
        BundleIndexCacheEntryDto cacheEntry,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup)
    {
        var bundleId = ResolveBundleId(cacheEntry);

        var assets = cacheEntry.Assets
            .Select(asset => MapAssetState(bundleId, asset))
            .ToArray();

        return new CachedBundleState(
            BundleId: bundleId,
            DisplayName: cacheEntry.DisplayName,
            LogicalPath: NormalizeLogicalPath(cacheEntry.LogicalPath, cacheEntry.DisplayName),
            SourcePath: cacheEntry.SourceFingerprint.SourcePath ?? string.Empty,
            BundleExternalDependencies: MapBundleExternalDependencies(cacheEntry.Dependencies, serializedFileBundleLookup),
            Assets: assets);
    }

    private static BundleId ResolveBundleId(BundleIndexCacheEntryDto cacheEntry)
    {
        return BundleId.TryParse(cacheEntry.BundleId, out var parsedBundleId)
            ? parsedBundleId
            : new BundleId($"bundle.{cacheEntry.BundleCacheKey}");
    }

    private static CachedAssetState MapAssetState(BundleId bundleId, BundleIndexAssetCacheEntryDto asset)
    {
        var assetId = AssetId.TryParse(asset.AssetId, out var parsedAssetId)
            ? parsedAssetId
            : new AssetId($"asset.{Guid.NewGuid():N}");
        var assetType = ParseAssetType(asset.AssetType);

        return new CachedAssetState(
            assetId: assetId,
            name: asset.AssetName,
            assetPath: string.IsNullOrWhiteSpace(asset.AssetPath) ? asset.AssetName : asset.AssetPath,
            containerPath: asset.ContainerPath ?? string.Empty,
            assetType: assetType,
            assetTypeDisplayName: ResolveAssetTypeDisplayName(asset.AssetTypeDisplayName, asset.AssetType),
            sizeInBytes: asset.SizeInBytes,
            preferredDisplaySizeInBytes: asset.PreferredDisplaySizeInBytes,
            pathId: asset.PathId,
            hasPreview: asset.HasPreview,
            canExtract: asset.CanExtract,
            canReplace: asset.CanReplace,
            dependencies: MapDependencies(bundleId, asset.Dependencies));
    }

    private static IReadOnlyList<AssetDependencyDto> MapDependencies(
        BundleId bundleId,
        IReadOnlyList<BundleIndexAssetDependencyCacheEntryDto>? dependencies)
    {
        if (dependencies is not { Count: > 0 })
        {
            return Array.Empty<AssetDependencyDto>();
        }

        return dependencies
            .OrderBy(static dependency => dependency.AssetName, StringComparer.OrdinalIgnoreCase)
            .Select(dependency => new AssetDependencyDto(
                AssetId.TryParse(dependency.AssetId, out var parsedAssetId)
                    ? parsedAssetId
                    : new AssetId(dependency.AssetId),
                bundleId,
                dependency.AssetName,
                ParseAssetType(dependency.AssetType),
                PathId: dependency.PathId,
                AssetTypeDisplayName: ResolveAssetTypeDisplayName(dependency.AssetTypeDisplayName, dependency.AssetType)))
            .ToArray();
    }

    /// <summary>
    /// Bundle externals 是资源包级读模型，
    /// 这里统一归一化路径、去重，并在当前工作区上下文内尽量把 `CAB-*`
    /// 反查回具体资源包文件名，避免把关联解析职责推回 Presentation。
    /// </summary>
    private static IReadOnlyList<BundleExternalDependencyDto> MapBundleExternalDependencies(
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup)
    {
        if (dependencies is not { Count: > 0 })
        {
            return Array.Empty<BundleExternalDependencyDto>();
        }

        return dependencies
            .Where(static dependency => !string.IsNullOrWhiteSpace(dependency))
            .Select(static dependency => NormalizeLogicalPath(dependency!, dependency!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static dependency => dependency, StringComparer.OrdinalIgnoreCase)
            .Select(dependency => ResolveBundleExternalDependency(dependency, serializedFileBundleLookup))
            .ToArray();
    }

    private static BundleExternalDependencyDto ResolveBundleExternalDependency(
        string dependency,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup)
    {
        var resolvedDependency = AssetsToolsBundleExternalDependencyDisplayResolver.Resolve(dependency);
        if (!TryResolveWorkspaceBundleTarget(resolvedDependency.ReferencePath, serializedFileBundleLookup, out var bundleTarget))
        {
            return resolvedDependency;
        }

        return resolvedDependency with
        {
            DisplayName = bundleTarget.DisplayName,
            ResolvedBundleDisplayName = bundleTarget.DisplayName
        };
    }

    private static bool TryResolveWorkspaceBundleTarget(
        string referencePath,
        IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> serializedFileBundleLookup,
        out WorkspaceSerializedFileBundleTarget bundleTarget)
    {
        var matchedTargets = AssetsToolsBundleExternalDependencyDisplayResolver
            .EnumerateLookupCandidates(referencePath)
            .SelectMany(candidate =>
                serializedFileBundleLookup.TryGetValue(candidate, out var candidates)
                    ? candidates
                    : Array.Empty<WorkspaceSerializedFileBundleTarget>())
            .Distinct()
            .ToArray();

        if (matchedTargets.Length == 1)
        {
            bundleTarget = matchedTargets[0];
            return true;
        }

        bundleTarget = default!;
        return false;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<WorkspaceSerializedFileBundleTarget>> BuildSerializedFileBundleLookup(
        IReadOnlyList<BundleIndexCacheEntryDto> cacheEntries)
    {
        var lookup = new Dictionary<string, HashSet<WorkspaceSerializedFileBundleTarget>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cacheEntry in cacheEntries)
        {
            var bundleTarget = new WorkspaceSerializedFileBundleTarget(
                BundleId: ResolveBundleId(cacheEntry),
                DisplayName: cacheEntry.DisplayName,
                LogicalPath: NormalizeLogicalPath(cacheEntry.LogicalPath, cacheEntry.DisplayName));

            var serializedFileNames = (cacheEntry.SerializedFileNames is { Count: > 0 }
                    ? cacheEntry.SerializedFileNames
                    : cacheEntry.Assets.Select(static asset => asset.SerializedFileName).ToArray())
                .Where(static serializedFileName => !string.IsNullOrWhiteSpace(serializedFileName))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var serializedFileName in serializedFileNames)
            {
                RegisterSerializedFileBundleTarget(lookup, serializedFileName, bundleTarget);
            }
        }

        return lookup.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<WorkspaceSerializedFileBundleTarget>)pair.Value
                .OrderBy(static target => target.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static target => target.LogicalPath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void RegisterSerializedFileBundleTarget(
        IDictionary<string, HashSet<WorkspaceSerializedFileBundleTarget>> lookup,
        string serializedFileName,
        WorkspaceSerializedFileBundleTarget bundleTarget)
    {
        var normalizedSerializedFileName = NormalizeExternalLookupKey(serializedFileName);
        RegisterSerializedFileBundleTargetCore(lookup, normalizedSerializedFileName, bundleTarget);

        var fileName = Path.GetFileName(normalizedSerializedFileName);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            RegisterSerializedFileBundleTargetCore(lookup, fileName, bundleTarget);
        }
    }

    private static void RegisterSerializedFileBundleTargetCore(
        IDictionary<string, HashSet<WorkspaceSerializedFileBundleTarget>> lookup,
        string key,
        WorkspaceSerializedFileBundleTarget bundleTarget)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (!lookup.TryGetValue(key, out var targets))
        {
            targets = new HashSet<WorkspaceSerializedFileBundleTarget>();
            lookup[key] = targets;
        }

        targets.Add(bundleTarget);
    }

    private static string NormalizeExternalLookupKey(string value)
    {
        return value.Trim().Replace('\\', '/');
    }

    /// <summary>
    /// 运行时资产图索引用于支撑“反向引用”和“依赖闭包”这类二次视图，
    /// 避免把这类遍历逻辑回推到 Presentation。
    /// </summary>
    private static IReadOnlyDictionary<string, AssetGraphNodeState> BuildAssetGraphNodes(
        IReadOnlyList<CachedBundleState> bundleStates)
    {
        return bundleStates
            .SelectMany(bundleState => bundleState.Assets.Select(assetState => new AssetGraphNodeState(
                BundleId: bundleState.BundleId,
                AssetState: assetState)))
            .ToDictionary(static node => node.AssetState.AssetId.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> BuildReverseDependenciesByAssetId(
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById)
    {
        var reverseDependencies = new Dictionary<string, List<AssetDependencyDto>>(StringComparer.Ordinal);

        foreach (var assetNode in assetsById.Values)
        {
            foreach (var dependency in assetNode.AssetState.Dependencies)
            {
                if (!reverseDependencies.TryGetValue(dependency.AssetId.Value, out var reverseDependencyList))
                {
                    reverseDependencyList = new List<AssetDependencyDto>();
                    reverseDependencies[dependency.AssetId.Value] = reverseDependencyList;
                }

                reverseDependencyList.Add(CreateDependencyDescriptor(assetNode));
            }
        }

        return reverseDependencies.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<AssetDependencyDto>)pair.Value
                .OrderBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static dependency => dependency.BundleId.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static dependency => dependency.PathId)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<AssetDependencyDto> BuildDependencyClosure(
        CachedAssetState sourceAssetState,
        IReadOnlyDictionary<string, AssetGraphNodeState> assetsById)
    {
        if (sourceAssetState.Dependencies.Count == 0)
        {
            return Array.Empty<AssetDependencyDto>();
        }

        var closure = new List<AssetDependencyDto>();
        var visitedAssetIds = new HashSet<string>(StringComparer.Ordinal);
        var pendingDependencies = new Queue<(AssetDependencyDto Dependency, int Depth)>();
        foreach (var dependency in sourceAssetState.Dependencies)
        {
            pendingDependencies.Enqueue((dependency, 1));
        }

        while (pendingDependencies.Count > 0)
        {
            var (dependency, depth) = pendingDependencies.Dequeue();
            if (!visitedAssetIds.Add(dependency.AssetId.Value))
            {
                continue;
            }

            closure.Add(dependency with
            {
                DependencyDepth = depth
            });

            if (!assetsById.TryGetValue(dependency.AssetId.Value, out var dependencyNode))
            {
                continue;
            }

            foreach (var nestedDependency in dependencyNode.AssetState.Dependencies)
            {
                pendingDependencies.Enqueue((nestedDependency, depth + 1));
            }
        }

        return closure
            .OrderBy(static dependency => dependency.DependencyDepth ?? int.MaxValue)
            .ThenBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static dependency => dependency.BundleId.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static dependency => dependency.PathId)
            .ToArray();
    }

    private static AssetDependencyDto CreateDependencyDescriptor(AssetGraphNodeState assetNode)
    {
        return new AssetDependencyDto(
            AssetId: assetNode.AssetState.AssetId,
            BundleId: assetNode.BundleId,
            Name: assetNode.AssetState.Name,
            AssetType: assetNode.AssetState.AssetType,
            PathId: assetNode.AssetState.PathId,
            AssetTypeDisplayName: assetNode.AssetState.AssetTypeDisplayName);
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

    private static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(IReadOnlyList<CachedBundleState> bundleStates)
    {
        return bundleStates
            .GroupBy(bundle => ResolveBundleGroupKey(bundle.LogicalPath), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var childBundles = group
                    .OrderBy(bundle => bundle.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(bundle => new BundleTreeNodeDto(
                        BundleId: bundle.BundleId,
                        DisplayName: bundle.DisplayName,
                        LogicalPath: bundle.LogicalPath,
                        SourcePath: bundle.SourcePath,
                        IsVirtualFolder: false,
                        AssetCount: bundle.Assets.Count,
                        Children: Array.Empty<BundleTreeNodeDto>()))
                    .ToArray();

                return new BundleTreeNodeDto(
                    BundleId: new BundleId($"folder.{group.Key}"),
                    DisplayName: group.Key,
                    LogicalPath: group.Key,
                    SourcePath: string.Empty,
                    IsVirtualFolder: true,
                    AssetCount: childBundles.Sum(static node => node.AssetCount),
                    Children: childBundles);
            })
            .ToArray();
    }

    private static string ResolveBundleGroupKey(string logicalPath)
    {
        var normalizedLogicalPath = logicalPath.Trim().Replace('\\', '/');
        var firstSlashIndex = normalizedLogicalPath.IndexOf('/');
        if (firstSlashIndex <= 0)
        {
            return "bundles";
        }

        return normalizedLogicalPath[..firstSlashIndex];
    }

    private sealed class WorkspaceSessionState
    {
        public WorkspaceSessionState(
            WorkspaceSummaryDto summary,
            IReadOnlyList<BundleTreeNodeDto> bundleTree,
            IReadOnlyDictionary<string, CachedBundleState> bundlesById,
            IReadOnlyDictionary<string, AssetGraphNodeState> assetsById)
        {
            Summary = summary;
            BundleTree = bundleTree;
            BundlesById = bundlesById;
            AssetsById = assetsById;
            ReverseDependenciesByAssetId = new Dictionary<string, IReadOnlyList<AssetDependencyDto>>(StringComparer.Ordinal);
        }

        public WorkspaceSummaryDto Summary { get; }

        public IReadOnlyList<BundleTreeNodeDto> BundleTree { get; }

        public IReadOnlyDictionary<string, CachedBundleState> BundlesById { get; }

        public IReadOnlyDictionary<string, AssetGraphNodeState> AssetsById { get; }

        public IReadOnlyDictionary<string, IReadOnlyList<AssetDependencyDto>> ReverseDependenciesByAssetId { get; private set; }

        public void InitializeDependencyIndexes()
        {
            ReverseDependenciesByAssetId = BuildReverseDependenciesByAssetId(AssetsById);
        }

        public bool TryGetReverseDependencies(
            AssetId assetId,
            out IReadOnlyList<AssetDependencyDto> reverseDependencies)
        {
            if (!string.IsNullOrWhiteSpace(assetId.Value) &&
                ReverseDependenciesByAssetId.TryGetValue(assetId.Value, out var cachedReverseDependencies))
            {
                reverseDependencies = cachedReverseDependencies;
                return true;
            }

            reverseDependencies = Array.Empty<AssetDependencyDto>();
            return false;
        }
    }

    private sealed record AssetGraphNodeState(
        BundleId BundleId,
        CachedAssetState AssetState);

    private sealed record CachedBundleState(
        BundleId BundleId,
        string DisplayName,
        string LogicalPath,
        string SourcePath,
        IReadOnlyList<BundleExternalDependencyDto> BundleExternalDependencies,
        IReadOnlyList<CachedAssetState> Assets);

    /// <summary>
    /// 表示当前工作区内某个 serialized file 对应到的资源包目标。
    /// 用于把 `archive:/CAB-*` 这类外部依赖尽量还原为用户更熟悉的 `xxx.ab` 文件名。
    /// </summary>
    private sealed record WorkspaceSerializedFileBundleTarget(
        BundleId BundleId,
        string DisplayName,
        string LogicalPath);

    private sealed class CachedAssetState
    {
        public CachedAssetState(
            AssetId assetId,
            string name,
            string assetPath,
            string containerPath,
            AssetType assetType,
            string assetTypeDisplayName,
            long sizeInBytes,
            long? preferredDisplaySizeInBytes,
            long pathId,
            bool hasPreview,
            bool canExtract,
            bool canReplace,
            IReadOnlyList<AssetDependencyDto> dependencies)
        {
            AssetId = assetId;
            Name = name;
            AssetPath = assetPath;
            ContainerPath = containerPath;
            AssetType = assetType;
            AssetTypeDisplayName = assetTypeDisplayName;
            SizeInBytes = sizeInBytes;
            PreferredDisplaySizeInBytes = preferredDisplaySizeInBytes;
            PathId = pathId;
            HasPreview = hasPreview;
            CanExtract = canExtract;
            CanReplace = canReplace;
            Dependencies = dependencies;
        }

        public AssetId AssetId { get; }

        public string Name { get; }

        public string AssetPath { get; }

        public string ContainerPath { get; }

        public AssetType AssetType { get; }

        public string AssetTypeDisplayName { get; }

        public long SizeInBytes { get; }

        public long? PreferredDisplaySizeInBytes { get; }

        public long PathId { get; }

        public bool HasPreview { get; }

        public bool CanExtract { get; }

        public bool CanReplace { get; }

        public IReadOnlyList<AssetDependencyDto> Dependencies { get; }

        public bool IsModified { get; set; }

        public DateTimeOffset? LastExtractedAtUtc { get; set; }

        public AppliedWorkspaceReplacementState? AppliedWorkspaceReplacement { get; set; }

        public AppliedFileReplacementState? AppliedFileReplacement { get; set; }
    }

    private sealed record AppliedWorkspaceReplacementState(
        BundleId SourceBundleId,
        AssetId SourceAssetId,
        string SourceBundleDisplayName,
        string SourceAssetName,
        AssetType SourceAssetType,
        DateTimeOffset AppliedAtUtc);

    private sealed record AppliedFileReplacementState(
        string SourceFilePath,
        string SourceFileName,
        AssetType SourceAssetType,
        DateTimeOffset AppliedAtUtc);
}
