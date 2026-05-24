using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Infrastructure.Workspace.Runtime;

public sealed partial class CachedWorkspaceSessionRegistry
{
    public bool TryApplySpriteEdit(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        string draftId,
        string draftMode,
        WorkspaceSpriteEditGeometryDto geometry,
        out AssetSpriteEditAppliedResultDto? editResult)
    {
        lock (_syncRoot)
        {
            if (!TryGetAssetStateUnsafe(workspaceId, bundleId, assetId, out var assetState, out var bundleState, out var workspaceState) ||
                assetState.AssetType != AssetType.Sprite)
            {
                editResult = null;
                return false;
            }

            var appliedAtUtc = DateTimeOffset.UtcNow;
            var normalizedDraftId = string.IsNullOrWhiteSpace(draftId)
                ? Guid.NewGuid().ToString("N")
                : draftId.Trim();
            var normalizedDraftMode = string.IsNullOrWhiteSpace(draftMode)
                ? "sprite-geometry"
                : draftMode.Trim();

            ApplySpriteEditState(
                assetState,
                new AppliedSpriteEditState(
                    normalizedDraftId,
                    normalizedDraftMode,
                    geometry,
                    appliedAtUtc));

            editResult = new AssetSpriteEditAppliedResultDto(
                WorkspaceId: workspaceState.Summary.WorkspaceId,
                BundleId: bundleState.BundleId,
                AssetId: assetState.AssetId,
                AssetName: assetState.Name,
                DraftId: normalizedDraftId,
                DraftMode: normalizedDraftMode,
                Geometry: geometry,
                AppliedAtUtc: appliedAtUtc);

            return true;
        }
    }
}
