using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 暴露当前会话内可物化的 TextAsset 编辑状态。
/// </summary>
public interface IWorkspaceTextEditSessionRegistry
{
    bool TryApplyTextEdit(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        string draftId,
        string draftMode,
        out AssetTextEditAppliedResultDto? editResult);
}
