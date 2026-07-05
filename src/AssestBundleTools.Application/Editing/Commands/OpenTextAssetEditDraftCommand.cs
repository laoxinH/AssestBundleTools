using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 通过已有轻量草稿引用重新打开文本编辑草稿。
/// </summary>
public sealed record OpenTextAssetEditDraftCommand(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    AssetId AssetId,
    string DraftId,
    string DraftMode,
    string? SourceFingerprint = null,
    int? DraftRevision = null);
