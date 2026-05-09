using System.Collections.ObjectModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewSpineAnimationViewModel : ObservableObject
{
    private SpineAnimationPreviewDto? _preview;
    private IReadOnlyDictionary<string, byte[]> _texturePageContents =
        new Dictionary<string, byte[]>(StringComparer.Ordinal);
    private string? _selectedAnimationName;
    private string? _selectedSkinName;
    private double _playbackTime;
    private double _playbackSpeed = 1d;
    private bool _isPlaying = true;
    private int _cachedFrameIndex;
    private SpineAnimationFrameDto? _currentFrame;
    private bool _isShowingSetupPose;

    public ObservableCollection<string> AnimationNames { get; } = [];

    public ObservableCollection<string> SkinNames { get; } = [];

    public ObservableCollection<double> PlaybackSpeedOptions { get; } = [0.25d, 0.5d, 1d, 1.5d, 2d, 3d];

    public SpineAnimationPreviewDto? Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public IReadOnlyDictionary<string, byte[]> TexturePageContents
    {
        get => _texturePageContents;
        private set => SetProperty(ref _texturePageContents, value);
    }

    public string? SelectedAnimationName
    {
        get => _selectedAnimationName;
        set
        {
            if (SetProperty(ref _selectedAnimationName, value))
            {
                _cachedFrameIndex = 0;
                _isShowingSetupPose = false;
                SetPlaybackTime(0d);
                OnPropertyChanged(nameof(SelectedAnimation));
                OnPropertyChanged(nameof(Duration));
                SyncCurrentFrame();
            }
        }
    }

    public string? SelectedSkinName
    {
        get => _selectedSkinName;
        set
        {
            if (SetProperty(ref _selectedSkinName, value))
            {
                _cachedFrameIndex = 0;
                _isShowingSetupPose = false;
                RefreshAnimationNamesForActiveSkin();
                if (!AnimationNames.Contains(SelectedAnimationName ?? string.Empty))
                {
                    SelectedAnimationName = AnimationNames.FirstOrDefault();
                }
                else
                {
                    SetPlaybackTime(PlaybackTime);
                    OnPropertyChanged(nameof(SelectedAnimation));
                    OnPropertyChanged(nameof(Duration));
                    SyncCurrentFrame();
                }
            }
        }
    }

    public SpineAnimationClipDto? SelectedAnimation =>
        ActiveAnimations.FirstOrDefault(animation =>
            string.Equals(animation.Name, SelectedAnimationName, StringComparison.Ordinal));

    public double Duration => Math.Max(0d, SelectedAnimation?.Duration ?? 0d);

    public double PlaybackTime
    {
        get => _playbackTime;
        set
        {
            _isShowingSetupPose = false;
            SetPlaybackTime(value);
            SyncCurrentFrame();
        }
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, Math.Clamp(value, 0.1d, 3d));
    }

    public SpineAnimationFrameDto? CurrentFrame
    {
        get => _currentFrame;
        private set => SetProperty(ref _currentFrame, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseGlyph));
            }
        }
    }

    public bool HasSpineAnimation => Preview is not null &&
                                     ActiveAnimations.Any(static animation => animation.SampledFrames.Count > 0) &&
                                     TexturePageContents.Count > 0;

    public Visibility Visibility => HasSpineAnimation ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSkinSelectionEnabled => SkinNames.Count > 1;

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    internal void Apply(
        SpineAnimationPreviewDto preview,
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> texturePreviews)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(texturePreviews);

        Clear();
        Preview = preview;
        TexturePageContents = texturePreviews
            .Where(static pair => pair.Value.BinaryContent is { Length: > 0 })
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.BinaryContent!,
                StringComparer.Ordinal);
        foreach (var skinName in ResolveSkinNames(preview))
        {
            SkinNames.Add(skinName);
        }

        if (SkinNames.Count == 0)
        {
            SkinNames.Add("default");
        }

        SelectedSkinName = ResolveInitialSkinName(preview) ?? SkinNames.FirstOrDefault();
        SelectedAnimationName ??= AnimationNames.FirstOrDefault();
        _isShowingSetupPose = false;
        IsPlaying = true;
        NotifyStateChanged();
    }

    internal void Clear()
    {
        AnimationNames.Clear();
        SkinNames.Clear();
        Preview = null;
        TexturePageContents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        SelectedAnimationName = null;
        SelectedSkinName = null;
        _cachedFrameIndex = 0;
        _isShowingSetupPose = false;
        SetPlaybackTime(0d);
        CurrentFrame = null;
        IsPlaying = false;
        NotifyStateChanged();
    }

    public void TogglePlayback()
    {
        if (!HasSpineAnimation)
        {
            return;
        }

        if (!IsPlaying)
        {
            _isShowingSetupPose = false;
            SyncCurrentFrame();
        }

        IsPlaying = !IsPlaying;
    }

    public void Restart()
    {
        _isShowingSetupPose = false;
        PlaybackTime = 0d;
    }

    public void ShowSetupPose()
    {
        _isShowingSetupPose = true;
        IsPlaying = false;
        SetPlaybackTime(0d);
        SyncCurrentFrame();
    }

    public void StepPreviousFrame()
    {
        StepFrame(-1);
    }

    public void StepNextFrame()
    {
        StepFrame(1);
    }

    public void Advance(TimeSpan elapsed)
    {
        if (!IsPlaying || Duration <= 0d)
        {
            return;
        }

        var nextTime = PlaybackTime + Math.Max(0d, elapsed.TotalSeconds) * PlaybackSpeed;
        PlaybackTime = nextTime % Duration;
    }

    private void StepFrame(int direction)
    {
        var frames = SelectedAnimation?.SampledFrames;
        if (frames is not { Count: > 0 })
        {
            return;
        }

        IsPlaying = false;
        _isShowingSetupPose = false;
        var currentIndex = ResolveFrameIndex(frames, PlaybackTime);
        var nextIndex = (currentIndex + direction + frames.Count) % frames.Count;
        _cachedFrameIndex = nextIndex;
        SetPlaybackTime(frames[nextIndex].Time);
        CurrentFrame = frames[nextIndex];
    }

    private void SetPlaybackTime(double value)
    {
        var maxTime = Duration > 0d ? Duration : double.MaxValue;
        SetProperty(ref _playbackTime, Math.Clamp(value, 0d, maxTime));
    }

    private void SyncCurrentFrame()
    {
        var frames = SelectedAnimation?.SampledFrames;
        if (_isShowingSetupPose &&
            ActiveSetupFrame is not null)
        {
            CurrentFrame = ActiveSetupFrame;
            return;
        }

        if (frames is not { Count: > 0 })
        {
            CurrentFrame = null;
            return;
        }

        _cachedFrameIndex = ResolveFrameIndex(frames, PlaybackTime);
        CurrentFrame = frames[_cachedFrameIndex];
    }

    private int ResolveFrameIndex(IReadOnlyList<SpineAnimationFrameDto> frames, double time)
    {
        if (frames.Count <= 1)
        {
            return 0;
        }

        if (IsCachedFrameValid(frames, time))
        {
            return _cachedFrameIndex;
        }

        var low = 0;
        var high = frames.Count - 1;
        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            if (frames[mid].Time <= time)
            {
                low = mid + 1;
                continue;
            }

            high = mid - 1;
        }

        return Math.Clamp(high, 0, frames.Count - 1);
    }

    private bool IsCachedFrameValid(IReadOnlyList<SpineAnimationFrameDto> frames, double time)
    {
        if (_cachedFrameIndex < 0 || _cachedFrameIndex >= frames.Count)
        {
            return false;
        }

        var currentTime = frames[_cachedFrameIndex].Time;
        var nextTime = _cachedFrameIndex + 1 < frames.Count
            ? frames[_cachedFrameIndex + 1].Time
            : double.PositiveInfinity;
        return time >= currentTime && time < nextTime;
    }

    private SpineAnimationSkinPreviewDto? SelectedSkinPreview =>
        Preview?.SkinPreviews.FirstOrDefault(skin =>
            string.Equals(skin.Name, SelectedSkinName, StringComparison.Ordinal));

    private IReadOnlyList<SpineAnimationClipDto> ActiveAnimations =>
        SelectedSkinPreview?.Animations ??
        Preview?.Animations ??
        Array.Empty<SpineAnimationClipDto>();

    private SpineAnimationFrameDto? ActiveSetupFrame =>
        SelectedSkinPreview?.SetupFrame ?? Preview?.SetupFrame;

    private void RefreshAnimationNamesForActiveSkin()
    {
        AnimationNames.Clear();
        foreach (var animation in ActiveAnimations.Where(static animation => animation.SampledFrames.Count > 0))
        {
            AnimationNames.Add(animation.Name);
        }

        OnPropertyChanged(nameof(HasSpineAnimation));
        OnPropertyChanged(nameof(Visibility));
    }

    private static IReadOnlyList<string> ResolveSkinNames(SpineAnimationPreviewDto preview)
    {
        var names = preview.SkinPreviews.Count > 0
            ? preview.SkinPreviews.Select(static skin => skin.Name)
            : preview.SkinNames;
        return names
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ResolveInitialSkinName(SpineAnimationPreviewDto preview)
    {
        var skinPreview = preview.SkinPreviews.FirstOrDefault(static skin =>
            string.Equals(skin.Name, "default", StringComparison.OrdinalIgnoreCase)) ??
            preview.SkinPreviews.FirstOrDefault(static skin =>
                skin.Animations.Any(static animation =>
                    animation.SampledFrames.Any(static frame => frame.DrawCommands.Count > 0))) ??
            preview.SkinPreviews.FirstOrDefault();
        return skinPreview?.Name ?? preview.SkinNames.FirstOrDefault();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasSpineAnimation));
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(IsSkinSelectionEnabled));
        OnPropertyChanged(nameof(SelectedAnimation));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(CurrentFrame));
    }
}
