using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示文本编辑草稿已应用为工作区操作记录的结果。
/// </summary>
public sealed record AssetTextEditAppliedResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    AssetType AssetType,
    string? AssetTypeDisplayName,
    string DraftId,
    string DraftMode,
    DateTimeOffset AppliedAtUtc,
    int? DraftRevision = null);
