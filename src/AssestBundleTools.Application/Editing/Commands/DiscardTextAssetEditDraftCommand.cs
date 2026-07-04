using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 丢弃一个未应用的文本类资产编辑草稿。
/// </summary>
public sealed record DiscardTextAssetEditDraftCommand(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    AssetId AssetId,
    string DraftId,
    string? DraftMode = null);
