using System.Numerics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using Windows.UI;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

internal static class InspectorMeshOrientationGizmoRenderer
{
    private const double DegreesToRadians = Math.PI / 180d;
    private const float Width = 118f;
    private const float Height = 56f;
    private const float AxisLength = 22f;

    private static readonly CanvasTextFormat AngleTextFormat = new()
    {
        FontFamily = "Consolas",
        FontSize = 10f
    };

    private static readonly CanvasTextFormat AxisTextFormat = new()
    {
        FontFamily = "Consolas",
        FontSize = 9f
    };

    public static void Draw(
        CanvasDrawingSession drawingSession,
        float viewportWidth,
        float viewportHeight,
        double rotationX,
        double rotationY)
    {
        if (viewportWidth < Width + 16f || viewportHeight < Height + 16f)
        {
            return;
        }

        const float margin = 8f;
        var left = margin;
        var top = viewportHeight - Height - margin;
        drawingSession.FillRoundedRectangle(
            left,
            top,
            Width,
            Height,
            8f,
            8f,
            Color.FromArgb(112, 15, 23, 42));
        drawingSession.DrawRoundedRectangle(
            left,
            top,
            Width,
            Height,
            8f,
            8f,
            Color.FromArgb(64, 226, 232, 240),
            1f);

        var origin = new Vector2(left + 31f, top + 33f);
        var axes = new[]
        {
            ProjectAxis(Vector3.UnitX, Color.FromArgb(232, 248, 113, 113), "X", rotationX, rotationY),
            ProjectAxis(Vector3.UnitY, Color.FromArgb(232, 74, 222, 128), "Y", rotationX, rotationY),
            ProjectAxis(Vector3.UnitZ, Color.FromArgb(232, 96, 165, 250), "Z", rotationX, rotationY)
        };

        foreach (var axis in axes.OrderBy(axis => axis.Depth))
        {
            var end = origin + (axis.ScreenDirection * AxisLength);
            drawingSession.DrawLine(
                origin + new Vector2(1f, 1f),
                end + new Vector2(1f, 1f),
                Color.FromArgb(92, 2, 6, 23),
                2.8f);
            drawingSession.DrawLine(origin, end, axis.Color, 2f);
            drawingSession.FillCircle(end, 2.6f, axis.Color);
            drawingSession.DrawText(
                axis.Label,
                end.X + ResolveAxisLabelOffset(axis.ScreenDirection.X),
                end.Y + ResolveAxisLabelOffset(axis.ScreenDirection.Y),
                axis.Color,
                AxisTextFormat);
        }

        drawingSession.FillCircle(origin, 2.5f, Color.FromArgb(224, 248, 250, 252));
        drawingSession.DrawText(
            FormatRotationStatus(rotationX, rotationY),
            left + 55f,
            top + 23f,
            Color.FromArgb(224, 248, 250, 252),
            AngleTextFormat);
    }

    private static OrientationAxisProjection ProjectAxis(
        Vector3 axis,
        Color color,
        string label,
        double rotationX,
        double rotationY)
    {
        var rotated = Rotate(axis, rotationX * DegreesToRadians, NormalizeRotationY(rotationY) * DegreesToRadians);
        var screenDirection = new Vector2(rotated.X, -rotated.Y);
        if (screenDirection.LengthSquared() < 0.0001f)
        {
            screenDirection = new Vector2(0f, -1f);
        }
        else
        {
            screenDirection = Vector2.Normalize(screenDirection);
        }

        return new OrientationAxisProjection(screenDirection, rotated.Z, color, label);
    }

    private static Vector3 Rotate(Vector3 axis, double rotationX, double rotationY)
    {
        var sinX = Math.Sin(rotationX);
        var cosX = Math.Cos(rotationX);
        var rotatedY = (axis.Y * cosX) - (axis.Z * sinX);
        var rotatedZ = (axis.Y * sinX) + (axis.Z * cosX);

        var sinY = Math.Sin(rotationY);
        var cosY = Math.Cos(rotationY);
        var rotatedX = (axis.X * cosY) + (rotatedZ * sinY);
        rotatedZ = (-axis.X * sinY) + (rotatedZ * cosY);

        return new Vector3((float)rotatedX, (float)rotatedY, (float)rotatedZ);
    }

    private static float ResolveAxisLabelOffset(float direction) =>
        direction >= 0f ? 3f : -9f;

    private static string FormatRotationStatus(double rotationX, double rotationY) =>
        FormattableString.Invariant(
            $"X {Math.Round(rotationX):+0;-0;0}\u00B0\nY {Math.Round(NormalizeRotationY(rotationY)):+0;-0;0}\u00B0");

    private static double NormalizeRotationY(double rotationY)
    {
        var normalizedRotation = rotationY % 360d;
        if (normalizedRotation > 180d)
        {
            normalizedRotation -= 360d;
        }
        else if (normalizedRotation < -180d)
        {
            normalizedRotation += 360d;
        }

        return normalizedRotation;
    }

    private readonly record struct OrientationAxisProjection(
        Vector2 ScreenDirection,
        float Depth,
        Color Color,
        string Label);
}
