using System.Collections.ObjectModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel : ObservableObject
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
            var update = ApplySelectedAnimationName(value, resetPlayback: true);
            if (update.AnimationNameChanged)
            {
                NotifyAnimationSelectionChanged(update);
            }
        }
    }

    public string? SelectedSkinName
    {
        get => _selectedSkinName;
        set
        {
            if (EqualityComparer<string?>.Default.Equals(_selectedSkinName, value))
            {
                return;
            }

            _selectedSkinName = value;
            _cachedFrameIndex = 0;
            _isShowingSetupPose = false;
            RefreshAnimationNamesForActiveSkin();
            var update = AnimationNames.Contains(SelectedAnimationName ?? string.Empty)
                ? ApplyCurrentAnimationForActiveSkin()
                : ApplySelectedAnimationName(AnimationNames.FirstOrDefault(), resetPlayback: true);

            OnPropertyChanged(nameof(SelectedSkinName));
            if (update.AnimationNameChanged)
            {
                OnPropertyChanged(nameof(SelectedAnimationName));
            }

            OnPropertyChanged(nameof(SelectedAnimation));
            OnPropertyChanged(nameof(Duration));
            if (update.PlaybackTimeChanged)
            {
                OnPropertyChanged(nameof(PlaybackTime));
            }

            if (update.CurrentFrameChanged)
            {
                OnPropertyChanged(nameof(CurrentFrame));
            }
        }
    }

    private AnimationSelectionUpdate ApplySelectedAnimationName(string? value, bool resetPlayback)
    {
        if (EqualityComparer<string?>.Default.Equals(_selectedAnimationName, value))
        {
            return new AnimationSelectionUpdate(false, false, false);
        }

        _selectedAnimationName = value;
        _cachedFrameIndex = 0;
        _isShowingSetupPose = false;
        var playbackTimeChanged = SetPlaybackTimeCore(resetPlayback ? 0d : PlaybackTime, notify: false);
        var currentFrameChanged = SyncCurrentFrame(notify: false);
        return new AnimationSelectionUpdate(true, playbackTimeChanged, currentFrameChanged);
    }

    private AnimationSelectionUpdate ApplyCurrentAnimationForActiveSkin()
    {
        var playbackTimeChanged = SetPlaybackTimeCore(PlaybackTime, notify: false);
        var currentFrameChanged = SyncCurrentFrame(notify: false);
        return new AnimationSelectionUpdate(false, playbackTimeChanged, currentFrameChanged);
    }

    private void NotifyAnimationSelectionChanged(AnimationSelectionUpdate update)
    {
        OnPropertyChanged(nameof(SelectedAnimationName));
        OnPropertyChanged(nameof(SelectedAnimation));
        OnPropertyChanged(nameof(Duration));
        if (update.PlaybackTimeChanged)
        {
            OnPropertyChanged(nameof(PlaybackTime));
        }

        if (update.CurrentFrameChanged)
        {
            OnPropertyChanged(nameof(CurrentFrame));
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
            var playbackTimeChanged = SetPlaybackTimeCore(value, notify: false);
            var currentFrameChanged = HasRuntimePlayback ? false : SyncCurrentFrame(notify: false);
            if (playbackTimeChanged)
            {
                OnPropertyChanged(nameof(PlaybackTime));
            }

            if (currentFrameChanged)
            {
                OnPropertyChanged(nameof(CurrentFrame));
            }
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
        private set => SetCurrentFrame(value, notify: true);
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
                                     (HasRuntimePlayback ||
                                      ActiveAnimations.Any(static animation => animation.SampledFrames.Count > 0)) &&
                                     TexturePageContents.Count > 0;

    public bool HasRuntimePlayback => Preview?.PlaybackSource is not null;

    public bool IsShowingSetupPose => _isShowingSetupPose;

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
        SetPlaybackTimeCore(0d, notify: true);
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
        SetPlaybackTimeCore(0d, notify: true);
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

    internal void ApplyRuntimeFrame(SpineAnimationFrameDto? frame)
    {
        SetCurrentFrame(frame, notify: true);
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
            StepRuntimeFrame(direction);
            return;
        }

        IsPlaying = false;
        _isShowingSetupPose = false;
        var currentIndex = ResolveFrameIndex(frames, PlaybackTime);
        var nextIndex = (currentIndex + direction + frames.Count) % frames.Count;
        _cachedFrameIndex = nextIndex;
        var playbackTimeChanged = SetPlaybackTimeCore(frames[nextIndex].Time, notify: false);
        var currentFrameChanged = SetCurrentFrame(frames[nextIndex], notify: false);
        if (playbackTimeChanged)
        {
            OnPropertyChanged(nameof(PlaybackTime));
        }

        if (currentFrameChanged)
        {
            OnPropertyChanged(nameof(CurrentFrame));
        }
    }

    private void StepRuntimeFrame(int direction)
    {
        if (!HasRuntimePlayback || Duration <= 0d)
        {
            return;
        }

        IsPlaying = false;
        _isShowingSetupPose = false;
        var step = Math.Clamp(1d / 60d, 0d, Duration);
        var nextTime = PlaybackTime + (direction * step);
        if (nextTime < 0d)
        {
            nextTime += Duration;
        }
        else if (nextTime > Duration)
        {
            nextTime %= Duration;
        }

        SetPlaybackTimeCore(nextTime, notify: true);
    }

    private bool SetPlaybackTimeCore(double value, bool notify)
    {
        var maxTime = Duration > 0d ? Duration : double.MaxValue;
        var normalizedValue = Math.Clamp(value, 0d, maxTime);
        if (EqualityComparer<double>.Default.Equals(_playbackTime, normalizedValue))
        {
            return false;
        }

        _playbackTime = normalizedValue;
        if (notify)
        {
            OnPropertyChanged(nameof(PlaybackTime));
        }

        return true;
    }

    private bool SyncCurrentFrame(bool notify = true)
    {
        var frames = SelectedAnimation?.SampledFrames;
        if (_isShowingSetupPose &&
            ActiveSetupFrame is not null)
        {
            return SetCurrentFrame(ActiveSetupFrame, notify);
        }

        if (frames is not { Count: > 0 })
        {
            return HasRuntimePlayback
                ? SetCurrentFrame(ActiveSetupFrame, notify)
                : SetCurrentFrame(null, notify);
        }

        _cachedFrameIndex = ResolveFrameIndex(frames, PlaybackTime);
        return SetCurrentFrame(frames[_cachedFrameIndex], notify);
    }

    private bool SetCurrentFrame(SpineAnimationFrameDto? value, bool notify)
    {
        if (ReferenceEquals(_currentFrame, value))
        {
            return false;
        }

        _currentFrame = value;
        if (notify)
        {
            OnPropertyChanged(nameof(CurrentFrame));
        }

        return true;
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasSpineAnimation));
        OnPropertyChanged(nameof(HasRuntimePlayback));
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(IsSkinSelectionEnabled));
        OnPropertyChanged(nameof(SelectedAnimation));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(CurrentFrame));
    }

    private readonly record struct AnimationSelectionUpdate(
        bool AnimationNameChanged,
        bool PlaybackTimeChanged,
        bool CurrentFrameChanged);
}
