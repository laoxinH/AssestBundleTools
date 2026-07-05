using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 将文本类资产编辑草稿应用为工作区可恢复操作记录。
/// </summary>
public sealed record ApplyTextAssetEditDraftCommand(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    AssetId AssetId,
    string DraftId,
    string? DraftMode = null,
    int? DraftRevision = null);
