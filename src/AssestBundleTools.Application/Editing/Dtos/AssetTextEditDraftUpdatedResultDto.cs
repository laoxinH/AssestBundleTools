using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示文本编辑草稿正文已经写入 draft store 的结果。
/// </summary>
public sealed record AssetTextEditDraftUpdatedResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    string DraftId,
    string DraftMode,
    AssetPreviewFullContentReferenceDto Reference,
    DateTimeOffset UpdatedAtUtc,
    int? DraftRevision = null,
    TextAssetEditDraftPatchBaselineDto? PatchBaseline = null);
