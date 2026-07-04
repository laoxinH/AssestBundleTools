using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示可由 UI 持有的文本编辑草稿轻量状态。
/// </summary>
public sealed record AssetTextEditDraftDto(
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
    bool CanApply,
    string? Summary = null);
