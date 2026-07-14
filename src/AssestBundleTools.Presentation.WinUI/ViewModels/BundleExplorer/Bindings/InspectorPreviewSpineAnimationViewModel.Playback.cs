using System.Globalization;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel
{
    public void TogglePlayback()
    {
        if (!HasSpineAnimation)
        {
            return;
        }

        if (!IsPlaying)
        {
            if (_isShowingSetupPose)
            {
                _isShowingSetupPose = false;
                OnPropertyChanged(nameof(IsShowingSetupPose));
            }

            SyncCurrentFrame();
        }

        IsPlaying = !IsPlaying;
    }

    public void Restart()
    {
        if (_isShowingSetupPose)
        {
            _isShowingSetupPose = false;
            OnPropertyChanged(nameof(IsShowingSetupPose));
        }

        PlaybackTime = 0d;
        RaisePreviewStateChanged();
    }

    public void ShowSetupPose()
    {
        _isShowingSetupPose = true;
        OnPropertyChanged(nameof(IsShowingSetupPose));
        IsPlaying = false;
        SetPlaybackTimeCore(0d, notify: true);
        SynchronizeDisplayPlaybackTime(force: true);
        SyncCurrentFrame();
        RaisePreviewStateChanged();
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

        RaisePreviewStateChanged();
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
        RaisePreviewStateChanged();
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
}
