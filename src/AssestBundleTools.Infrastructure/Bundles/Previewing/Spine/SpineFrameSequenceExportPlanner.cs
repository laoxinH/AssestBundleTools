using System.Globalization;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal static class SpineFrameSequenceExportPlanner
{
    private const int DefaultCanvasSize = 1;
    private const int MinimumFrameNumberDigits = 4;
    private const double MinimumContentExtent = 1d;
    private const double MinimumScale = 0.000001d;

    public static bool IsValidRequest(SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request) =>
        request.FramesPerSecond > 0d &&
        !double.IsNaN(request.FramesPerSecond) &&
        !double.IsInfinity(request.FramesPerSecond) &&
        request.PaddingPixels >= 0 &&
        IsValidCanvasSize(request.CanvasWidth) &&
        IsValidCanvasSize(request.CanvasHeight);

    public static SpineAnimationRuntimeFrameSequenceExportPlanResultDto CreatePlan(
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request,
        SpineAnimationRuntimeFixedFrameSamplingResultDto samplingResult)
    {
        if (samplingResult.Frames.Count == 0 ||
            samplingResult.SampleTimes.Count != samplingResult.Frames.Count)
        {
            return SpineAnimationRuntimeFrameSequenceExportPlanResultDto.NotHandled;
        }

        var bounds = ResolveBounds(samplingResult.Frames);
        var layout = CreateLayout(request, bounds);
        var frames = CreateFrames(request, samplingResult);
        return SpineAnimationRuntimeFrameSequenceExportPlanResultDto.Handled(
            samplingResult.FramesPerSecond,
            samplingResult.Duration,
            layout,
            frames,
            samplingResult.Diagnostics);
    }

    private static IReadOnlyList<SpineAnimationRuntimeFrameSequenceExportFrameDto> CreateFrames(
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request,
        SpineAnimationRuntimeFixedFrameSamplingResultDto samplingResult)
    {
        var digits = Math.Max(
            MinimumFrameNumberDigits,
            Math.Max(0, samplingResult.Frames.Count - 1).ToString(CultureInfo.InvariantCulture).Length);
        var prefix = ResolveFileNamePrefix(request);
        var frames = new List<SpineAnimationRuntimeFrameSequenceExportFrameDto>(samplingResult.Frames.Count);
        for (var index = 0; index < samplingResult.Frames.Count; index++)
        {
            var fileName = string.Create(
                CultureInfo.InvariantCulture,
                $"{prefix}_{index.ToString("D" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)}.png");
            frames.Add(new SpineAnimationRuntimeFrameSequenceExportFrameDto(
                index,
                samplingResult.SampleTimes[index],
                fileName,
                samplingResult.Frames[index]));
        }

        return frames;
    }

    private static SpineAnimationRuntimeFrameSequenceExportLayoutDto CreateLayout(
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request,
        SpineFrameSequenceBounds bounds)
    {
        var contentWidth = Math.Max(bounds.MaxX - bounds.MinX, MinimumContentExtent);
        var contentHeight = Math.Max(bounds.MaxY - bounds.MinY, MinimumContentExtent);
        var padding = request.PaddingPixels;
        var canvasWidth = request.CanvasWidth ?? ToCanvasSize(contentWidth + (padding * 2d));
        var canvasHeight = request.CanvasHeight ?? ToCanvasSize(contentHeight + (padding * 2d));
        var availableWidth = Math.Max(DefaultCanvasSize, canvasWidth - (padding * 2d));
        var availableHeight = Math.Max(DefaultCanvasSize, canvasHeight - (padding * 2d));
        var scale = Math.Max(
            MinimumScale,
            Math.Min(availableWidth / contentWidth, availableHeight / contentHeight));
        var left = (canvasWidth - (contentWidth * scale)) / 2d;
        var top = (canvasHeight - (contentHeight * scale)) / 2d;
        var offsetX = left - (bounds.MinX * scale);
        var offsetY = top + (bounds.MaxY * scale);

        return new SpineAnimationRuntimeFrameSequenceExportLayoutDto(
            canvasWidth,
            canvasHeight,
            padding,
            bounds.MinX,
            bounds.MinY,
            bounds.MaxX,
            bounds.MaxY,
            scale,
            offsetX,
            offsetY);
    }

    private static SpineFrameSequenceBounds ResolveBounds(
        IReadOnlyList<SpineAnimationFrameDto> frames)
    {
        var builder = new SpineFrameSequenceBoundsBuilder();
        foreach (var frame in frames)
        {
            foreach (var command in frame.DrawCommands)
            {
                IncludeCommandBounds(command, builder);
            }
        }

        return builder.Build();
    }

    private static void IncludeCommandBounds(
        SpineAnimationDrawCommandDto command,
        SpineFrameSequenceBoundsBuilder builder)
    {
        if (command.MeshVertices.Count > 0)
        {
            foreach (var vertex in command.MeshVertices)
            {
                builder.Include(vertex.X, vertex.Y);
            }

            return;
        }

        if (command.Width <= 0d || command.Height <= 0d)
        {
            return;
        }

        var halfWidth = command.Width / 2d;
        var halfHeight = command.Height / 2d;
        IncludeTransformedPoint(command, builder, -halfWidth, -halfHeight);
        IncludeTransformedPoint(command, builder, halfWidth, -halfHeight);
        IncludeTransformedPoint(command, builder, halfWidth, halfHeight);
        IncludeTransformedPoint(command, builder, -halfWidth, halfHeight);
    }

    private static void IncludeTransformedPoint(
        SpineAnimationDrawCommandDto command,
        SpineFrameSequenceBoundsBuilder builder,
        double x,
        double y)
    {
        builder.Include(
            (x * command.TransformM11) + (y * command.TransformM21) + command.TransformM31,
            (x * command.TransformM12) + (y * command.TransformM22) + command.TransformM32);
    }

    private static int ToCanvasSize(double value)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            return DefaultCanvasSize;
        }

        return value >= int.MaxValue
            ? int.MaxValue
            : Math.Max(DefaultCanvasSize, (int)Math.Ceiling(value));
    }

    private static bool IsValidCanvasSize(int? value) =>
        !value.HasValue || value.Value > 0;

    private static string ResolveFileNamePrefix(
        SpineAnimationRuntimeFrameSequenceExportPlanRequestDto request)
    {
        var requestedPrefix = string.IsNullOrWhiteSpace(request.FileNamePrefix)
            ? request.AnimationName
            : request.FileNamePrefix;
        return SanitizeFileNameStem(string.IsNullOrWhiteSpace(requestedPrefix)
            ? "spine"
            : requestedPrefix);
    }

    private static string SanitizeFileNameStem(string value)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            builder.Append(IsInvalidFileNameCharacter(character, invalidCharacters) ? '_' : character);
        }

        var sanitized = builder.ToString().Trim(' ', '.', '_');
        return sanitized.Length == 0 ? "spine" : sanitized;
    }

    private static bool IsInvalidFileNameCharacter(char character, IReadOnlyCollection<char> invalidCharacters) =>
        char.IsControl(character) ||
        char.IsWhiteSpace(character) ||
        invalidCharacters.Contains(character);

    private readonly record struct SpineFrameSequenceBounds(
        double MinX,
        double MinY,
        double MaxX,
        double MaxY);

    private sealed class SpineFrameSequenceBoundsBuilder
    {
        private double _minX = double.PositiveInfinity;
        private double _minY = double.PositiveInfinity;
        private double _maxX = double.NegativeInfinity;
        private double _maxY = double.NegativeInfinity;

        public void Include(double x, double y)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y))
            {
                return;
            }

            _minX = Math.Min(_minX, x);
            _minY = Math.Min(_minY, y);
            _maxX = Math.Max(_maxX, x);
            _maxY = Math.Max(_maxY, y);
        }

        public SpineFrameSequenceBounds Build() =>
            double.IsFinite(_minX) &&
            double.IsFinite(_minY) &&
            double.IsFinite(_maxX) &&
            double.IsFinite(_maxY)
                ? new SpineFrameSequenceBounds(_minX, _minY, _maxX, _maxY)
                : new SpineFrameSequenceBounds(0d, 0d, MinimumContentExtent, MinimumContentExtent);
    }
}
