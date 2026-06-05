using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    private async Task RefreshOperationAffectedStateAsync(WorkspaceOperationChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        var assetTabs = eventArgs.IsWorkspaceWide
            ? DocumentGroup.AssetTabs.ToArray()
            : DocumentGroup.AssetTabs
                .Where(assetTab => eventArgs.AffectedAssets.Any(affectedAsset =>
                    assetTab.BundleId.Equals(affectedAsset.BundleId) &&
                    assetTab.AssetId.Equals(affectedAsset.AssetId)))
                .ToArray();
        foreach (var assetTab in assetTabs)
        {
            await RefreshAssetDocumentDetailsAsync(assetTab);
        }

        var spineGroupTabs = eventArgs.IsWorkspaceWide
            ? DocumentGroup.SpineGroupTabs.ToArray()
            : DocumentGroup.SpineGroupTabs
                .Where(spineGroupTab => eventArgs.AffectedAssets.Any(affectedAsset =>
                    SpineGroupContainsAsset(spineGroupTab, affectedAsset)))
                .ToArray();
        foreach (var spineGroupTab in spineGroupTabs)
        {
            QueueSpineGroupDocumentDetailsRefresh(spineGroupTab);
        }
    }

    private async Task RefreshAssetDocumentDetailsAsync(AssetDocumentTabViewModel assetTab)
    {
        if (!DocumentGroup.AssetTabs.Contains(assetTab) ||
            _workspaceShell.WorkspaceSummary is not { } workspaceSummary)
        {
            return;
        }

        var detailsResult = await _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.GetAssetDetailsAsync(
                new GetAssetDetailsQuery(
                    WorkspaceId: workspaceSummary.WorkspaceId,
                    BundleId: assetTab.BundleId,
                    AssetId: assetTab.AssetId),
                token));
        if (!DocumentGroup.AssetTabs.Contains(assetTab) ||
            !TryGetValue(detailsResult, out var details))
        {
            return;
        }

        var shouldReloadPreview = AssetPreviewInvalidationPolicy.HasEffectivePreviewProjectionChanged(
            assetTab.Details,
            details);
        ReapplyAssetDocumentState(assetTab, details);
        if (!shouldReloadPreview)
        {
            return;
        }

        if (ReferenceEquals(assetTab, DocumentGroup.ActiveAssetTab))
        {
            _assetPreviewLoadCoordinator.ReloadForegroundPreview(assetTab);
        }
        else
        {
            _assetPreviewLoadCoordinator.InvalidatePreview(assetTab);
        }
    }

    private void QueueSpineGroupDocumentDetailsRefresh(SpineAssetGroupDocumentTabViewModel spineGroupTab)
    {
        if (!DocumentGroup.SpineGroupTabs.Contains(spineGroupTab))
        {
            return;
        }

        ReapplySpineGroupDocumentState(spineGroupTab);
        _ = _spineGroupDetailsRefreshScheduler.RequestAsync(spineGroupTab);
    }

    private Task RefreshSpineGroupDocumentDetailsFromRequestAsync(SpineAssetGroupDetailsRefreshRequest request)
    {
        return !request.IsCurrent
            ? Task.CompletedTask
            : RefreshSpineGroupDocumentDetailsAsync(request.GroupTab, request.CancellationToken, () => request.IsCurrent);
    }

    private async Task RefreshSpineGroupDocumentDetailsAsync(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        CancellationToken cancellationToken,
        Func<bool>? isCurrent = null)
    {
        if (!DocumentGroup.SpineGroupTabs.Contains(spineGroupTab))
        {
            return;
        }

        var expectedGroupId = spineGroupTab.GroupId;
        var expectedGroup = spineGroupTab.Item;
        var loadResult = await _spineGroupDetailsLoader.LoadAsync(spineGroupTab, cancellationToken);
        if (!DocumentGroup.SpineGroupTabs.Contains(spineGroupTab) ||
            !string.Equals(expectedGroupId, spineGroupTab.GroupId, StringComparison.Ordinal) ||
            !ReferenceEquals(expectedGroup, spineGroupTab.Item) ||
            isCurrent?.Invoke() == false)
        {
            return;
        }

        if (loadResult is not null)
        {
            spineGroupTab.ApplyDetails(
                loadResult.Details,
                loadResult.TexturePreview,
                loadResult.AnimationTextures);
        }

        ReapplySpineGroupDocumentState(spineGroupTab);
    }

    private static bool SpineGroupContainsAsset(
        SpineAssetGroupDocumentTabViewModel spineGroupTab,
        WorkspaceOperationAffectedAsset affectedAsset) =>
        spineGroupTab.Item.SkeletonAssets
            .Concat(spineGroupTab.Item.AtlasAssets)
            .Concat(spineGroupTab.Item.TexturePageAssets)
            .Any(part =>
                part.BundleId.Equals(affectedAsset.BundleId) &&
                part.AssetId.Equals(affectedAsset.AssetId));
}
