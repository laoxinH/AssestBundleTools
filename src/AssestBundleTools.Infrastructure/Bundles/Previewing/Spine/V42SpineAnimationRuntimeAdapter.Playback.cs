using System.Diagnostics;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using Runtime = AssestBundleTools.Infrastructure.ThirdParty.Spine.V42;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal sealed partial class V42SpineAnimationRuntimeAdapter
{
    public SpineAnimationRuntimePlaybackSessionResultDto TryCreatePlaybackSession(
        SpineAnimationRuntimePlaybackSourceDto source,
        IAssetPreviewTextSourceReader textSourceReader,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(textSourceReader);
        if (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
        }

        if (!SpineRuntimeAtlasInputGuard.CanAttemptRuntimeParse(source.AtlasTexts, source.AtlasTextSources) ||
            (string.IsNullOrWhiteSpace(source.SkeletonJson) &&
             source.SkeletonJsonSource is null &&
             source.SkeletonBinaryContent is not { Length: > 0 }))
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.NotHandled;
        }

        try
        {
            var skeletonData = ReadSkeletonData(source, textSourceReader);
            if (cancellationToken.IsCancellationRequested)
            {
                return SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
            }

            if (skeletonData is null ||
                !IsV42Skeleton(skeletonData.Version))
            {
                return SpineAnimationRuntimePlaybackSessionResultDto.NotHandled;
            }

            return SpineAnimationRuntimePlaybackSessionResultDto.Handled(
                new PlaybackSession(skeletonData, AdapterKey));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.Cancelled;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return SpineAnimationRuntimePlaybackSessionResultDto.NotHandled;
        }
    }

    private static Runtime.SkeletonData? ReadSkeletonData(
        SpineAnimationRuntimePlaybackSourceDto source,
        IAssetPreviewTextSourceReader textSourceReader)
    {
        var atlases = CreateAtlases(source.AtlasTexts, source.AtlasTextSources, textSourceReader);
        if (source.SkeletonBinaryContent is { Length: > 0 } skeletonBinaryContent)
        {
            var binaryLoader = new Runtime.SkeletonBinary(atlases);
            using var stream = new MemoryStream(skeletonBinaryContent);
            return binaryLoader.ReadSkeletonData(stream);
        }

        if (string.IsNullOrWhiteSpace(source.SkeletonJson) &&
            source.SkeletonJsonSource is null)
        {
            throw new InvalidDataException("Skeleton JSON or binary content is required.");
        }

        var jsonLoader = new Runtime.SkeletonJson(atlases);
        using var reader = OpenSkeletonJsonReader(
            source.SkeletonJson,
            source.SkeletonJsonSource,
            textSourceReader);
        return jsonLoader.ReadSkeletonData(reader);
    }

    private sealed class PlaybackSession : ISpineAnimationRuntimePlaybackSession
    {
        private readonly string _adapterKey;
        private readonly Runtime.SkeletonData _skeletonData;
        private readonly Dictionary<string, SpineAnimationAttachmentDto> _attachmentMap =
            new(StringComparer.Ordinal);
        private readonly object _syncRoot = new();
        private Runtime.Skeleton? _skeleton;
        private Runtime.Skin? _skeletonSkin;
        private bool _isDisposed;

        public PlaybackSession(Runtime.SkeletonData skeletonData, string adapterKey)
        {
            _skeletonData = skeletonData;
            _adapterKey = adapterKey;
        }

        public Task<SpineAnimationRuntimePlaybackFrameResultDto> GetFrameAsync(
            SpineAnimationRuntimePlaybackFrameRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(SpineAnimationRuntimePlaybackFrameResultDto.Cancelled);
            }

            return Task.Run(() => BuildFrame(request, cancellationToken));
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                _isDisposed = true;
                _attachmentMap.Clear();
                _skeleton = null;
                _skeletonSkin = null;
            }
        }

        private SpineAnimationRuntimePlaybackFrameResultDto BuildFrame(
            SpineAnimationRuntimePlaybackFrameRequestDto request,
            CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
            }

            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                lock (_syncRoot)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
                    }

                    if (_isDisposed)
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.NotHandled;
                    }

                    var skin = ResolveSkin(request.SkinName);
                    var preparedSkeleton = PrepareSkeleton(skin);
                    var skeleton = preparedSkeleton.Skeleton;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
                    }

                    if (request.ShowSetupPose)
                    {
                        var setupWorldTransformStopwatch = Stopwatch.StartNew();
                        skeleton.UpdateWorldTransform(Runtime.Skeleton.Physics.None);
                        setupWorldTransformStopwatch.Stop();
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
                        }

                        var setupDrawCommandStopwatch = Stopwatch.StartNew();
                        var setupDrawCommands = BuildDrawCommands(skeleton, _attachmentMap);
                        setupDrawCommandStopwatch.Stop();
                        var setupFrame = new SpineAnimationFrameDto(0d, setupDrawCommands);
                        return SpineAnimationRuntimePlaybackFrameResultDto.Handled(
                            setupFrame,
                            playbackTime: 0d,
                            duration: 0d,
                            runtimeDiagnostics: CreateDiagnostics(
                                isSetupPose: true,
                                totalStopwatch: totalStopwatch,
                                skeletonCreateElapsed: preparedSkeleton.CreateElapsed,
                                skeletonResetElapsed: preparedSkeleton.ResetElapsed,
                                isSkeletonReused: preparedSkeleton.IsReused,
                                animationApplyElapsed: TimeSpan.Zero,
                                worldTransformElapsed: setupWorldTransformStopwatch.Elapsed,
                                drawCommandBuildElapsed: setupDrawCommandStopwatch.Elapsed,
                                drawCommands: setupDrawCommands));
                    }

                    if (string.IsNullOrWhiteSpace(request.AnimationName))
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.NotHandled;
                    }

                    var animation = _skeletonData.FindAnimation(request.AnimationName);
                    if (animation is null)
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.NotHandled;
                    }

                    var playbackTime = NormalizePlaybackTime(request.PlaybackTime, animation.Duration);
                    var animationApplyStopwatch = Stopwatch.StartNew();
                    animation.Apply(
                        skeleton,
                        lastTime: -1f,
                        (float)playbackTime,
                        loop: false,
                        events: null,
                        alpha: 1f,
                        blend: Runtime.MixBlend.Setup,
                        direction: Runtime.MixDirection.In);
                    animationApplyStopwatch.Stop();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
                    }

                    var worldTransformStopwatch = Stopwatch.StartNew();
                    skeleton.UpdateWorldTransform(Runtime.Skeleton.Physics.None);
                    worldTransformStopwatch.Stop();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
                    }

                    var drawCommandStopwatch = Stopwatch.StartNew();
                    var drawCommands = BuildDrawCommands(skeleton, _attachmentMap);
                    drawCommandStopwatch.Stop();
                    var frame = new SpineAnimationFrameDto(playbackTime, drawCommands);
                    return SpineAnimationRuntimePlaybackFrameResultDto.Handled(
                        frame,
                        playbackTime,
                        animation.Duration,
                        CreateDiagnostics(
                            isSetupPose: false,
                            totalStopwatch: totalStopwatch,
                            skeletonCreateElapsed: preparedSkeleton.CreateElapsed,
                            skeletonResetElapsed: preparedSkeleton.ResetElapsed,
                            isSkeletonReused: preparedSkeleton.IsReused,
                            animationApplyElapsed: animationApplyStopwatch.Elapsed,
                            worldTransformElapsed: worldTransformStopwatch.Elapsed,
                            drawCommandBuildElapsed: drawCommandStopwatch.Elapsed,
                            drawCommands: drawCommands));
                }
            }
            catch (OperationCanceledException)
            {
                return SpineAnimationRuntimePlaybackFrameResultDto.Cancelled;
            }
            catch (Exception)
            {
                return cancellationToken.IsCancellationRequested
                    ? SpineAnimationRuntimePlaybackFrameResultDto.Cancelled
                    : SpineAnimationRuntimePlaybackFrameResultDto.NotHandled;
            }
        }

        private SpineAnimationRuntimePlaybackFrameDiagnosticsDto CreateDiagnostics(
            bool isSetupPose,
            Stopwatch totalStopwatch,
            TimeSpan skeletonCreateElapsed,
            TimeSpan skeletonResetElapsed,
            bool isSkeletonReused,
            TimeSpan animationApplyElapsed,
            TimeSpan worldTransformElapsed,
            TimeSpan drawCommandBuildElapsed,
            IReadOnlyList<SpineAnimationDrawCommandDto> drawCommands)
        {
            totalStopwatch.Stop();
            var meshVertexCount = 0;
            var meshTriangleCount = 0;
            foreach (var command in drawCommands)
            {
                meshVertexCount += command.MeshVertices.Count;
                meshTriangleCount += command.MeshTriangles.Count;
            }

            return new SpineAnimationRuntimePlaybackFrameDiagnosticsDto(
                _adapterKey,
                isSetupPose,
                totalStopwatch.Elapsed.TotalMilliseconds,
                skeletonCreateElapsed.TotalMilliseconds,
                skeletonResetElapsed.TotalMilliseconds,
                isSkeletonReused,
                animationApplyElapsed.TotalMilliseconds,
                worldTransformElapsed.TotalMilliseconds,
                drawCommandBuildElapsed.TotalMilliseconds,
                drawCommands.Count,
                meshVertexCount,
                meshTriangleCount);
        }

        private Runtime.Skin? ResolveSkin(string? skinName)
        {
            if (string.IsNullOrWhiteSpace(skinName))
            {
                return _skeletonData.DefaultSkin;
            }

            return _skeletonData.FindSkin(skinName) ?? _skeletonData.DefaultSkin;
        }

        private PreparedSkeleton PrepareSkeleton(Runtime.Skin? skin)
        {
            if (_skeleton is null || !ReferenceEquals(_skeletonSkin, skin))
            {
                var createStopwatch = Stopwatch.StartNew();
                _skeleton = CreateSkeleton(_skeletonData, skin);
                _skeletonSkin = skin;
                createStopwatch.Stop();
                return new PreparedSkeleton(_skeleton, createStopwatch.Elapsed, TimeSpan.Zero, IsReused: false);
            }

            var resetStopwatch = Stopwatch.StartNew();
            _skeleton.SetToSetupPose();
            resetStopwatch.Stop();
            return new PreparedSkeleton(_skeleton, TimeSpan.Zero, resetStopwatch.Elapsed, IsReused: true);
        }

        private readonly record struct PreparedSkeleton(
            Runtime.Skeleton Skeleton,
            TimeSpan CreateElapsed,
            TimeSpan ResetElapsed,
            bool IsReused);
    }

    private static double NormalizePlaybackTime(double playbackTime, float duration)
    {
        if (duration <= 0f ||
            double.IsNaN(playbackTime) ||
            double.IsInfinity(playbackTime))
        {
            return 0d;
        }

        if (playbackTime < 0d)
        {
            var wrapped = playbackTime % duration;
            return wrapped < 0d ? wrapped + duration : wrapped;
        }

        if (playbackTime <= duration)
        {
            return playbackTime;
        }

        return playbackTime % duration;
    }
}
