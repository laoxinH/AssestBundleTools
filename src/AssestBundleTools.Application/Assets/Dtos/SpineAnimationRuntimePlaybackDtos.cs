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
    byte[]? SkeletonBinaryContent = null);

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
