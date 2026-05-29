using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 应用当前 Sprite 编辑工作面的裁切几何。
/// </summary>
public sealed record ApplySpriteEditCommand(
    WorkspaceId WorkspaceId,
    BundleId BundleId,
    AssetId AssetId,
    WorkspaceSpriteEditGeometryDto Geometry,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null,
    string? DraftId = null,
    string? DraftMode = null);
