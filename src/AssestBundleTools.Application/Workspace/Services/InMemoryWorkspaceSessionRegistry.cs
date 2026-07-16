using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 基于内存的临时工作区注册表，用于 M2 阶段打通用例与假数据链路。
/// </summary>
public sealed partial class InMemoryWorkspaceSessionRegistry : IWorkspaceSessionRegistry, IWorkspaceReplacementRedoSessionRegistry, IWorkspaceSpriteEditSessionRegistry, IWorkspaceTextEditSessionRegistry
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, WorkspaceSessionState> _workspaceStates = new(StringComparer.Ordinal);

    public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedWorkspacePath = string.IsNullOrWhiteSpace(request.WorkspacePath)
            ? string.Empty
            : request.WorkspacePath.Trim();
        var bundleStates = string.IsNullOrWhiteSpace(normalizedWorkspacePath)
            ? new List<SampleBundleState>()
            : CreateSampleBundles();
        var workspaceSummary = new WorkspaceSummaryDto(
            WorkspaceId: WorkspaceId.New(),
            DisplayName: NormalizeWorkspaceDisplayName(request.WorkspaceDisplayName, request.WorkspacePath),
            SourcePath: normalizedWorkspacePath,
            Mode: request.InitialMode,
            ActivePaneId: PaneId.New(),
            BundleCount: bundleStates.Count,
            AssetCount: bundleStates.Sum(static bundle => bundle.Assets.Count),
            OpenedAtUtc: DateTimeOffset.UtcNow,
            WorkspaceKey: request.WorkspaceKey,
            WorkspaceDataRootPath: request.WorkspaceDataRootPath);

        var workspaceState = new WorkspaceSessionState(
            summary: workspaceSummary,
            bundleTree: CreateBundleTree(bundleStates),
            bundlesById: bundleStates.ToDictionary(static bundle => bundle.BundleId.Value, StringComparer.Ordinal));

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
        string? assetTypeDisplayNameFilter,
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
                .Where(asset => MatchesAssetType(asset, assetTypeFilter, assetTypeDisplayNameFilter))
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
                    AssetTypeDisplayName: asset.AssetTypeDisplayName,
                    LastExtractedAtUtc: asset.LastExtractedAtUtc,
                    PathId: asset.PathId,
                    CanExtract: true))
                .ToArray();

            assetListResult = new AssetListResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                BundleDisplayName: bundleState.DisplayName,
                SearchKeyword: NormalizeSearchKeyword(searchKeyword),
                AssetTypeFilter: assetTypeFilter,
                TotalCount: bundleState.Assets.Count,
                FilteredCount: filteredItems.Length,
                Items: filteredItems,
                AssetTypeDisplayNameFilter: NormalizeAssetTypeDisplayNameFilter(assetTypeDisplayNameFilter),
                TypeFacets: BuildTypeFacets(bundleState.Assets));

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
                CanExtract: true,
                CanReplaceFromFile: true,
                CanReplaceFromWorkspace: true,
                WorkspaceReplacementSourceBundleDisplayName: assetState.AppliedWorkspaceReplacement?.SourceBundleDisplayName,
                WorkspaceReplacementSourceAssetName: assetState.AppliedWorkspaceReplacement?.SourceAssetName,
                WorkspaceReplacementSourceAssetType: assetState.AppliedWorkspaceReplacement?.SourceAssetType,
                FileReplacementSourceFileName: assetState.AppliedFileReplacement?.SourceFileName,
                FileReplacementSourcePath: assetState.AppliedFileReplacement?.SourceFilePath,
                FileReplacementSourceAssetType: assetState.AppliedFileReplacement?.SourceAssetType,
                LastExtractedAtUtc: assetState.LastExtractedAtUtc,
                Dependencies: assetState.Dependencies,
                AssetTypeDisplayName: assetState.AssetTypeDisplayName,
                WorkspaceReplacementSourceBundleId: assetState.AppliedWorkspaceReplacement?.SourceBundleId,
                WorkspaceReplacementSourceAssetId: assetState.AppliedWorkspaceReplacement?.SourceAssetId,
                CanRedoReplacement: assetState.ReplacementRedoStack.Count > 0,
                SpriteEditGeometry: assetState.AppliedSpriteEdit?.Geometry,
                SpriteEditAppliedAtUtc: assetState.AppliedSpriteEdit?.AppliedAtUtc,
                TextEditDraftId: assetState.AppliedTextEdit?.DraftId,
                TextEditDraftMode: assetState.AppliedTextEdit?.DraftMode,
                TextEditAppliedAtUtc: assetState.AppliedTextEdit?.AppliedAtUtc,
                TextEditDraftRevision: assetState.AppliedTextEdit?.DraftRevision);

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
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState))
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

    /// <summary>
    /// 统一归一化工作区展示名称。
    /// 如果用户已经通过工作区向导显式命名，则优先使用该名称；
    /// 否则回退到路径名，兼容当前阶段的假数据链路。
    /// </summary>
    private static string NormalizeWorkspaceDisplayName(string? workspaceDisplayName, string workspacePath)
    {
        if (!string.IsNullOrWhiteSpace(workspaceDisplayName))
        {
            return workspaceDisplayName.Trim();
        }

        var trimmedPath = workspacePath.Trim().TrimEnd('\\', '/');
        var lastSlashIndex = trimmedPath.LastIndexOfAny(new[] { '\\', '/' });

        return lastSlashIndex >= 0
            ? trimmedPath[(lastSlashIndex + 1)..]
            : trimmedPath;
    }

    private static bool MatchesSearch(SampleAssetState asset, string? searchKeyword)
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

    private static bool MatchesAssetType(
        SampleAssetState asset,
        AssetType? assetTypeFilter,
        string? assetTypeDisplayNameFilter)
    {
        if (assetTypeFilter.HasValue && asset.AssetType != assetTypeFilter.Value)
        {
            return false;
        }

        var normalizedDisplayNameFilter = NormalizeAssetTypeDisplayNameFilter(assetTypeDisplayNameFilter);
        return normalizedDisplayNameFilter is null ||
               string.Equals(
                   asset.AssetTypeDisplayName,
                   normalizedDisplayNameFilter,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeAssetTypeDisplayNameFilter(string? assetTypeDisplayNameFilter) =>
        string.IsNullOrWhiteSpace(assetTypeDisplayNameFilter)
            ? null
            : assetTypeDisplayNameFilter.Trim();

    private static IReadOnlyList<AssetTypeFacetDto> BuildTypeFacets(IReadOnlyList<SampleAssetState> assets)
    {
        return assets
            .GroupBy(
                static asset => new AssetTypeFacetKey(
                    asset.AssetType,
                    asset.AssetTypeDisplayName),
                AssetTypeFacetKeyComparer.Instance)
            .Select(static group => new AssetTypeFacetDto(
                group.Key.AssetType,
                group.Key.AssetTypeDisplayName,
                group.Count()))
            .ToArray();
    }

    private static string BuildSuggestedOutputPath(string bundleDisplayName, string assetName, AssetType assetType)
    {
        return $"Exports/{bundleDisplayName}/{assetName}{GetAssetExtension(assetType)}";
    }

    private static WorkspaceChangeItemDto? BuildWorkspaceChangeItem(
        SampleBundleState bundleState,
        SampleAssetState assetState)
    {
        var workspaceReplacementAppliedAtUtc = assetState.AppliedWorkspaceReplacement?.AppliedAtUtc;
        var fileReplacementAppliedAtUtc = assetState.AppliedFileReplacement?.AppliedAtUtc;
        var spriteEditAppliedAtUtc = assetState.AppliedSpriteEdit?.AppliedAtUtc;
        var textEditAppliedAtUtc = assetState.AppliedTextEdit?.AppliedAtUtc;
        var lastChangedAtUtc = new[] { assetState.LastExtractedAtUtc, workspaceReplacementAppliedAtUtc, fileReplacementAppliedAtUtc, spriteEditAppliedAtUtc, textEditAppliedAtUtc }
            .Where(static timestamp => timestamp.HasValue)
            .Select(static timestamp => timestamp!.Value)
            .OrderByDescending(static timestamp => timestamp)
            .FirstOrDefault();

        var hasTrackedChange = assetState.IsModified ||
                               assetState.LastExtractedAtUtc.HasValue ||
                               workspaceReplacementAppliedAtUtc.HasValue ||
                               fileReplacementAppliedAtUtc.HasValue ||
                               spriteEditAppliedAtUtc.HasValue ||
                               textEditAppliedAtUtc.HasValue;

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
        out SampleBundleState bundleState,
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
        out SampleAssetState assetState,
        out SampleBundleState bundleState,
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
