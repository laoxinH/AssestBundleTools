namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示已应用到工作区操作记录中的 Sprite 裁切几何。
/// </summary>
public sealed record WorkspaceSpriteEditGeometryDto(
    double CanvasWidth,
    double CanvasHeight,
    double RegionX,
    double RegionY,
    double RegionWidth,
    double RegionHeight,
    double PivotX,
    double PivotY);
