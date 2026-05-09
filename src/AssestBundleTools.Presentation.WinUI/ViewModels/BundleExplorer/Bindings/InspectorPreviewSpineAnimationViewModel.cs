using System.Collections.ObjectModel;
using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel : ObservableObject
{
    private static readonly TimeSpan PlaybackDisplayRefreshInterval = TimeSpan.FromMilliseconds(100);

    private SpineAnimationPreviewDto? _preview;
    private IReadOnlyDictionary<string, SpineAnimationTexturePageContent> _texturePageContents =
        new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal);
    private string? _selectedAnimationName;
    private string? _selectedSkinName;
    private double _playbackTime;
    private double _displayPlaybackTime;
    private double _playbackSpeed = 1d;
    private TimeSpan _playbackDisplayElapsedSinceLastNotification;
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

    public IReadOnlyDictionary<string, SpineAnimationTexturePageContent> TexturePageContents
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
            OnPropertyChanged(nameof(CanExportFrameSequence));
            OnPropertyChanged(nameof(DisplayPlaybackTimeText));
            if (update.PlaybackTimeChanged)
            {
                OnPropertyChanged(nameof(PlaybackTime));
                SynchronizeDisplayPlaybackTime(force: true);
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
        OnPropertyChanged(nameof(CanExportFrameSequence));
        OnPropertyChanged(nameof(DisplayPlaybackTimeText));
        if (update.PlaybackTimeChanged)
        {
            OnPropertyChanged(nameof(PlaybackTime));
            SynchronizeDisplayPlaybackTime(force: true);
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
                SynchronizeDisplayPlaybackTime(force: true);
            }

            if (currentFrameChanged)
            {
                OnPropertyChanged(nameof(CurrentFrame));
            }
        }
    }

    public double DisplayPlaybackTime
    {
        get => _displayPlaybackTime;
        set => PlaybackTime = value;
    }

    public string DisplayPlaybackTimeText => $"{FormatPlaybackTime(DisplayPlaybackTime)} / {FormatPlaybackTime(Duration)}";

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

    public bool CanExportFrameSequence => HasSpineAnimation &&
                                          HasRuntimePlayback &&
                                          Duration > 0d &&
                                          TexturePageContents.Count > 0;

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
        TexturePageContents = CreateTexturePageContents(texturePreviews);
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
        TexturePageContents = new Dictionary<string, SpineAnimationTexturePageContent>(StringComparer.Ordinal);
        SelectedAnimationName = null;
        SelectedSkinName = null;
        _cachedFrameIndex = 0;
        _isShowingSetupPose = false;
        SetPlaybackTimeCore(0d, notify: true);
        SynchronizeDisplayPlaybackTime(force: true);
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
        SynchronizeDisplayPlaybackTime(force: true);
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

    public bool Advance(TimeSpan elapsed)
    {
        if (!IsPlaying || Duration <= 0d)
        {
            return false;
        }

        var elapsedSeconds = Math.Max(0d, elapsed.TotalSeconds);
        var nextTime = PlaybackTime + (elapsedSeconds * PlaybackSpeed);
        var normalizedTime = nextTime % Duration;
        var wrapped = normalizedTime < PlaybackTime;
        var playbackTimeChanged = SetPlaybackTimeCore(normalizedTime, notify: false);
        if (!playbackTimeChanged)
        {
            return false;
        }

        _isShowingSetupPose = false;
        _playbackDisplayElapsedSinceLastNotification += TimeSpan.FromSeconds(elapsedSeconds);
        if (HasRuntimePlayback)
        {
            SynchronizeDisplayPlaybackTimeIfDue(wrapped);
            return true;
        }

        if (SyncCurrentFrame(notify: false))
        {
            OnPropertyChanged(nameof(CurrentFrame));
        }

        SynchronizeDisplayPlaybackTimeIfDue(wrapped);
        return true;
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
            SynchronizeDisplayPlaybackTime(force: true);
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
        SynchronizeDisplayPlaybackTime(force: true);
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

    private void SynchronizeDisplayPlaybackTimeIfDue(bool force)
    {
        if (force || _playbackDisplayElapsedSinceLastNotification >= PlaybackDisplayRefreshInterval)
        {
            SynchronizeDisplayPlaybackTime(force: true);
        }
    }

    private void SynchronizeDisplayPlaybackTime(bool force)
    {
        if (!force && EqualityComparer<double>.Default.Equals(_displayPlaybackTime, _playbackTime))
        {
            return;
        }

        _playbackDisplayElapsedSinceLastNotification = TimeSpan.Zero;
        if (EqualityComparer<double>.Default.Equals(_displayPlaybackTime, _playbackTime))
        {
            return;
        }

        _displayPlaybackTime = _playbackTime;
        OnPropertyChanged(nameof(DisplayPlaybackTime));
        OnPropertyChanged(nameof(DisplayPlaybackTimeText));
    }

    private static string FormatPlaybackTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0d, seconds));
        var format = time.TotalHours >= 1d ? @"h\:mm\:ss\.ff" : @"m\:ss\.ff";
        return time.ToString(format, CultureInfo.InvariantCulture);
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
        OnPropertyChanged(nameof(CanExportFrameSequence));
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(IsSkinSelectionEnabled));
        OnPropertyChanged(nameof(SelectedAnimation));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(DisplayPlaybackTime));
        OnPropertyChanged(nameof(DisplayPlaybackTimeText));
        OnPropertyChanged(nameof(PlayPauseGlyph));
        OnPropertyChanged(nameof(CurrentFrame));
    }

    private readonly record struct AnimationSelectionUpdate(
        bool AnimationNameChanged, bool PlaybackTimeChanged, bool CurrentFrameChanged);
}
