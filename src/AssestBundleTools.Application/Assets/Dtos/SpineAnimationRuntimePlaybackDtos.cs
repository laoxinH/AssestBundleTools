using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Application.Assets.Dtos;

/// <summary>
/// 表示创建 Spine runtime 实时播放 session 所需的中立载荷。
/// </summary>
public sealed record SpineAnimationRuntimePlaybackSourceDto(
    string GroupKey,
    SpineAssetGroupPartDto SkeletonAsset,
    string? SkeletonJson,
    SpineAnimationRuntimeDto Runtime,
    IReadOnlyList<SpineAtlasPageDto> AtlasPages,
    IReadOnlyList<string> AtlasTexts,
    byte[]? SkeletonBinaryContent = null,
    SpineAnimationSampledPlaybackSourceDto? SampledFallback = null,
    AssetPreviewTextSourceDto? SkeletonJsonSource = null,
    IReadOnlyList<AssetPreviewTextSourceDto>? AtlasTextSources = null);

public sealed record SpineAnimationSampledPlaybackSourceDto(
    IReadOnlyList<SpineAnimationSkinPreviewDto> SkinPreviews,
    SpineAnimationFrameDto? SetupFrame,
    IReadOnlyList<SpineAnimationClipDto> Animations);

public sealed record SpineAnimationRuntimePlaybackSessionResultDto(
    bool IsHandled,
    bool IsCancelled,
    ISpineAnimationRuntimePlaybackSession? Session,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationRuntimePlaybackSessionResultDto NotHandled { get; } =
        new(false, false, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimePlaybackSessionResultDto Cancelled { get; } =
        new(false, true, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimePlaybackSessionResultDto Handled(
        ISpineAnimationRuntimePlaybackSession session,
        IReadOnlyList<SpineAssetGroupDiagnosticDto>? diagnostics = null) =>
        new(true, false, session, diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
}

public sealed record SpineAnimationRuntimePlaybackFrameRequestDto(
    string? AnimationName,
    string? SkinName,
    double PlaybackTime,
    bool ShowSetupPose);

public sealed record SpineAnimationRuntimeFixedFrameSamplingRequestDto(
    SpineAnimationRuntimePlaybackSourceDto Source,
    string? AnimationName,
    string? SkinName,
    double FramesPerSecond,
    double StartTime = 0d,
    double? EndTime = null,
    bool IncludeFinalFrame = true);

public sealed record SpineAnimationRuntimeFixedFrameSamplingResultDto(
    bool IsHandled,
    bool IsCancelled,
    double FramesPerSecond,
    double Duration,
    IReadOnlyList<double> SampleTimes,
    IReadOnlyList<SpineAnimationFrameDto> Frames,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationRuntimeFixedFrameSamplingResultDto NotHandled { get; } =
        new(
            false,
            false,
            0d,
            0d,
            Array.Empty<double>(),
            Array.Empty<SpineAnimationFrameDto>(),
            Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimeFixedFrameSamplingResultDto Cancelled { get; } =
        new(
            false,
            true,
            0d,
            0d,
            Array.Empty<double>(),
            Array.Empty<SpineAnimationFrameDto>(),
            Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimeFixedFrameSamplingResultDto Handled(
        double framesPerSecond,
        double duration,
        IReadOnlyList<double> sampleTimes,
        IReadOnlyList<SpineAnimationFrameDto> frames,
        IReadOnlyList<SpineAssetGroupDiagnosticDto>? diagnostics = null) =>
        new(
            true,
            false,
            framesPerSecond,
            duration,
            sampleTimes,
            frames,
            diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
}

public sealed record SpineAnimationRuntimeFrameSequenceExportPlanRequestDto(
    SpineAnimationRuntimePlaybackSourceDto Source,
    string? AnimationName,
    string? SkinName,
    double FramesPerSecond,
    double StartTime = 0d,
    double? EndTime = null,
    bool IncludeFinalFrame = true,
    string? FileNamePrefix = null,
    int? CanvasWidth = null,
    int? CanvasHeight = null,
    int PaddingPixels = 16);

public sealed record SpineAnimationRuntimeFrameSequenceExportPlanResultDto(
    bool IsHandled,
    bool IsCancelled,
    double FramesPerSecond,
    double Duration,
    SpineAnimationRuntimeFrameSequenceExportLayoutDto? Layout,
    IReadOnlyList<SpineAnimationRuntimeFrameSequenceExportFrameDto> Frames,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationRuntimeFrameSequenceExportPlanResultDto NotHandled { get; } =
        new(
            false,
            false,
            0d,
            0d,
            null,
            Array.Empty<SpineAnimationRuntimeFrameSequenceExportFrameDto>(),
            Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimeFrameSequenceExportPlanResultDto Cancelled { get; } =
        new(
            false,
            true,
            0d,
            0d,
            null,
            Array.Empty<SpineAnimationRuntimeFrameSequenceExportFrameDto>(),
            Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimeFrameSequenceExportPlanResultDto Handled(
        double framesPerSecond,
        double duration,
        SpineAnimationRuntimeFrameSequenceExportLayoutDto layout,
        IReadOnlyList<SpineAnimationRuntimeFrameSequenceExportFrameDto> frames,
        IReadOnlyList<SpineAssetGroupDiagnosticDto>? diagnostics = null) =>
        new(
            true,
            false,
            framesPerSecond,
            duration,
            layout,
            frames,
            diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
}

public sealed record SpineAnimationRuntimeFrameSequenceExportLayoutDto(
    int CanvasWidth,
    int CanvasHeight,
    int PaddingPixels,
    double ContentMinX,
    double ContentMinY,
    double ContentMaxX,
    double ContentMaxY,
    double WorldToPixelScale,
    double WorldToPixelOffsetX,
    double WorldToPixelOffsetY);

public sealed record SpineAnimationRuntimeFrameSequenceExportFrameDto(
    int Index,
    double Time,
    string FileName,
    SpineAnimationFrameDto Frame);

public sealed record SpineAnimationRuntimePlaybackFrameResultDto(
    bool IsHandled,
    bool IsCancelled,
    SpineAnimationFrameDto? Frame,
    double PlaybackTime,
    double Duration,
    SpineAnimationRuntimePlaybackFrameDiagnosticsDto? RuntimeDiagnostics,
    IReadOnlyList<SpineAssetGroupDiagnosticDto> Diagnostics)
{
    public static SpineAnimationRuntimePlaybackFrameResultDto NotHandled { get; } =
        new(false, false, null, 0d, 0d, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimePlaybackFrameResultDto Cancelled { get; } =
        new(false, true, null, 0d, 0d, null, Array.Empty<SpineAssetGroupDiagnosticDto>());

    public static SpineAnimationRuntimePlaybackFrameResultDto Handled(
        SpineAnimationFrameDto frame,
        double playbackTime,
        double duration,
        SpineAnimationRuntimePlaybackFrameDiagnosticsDto? runtimeDiagnostics = null,
        IReadOnlyList<SpineAssetGroupDiagnosticDto>? diagnostics = null) =>
        new(
            true,
            false,
            frame,
            playbackTime,
            duration,
            runtimeDiagnostics,
            diagnostics ?? Array.Empty<SpineAssetGroupDiagnosticDto>());
}

public sealed record SpineAnimationRuntimePlaybackFrameDiagnosticsDto(
    string AdapterKey,
    bool IsSetupPose,
    double TotalMilliseconds,
    double SkeletonCreateMilliseconds,
    double SkeletonResetMilliseconds,
    bool IsSkeletonReused,
    double AnimationApplyMilliseconds,
    double WorldTransformMilliseconds,
    double DrawCommandBuildMilliseconds,
    int DrawCommandCount,
    int MeshVertexCount,
    int MeshTriangleCount);
