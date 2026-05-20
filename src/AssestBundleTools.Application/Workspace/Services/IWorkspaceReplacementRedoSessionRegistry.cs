using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 暴露运行时工作区替换重做能力，避免扩大基础会话注册表接口。
/// </summary>
public interface IWorkspaceReplacementRedoSessionRegistry
{
    bool TryRedoAssetReplacement(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        out AssetReplacementRedoResultDto? redoResult);
}
