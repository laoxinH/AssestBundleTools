using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

namespace AssestBundleTools.Presentation.Tests.Views;

public sealed class SpineAnimationFallbackRenderBackendTests
{
    [Fact]
    public void SwitchToFallback_ShouldReplayCurrentFrameAndTexturePages()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var primaryStates = new List<bool>();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, primaryStates.Add);
        var frame = new SpineAnimationFrameDto(0d, []);
        var texturePages = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["page.png"] = [1, 2, 3]
        };

        backend.LoadTexturePages(texturePages);
        backend.SetCurrentFrame(frame);
        backend.SwitchToFallback();

        Assert.Equal([true, false], primaryStates);
        Assert.Same(texturePages, fallback.TexturePages);
        Assert.Same(frame, fallback.CurrentFrame);
        Assert.Equal(1, fallback.ResetFrameCacheCount);
        Assert.Equal(1, fallback.InvalidateCount);
    }

    [Fact]
    public void CallsAfterSwitch_ShouldRouteToFallback()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, _ => { });
        var frame = new SpineAnimationFrameDto(0d, []);

        backend.SwitchToFallback();
        backend.SetCurrentFrame(frame);
        backend.ClearTexturePages();
        backend.Invalidate();

        Assert.Null(primary.CurrentFrame);
        Assert.Same(frame, fallback.CurrentFrame);
        Assert.True(fallback.TexturePagesCleared);
        Assert.Equal(2, fallback.InvalidateCount);
    }

    [Fact]
    public void PrewarmFrames_ShouldRouteToActiveBackend()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, _ => { });
        var frames = new[] { new SpineAnimationFrameDto(0d, []) };

        backend.PrewarmFrames(frames);
        backend.SwitchToFallback();
        backend.PrewarmFrames(frames);

        Assert.Same(frames, primary.PrewarmedFrames);
        Assert.Same(frames, fallback.PrewarmedFrames);
    }

    [Fact]
    public void ReleaseResources_ShouldReleasePrimaryAndFallback()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, _ => { });

        backend.ReleaseResources();

        Assert.Equal(1, primary.ReleaseResourcesCount);
        Assert.Equal(1, fallback.ReleaseResourcesCount);
    }

    private sealed class RecordingBackend : ISpineAnimationPreviewRenderBackend
    {
        public SpineAnimationFrameDto? CurrentFrame { get; private set; }

        public IReadOnlyList<SpineAnimationFrameDto>? PrewarmedFrames { get; private set; }

        public IReadOnlyDictionary<string, byte[]>? TexturePages { get; private set; }

        public bool TexturePagesCleared { get; private set; }

        public int ResetFrameCacheCount { get; private set; }

        public int InvalidateCount { get; private set; }

        public int ReleaseResourcesCount { get; private set; }

        public void SetCurrentFrame(SpineAnimationFrameDto? frame) =>
            CurrentFrame = frame;

        public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames) =>
            PrewarmedFrames = frames;

        public void LoadTexturePages(IReadOnlyDictionary<string, byte[]> texturePageContents)
        {
            TexturePages = texturePageContents;
            TexturePagesCleared = false;
        }

        public void ClearTexturePages()
        {
            TexturePages = null;
            TexturePagesCleared = true;
        }

        public void ResetFrameCache() =>
            ResetFrameCacheCount++;

        public void ReleaseResources() =>
            ReleaseResourcesCount++;

        public void Invalidate() =>
            InvalidateCount++;
    }
}
