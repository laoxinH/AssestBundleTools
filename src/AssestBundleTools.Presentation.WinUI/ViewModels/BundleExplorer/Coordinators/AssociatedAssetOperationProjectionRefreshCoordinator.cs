using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 刷新关联资源的轻量操作投影，避免变更记录按钮等待重型预览重载。
/// </summary>
internal sealed class AssociatedAssetOperationProjectionRefreshCoordinator
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly BundleExplorerAssetInspectorPresenter _assetInspectorPresenter;
    private readonly SpineAssetGroupInspectorPresenter _spineGroupInspectorPresenter;
    private readonly Func<WorkspaceId?> _workspaceIdProvider;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<IReadOnlyList<AppError>> _appendResultFailure;

    public AssociatedAssetOperationProjectionRefreshCoordinator(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        BundleExplorerAssetInspectorPresenter assetInspectorPresenter,
        SpineAssetGroupInspectorPresenter spineGroupInspectorPresenter,
        Func<WorkspaceId?> workspaceIdProvider,
        Func<bool> isBusyProvider,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _assetInspectorPresenter = assetInspectorPresenter ?? throw new ArgumentNullException(nameof(assetInspectorPresenter));
        _spineGroupInspectorPresenter = spineGroupInspectorPresenter ?? throw new ArgumentNullException(nameof(spineGroupInspectorPresenter));
        _workspaceIdProvider = workspaceIdProvider ?? throw new ArgumentNullException(nameof(workspaceIdProvider));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _appendResultFailure = appendResultFailure ?? throw new ArgumentNullException(nameof(appendResultFailure));
    }

    public async Task<bool> RefreshAssetDocumentOperationProjectionAsync(
        AssetDocumentTabViewModel assetTab)
    {
        ArgumentNullException.ThrowIfNull(assetTab);

        var workspaceId = _workspaceIdProvider();
        if (!workspaceId.HasValue)
        {
            return false;
        }

        var detailsResult = await GetAssetDetailsAsync(
            workspaceId.Value,
            assetTab.BundleId,
            assetTab.AssetId);
        if (!TryGetValue(detailsResult, out var details))
        {
            return false;
        }

        if (HasSameOperationProjection(assetTab.Details, details))
        {
            return false;
        }

        assetTab.ApplyOperationProjection(details);
        _assetInspectorPresenter.PresentAssetDocumentTab(
            assetTab,
            details,
            canEdit: assetTab.CanEdit,
            isBusy: _isBusyProvider());
        return true;
    }

    public async Task<bool> RefreshSpineGroupOperationProjectionAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets = null)
    {
        ArgumentNullException.ThrowIfNull(spineGroupTab);

        var workspaceId = _workspaceIdProvider();
        if (!workspaceId.HasValue)
        {
            return false;
        }

        var affectedAssetSet = CreateAffectedAssetSet(affectedAssets);
        var detailsCache = new Dictionary<string, AssetDetailsDto>(StringComparer.Ordinal);
        var group = spineGroupTab.Item;
        var refreshedGroup = group with
        {
            SkeletonAssets = await RefreshPartsAsync(workspaceId.Value, group.SkeletonAssets, affectedAssetSet, detailsCache),
            AtlasAssets = await RefreshPartsAsync(workspaceId.Value, group.AtlasAssets, affectedAssetSet, detailsCache),
            TexturePageAssets = await RefreshPartsAsync(workspaceId.Value, group.TexturePageAssets, affectedAssetSet, detailsCache)
        };

        if (SpineAssetGroupSnapshotComparer.HasSameSnapshot(group, refreshedGroup))
        {
            return false;
        }

        spineGroupTab.ApplyOperationProjection(refreshedGroup);
        _spineGroupInspectorPresenter.PresentSpineGroupOperationProjection(
            spineGroupTab,
            _isBusyProvider());
        return true;
    }

    private async Task<IReadOnlyList<SpineAssetGroupPartDto>> RefreshPartsAsync(
        WorkspaceId workspaceId,
        IReadOnlyList<SpineAssetGroupPartDto> parts,
        ISet<string>? affectedAssetSet,
        IDictionary<string, AssetDetailsDto> detailsCache)
    {
        if (parts.Count == 0)
        {
            return parts;
        }

        var refreshedParts = new SpineAssetGroupPartDto[parts.Count];
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            if (affectedAssetSet is not null &&
                !affectedAssetSet.Contains(CreateAssetKey(part.BundleId, part.AssetId)))
            {
                refreshedParts[index] = part;
                continue;
            }

            refreshedParts[index] = await TryRefreshPartAsync(workspaceId, part, detailsCache);
        }

        return refreshedParts;
    }

    private async Task<SpineAssetGroupPartDto> TryRefreshPartAsync(
        WorkspaceId workspaceId,
        SpineAssetGroupPartDto part,
        IDictionary<string, AssetDetailsDto> detailsCache)
    {
        var assetKey = CreateAssetKey(part.BundleId, part.AssetId);
        if (!detailsCache.TryGetValue(assetKey, out var details))
        {
            var detailsResult = await GetAssetDetailsAsync(
                workspaceId,
                part.BundleId,
                part.AssetId);
            if (!TryGetValue(detailsResult, out details))
            {
                return part;
            }

            detailsCache[assetKey] = details;
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

    private bool TryGetValue<T>(Result<T> result, out T value)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default!;
        if (!result.IsCancelled)
        {
            _appendResultFailure(result.Errors);
        }

        return false;
    }

    private static ISet<string>? CreateAffectedAssetSet(IReadOnlyList<WorkspaceOperationAffectedAsset>? affectedAssets)
    {
        if (affectedAssets is null || affectedAssets.Count == 0)
        {
            return null;
        }

        return affectedAssets
            .Select(static asset => CreateAssetKey(asset.BundleId, asset.AssetId))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string CreateAssetKey(BundleId bundleId, AssetId assetId) =>
        string.Concat(bundleId.Value, "\n", assetId.Value);

    private Task<Result<AssetDetailsDto>> GetAssetDetailsAsync(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId) =>
        _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.GetAssetDetailsAsync(
                new GetAssetDetailsQuery(
                    workspaceId,
                    bundleId,
                    assetId),
                token));

    private static bool HasSameOperationProjection(
        AssetDetailsDto? previousDetails,
        AssetDetailsDto currentDetails)
    {
        if (previousDetails is null)
        {
            return false;
        }

        return previousDetails.IsModified == currentDetails.IsModified &&
               previousDetails.CanRedoReplacement == currentDetails.CanRedoReplacement &&
               previousDetails.LastExtractedAtUtc == currentDetails.LastExtractedAtUtc &&
               string.Equals(previousDetails.FileReplacementSourceFileName, currentDetails.FileReplacementSourceFileName, StringComparison.Ordinal) &&
               string.Equals(previousDetails.FileReplacementSourcePath, currentDetails.FileReplacementSourcePath, StringComparison.Ordinal) &&
               string.Equals(previousDetails.WorkspaceReplacementSourceBundleId?.Value, currentDetails.WorkspaceReplacementSourceBundleId?.Value, StringComparison.Ordinal) &&
               string.Equals(previousDetails.WorkspaceReplacementSourceAssetId?.Value, currentDetails.WorkspaceReplacementSourceAssetId?.Value, StringComparison.Ordinal) &&
               HasSameChangeRecords(previousDetails.ChangeRecords, currentDetails.ChangeRecords);
    }

    private static bool HasSameChangeRecords(
        IReadOnlyList<AssetChangeRecordDto>? left,
        IReadOnlyList<AssetChangeRecordDto>? right)
    {
        var leftRecords = left ?? Array.Empty<AssetChangeRecordDto>();
        var rightRecords = right ?? Array.Empty<AssetChangeRecordDto>();
        if (leftRecords.Count != rightRecords.Count)
        {
            return false;
        }

        for (var index = 0; index < leftRecords.Count; index++)
        {
            var leftRecord = leftRecords[index];
            var rightRecord = rightRecords[index];
            if (leftRecord.Kind != rightRecord.Kind ||
                leftRecord.Sequence != rightRecord.Sequence ||
                leftRecord.IsEffectiveReplacement != rightRecord.IsEffectiveReplacement ||
                leftRecord.IsEffectiveEdit != rightRecord.IsEffectiveEdit ||
                !string.Equals(leftRecord.OperationId, rightRecord.OperationId, StringComparison.Ordinal) ||
                !string.Equals(leftRecord.TargetBundleId, rightRecord.TargetBundleId, StringComparison.Ordinal) ||
                !string.Equals(leftRecord.TargetAssetId, rightRecord.TargetAssetId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
