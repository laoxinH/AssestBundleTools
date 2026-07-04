using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示文本编辑草稿已丢弃的结果。
/// </summary>
public sealed record AssetTextEditDraftDiscardedResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    string DraftId,
    string DraftMode,
    DateTimeOffset DiscardedAtUtc);
