using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

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
        var texturePages = new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal)
        {
            ["page.png"] = new("page.png", [1, 2, 3])
        };

        backend.LoadTexturePages(texturePages);
        backend.LoadTexturePages(texturePages);
        backend.SetCurrentFrame(frame);
        backend.SwitchToFallback();

        Assert.Equal([true, false], primaryStates);
        Assert.Equal(1, primary.LoadTexturePagesCount);
        Assert.Same(texturePages, fallback.TexturePages);
        Assert.Same(frame, fallback.CurrentFrame);
        Assert.Equal(SpineAnimationPreviewViewport.Default, fallback.Viewport);
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
    public void SwitchToFallback_ShouldReplayViewport()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, _ => { });
        var viewport = new SpineAnimationPreviewViewport(1.5d, 12d, -4d);

        backend.SetViewport(viewport);
        backend.SwitchToFallback();

        Assert.Equal(viewport, primary.Viewport);
        Assert.Equal(viewport, fallback.Viewport);
    }

    [Fact]
    public void SwitchToFallback_ShouldReplayBackgroundMode()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, _ => { });

        backend.SetBackgroundMode(InspectorPreviewImageBackgroundMode.Checkerboard);
        backend.SwitchToFallback();

        Assert.Equal(InspectorPreviewImageBackgroundMode.Checkerboard, primary.BackgroundMode);
        Assert.Equal(InspectorPreviewImageBackgroundMode.Checkerboard, fallback.BackgroundMode);
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

    [Fact]
    public void ReleaseResources_ShouldDropRememberedTexturePagesAndFrame()
    {
        var primary = new RecordingBackend();
        var fallback = new RecordingBackend();
        var backend = new SpineAnimationFallbackRenderBackend(primary, fallback, _ => { });
        var frame = new SpineAnimationFrameDto(0d, []);
        var texturePages = new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal)
        {
            ["page.png"] = new("page.png", [1, 2, 3])
        };

        backend.LoadTexturePages(texturePages);
        backend.SetCurrentFrame(frame);
        backend.ReleaseResources();
        backend.SwitchToFallback();

        Assert.True(fallback.TexturePagesCleared);
        Assert.Null(fallback.CurrentFrame);
    }

    private sealed class RecordingBackend : ISpineAnimationPreviewRenderBackend
    {
        public SpineAnimationFrameDto? CurrentFrame { get; private set; }

        public IReadOnlyList<SpineAnimationFrameDto>? PrewarmedFrames { get; private set; }

        public IReadOnlyDictionary<string, SpineAnimationTexturePageContent>? TexturePages { get; private set; }

        public SpineAnimationPreviewViewport Viewport { get; private set; } =
            SpineAnimationPreviewViewport.Default;

        public InspectorPreviewImageBackgroundMode BackgroundMode { get; private set; } =
            InspectorPreviewImageBackgroundMode.Solid;

        public bool TexturePagesCleared { get; private set; }

        public int LoadTexturePagesCount { get; private set; }

        public int ResetFrameCacheCount { get; private set; }

        public int InvalidateCount { get; private set; }

        public int ReleaseResourcesCount { get; private set; }

        public void SetCurrentFrame(SpineAnimationFrameDto? frame) =>
            CurrentFrame = frame;

        public void SetViewport(SpineAnimationPreviewViewport viewport) =>
            Viewport = viewport.Normalize();

        public void SetBackgroundMode(InspectorPreviewImageBackgroundMode backgroundMode) =>
            BackgroundMode = backgroundMode;

        public void PrewarmFrames(IReadOnlyList<SpineAnimationFrameDto> frames) =>
            PrewarmedFrames = frames;

        public void LoadTexturePages(IReadOnlyDictionary<string, SpineAnimationTexturePageContent> texturePageContents)
        {
            LoadTexturePagesCount++;
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
