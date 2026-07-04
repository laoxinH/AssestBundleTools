using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 为文本类资产创建或刷新一个工作区文件背书编辑草稿。
/// </summary>
public sealed record CreateTextAssetEditDraftCommand(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    AssetId AssetId,
    string? DraftId = null,
    string? DraftMode = null);
