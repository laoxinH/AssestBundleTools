using System.Numerics;
using AssestBundleTools.Application.Assets.Dtos;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal sealed record SpineDrawItem(
    string PageName,
    CanvasBitmap Texture,
    ICanvasImage Image,
    Rect SourceRect,
    Matrix3x2 Transform,
    double Width,
    double Height,
    float Opacity,
    IReadOnlyList<SpineAnimationMeshVertexDto> MeshVertices,
    IReadOnlyList<SpineAnimationMeshTriangleDto> MeshTriangles)
{
    public bool HasMesh => MeshVertices.Count > 0 && MeshTriangles.Count > 0;
}

internal readonly record struct SpineBounds(
    double CenterX,
    double CenterY,
    double Width,
    double Height,
    double FitWidth,
    double FitHeight);

internal readonly record struct SpineDrawAutoFitContribution(
    IReadOnlyList<Vector2> BoundsPoints)
{
    public static SpineDrawAutoFitContribution Empty { get; } =
        new(Array.Empty<Vector2>());
}

internal readonly record struct SpineTint(
    double R,
    double G,
    double B,
    double A)
{
    public bool HasRgbTint =>
        Math.Abs(R - 1d) > 0.0001d ||
        Math.Abs(G - 1d) > 0.0001d ||
        Math.Abs(B - 1d) > 0.0001d;
}
