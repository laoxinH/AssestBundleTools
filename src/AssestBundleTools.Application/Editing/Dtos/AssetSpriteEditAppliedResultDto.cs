using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Dtos;

/// <summary>
/// 表示 Sprite 裁切几何已作为工作区编辑记录应用。
/// </summary>
public sealed record AssetSpriteEditAppliedResultDto(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    string AssetName,
    string DraftId,
    string DraftMode,
    WorkspaceSpriteEditGeometryDto Geometry,
    DateTimeOffset AppliedAtUtc);
