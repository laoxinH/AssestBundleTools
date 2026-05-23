namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

internal enum SpriteEditDragKind
{
    None,
    Move,
    Pivot,
    ResizeLeft,
    ResizeRight,
    ResizeTop,
    ResizeBottom,
    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight
}

internal readonly record struct SpriteEditDragState(
    SpriteEditDragKind Kind,
    double StartPointerSourceX,
    double StartPointerSourceY,
    double StartRegionX,
    double StartRegionY,
    double StartRegionWidth,
    double StartRegionHeight,
    double StartPivotX,
    double StartPivotY,
    SpriteEditDragViewportMapping ViewportMapping);

internal readonly record struct SpriteEditDeferredViewport(
    double Width,
    double Height);
