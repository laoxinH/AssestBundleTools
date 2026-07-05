using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Application.Editing.Dtos;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 将窗口化文本编辑器当前正文或范围补丁写回已有文本草稿 payload。
/// </summary>
public sealed record UpdateTextAssetEditDraftCommand(
    WorkspaceId WorkspaceId,
    string WorkspaceKey,
    string WorkspaceDataRootPath,
    BundleId BundleId,
    AssetId AssetId,
    string DraftId,
    string? DraftMode,
    string? TextContent,
    IReadOnlyList<TextAssetEditDraftRangeEditDto>? RangeEdits = null,
    TextAssetEditDraftPatchBaselineDto? PatchBaseline = null,
    int? BaseDraftRevision = null);
