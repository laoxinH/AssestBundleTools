using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    public bool TryApplyTextEdit(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        string draftId,
        string draftMode,
        int? draftRevision,
        out AssetTextEditAppliedResultDto? editResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                assetState.AssetType != AssetType.TextAsset)
            {
                editResult = null;
                return false;
            }

            var appliedAtUtc = DateTimeOffset.UtcNow;
            var normalizedDraftId = string.IsNullOrWhiteSpace(draftId)
                ? Guid.NewGuid().ToString("N")
                : draftId.Trim();
            var normalizedDraftMode = string.IsNullOrWhiteSpace(draftMode)
                ? WorkspaceTextEditOperationRecorder.TextDraftMode
                : draftMode.Trim();

            ApplyTextEditState(
                assetState,
                new AppliedTextEditState(
                    normalizedDraftId,
                    normalizedDraftMode,
                    draftRevision,
                    appliedAtUtc));

            editResult = new AssetTextEditAppliedResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                AssetName: assetState.Name,
                AssetType: assetState.AssetType,
                AssetTypeDisplayName: assetState.AssetTypeDisplayName,
                DraftId: normalizedDraftId,
                DraftMode: normalizedDraftMode,
                AppliedAtUtc: appliedAtUtc,
                DraftRevision: draftRevision);

            return true;
        }
    }
}
