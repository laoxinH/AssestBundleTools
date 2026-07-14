using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
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
    private IReadOnlyList<string> _animationNames = Array.Empty<string>();
    private IReadOnlyList<string> _skinNames = Array.Empty<string>();
    private InspectorPreviewImageBackgroundMode _backgroundMode = InspectorPreviewImageBackgroundMode.Solid;

    public IReadOnlyList<string> AnimationNames
    {
        get => _animationNames;
        private set
        {
            if (SetProperty(ref _animationNames, value))
            {
                OnPropertyChanged(nameof(SelectedAnimationName));
            }
        }
    }

    public IReadOnlyList<string> SkinNames
    {
        get => _skinNames;
        private set
        {
            if (SetProperty(ref _skinNames, value))
            {
                OnPropertyChanged(nameof(IsSkinSelectionEnabled));
            }
        }
    }

    public IReadOnlyList<double> PlaybackSpeedOptions { get; } = [0.25d, 0.5d, 1d, 1.5d, 2d, 3d];

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
            if (!TryCoerceSelectorName(AnimationNames, value, out var selectedAnimationName))
            {
                return;
            }

            var update = ApplySelectedAnimationName(selectedAnimationName, resetPlayback: true);
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
            if (!TryCoerceSelectorName(SkinNames, value, out var selectedSkinName) ||
                EqualityComparer<string?>.Default.Equals(_selectedSkinName, selectedSkinName))
            {
                return;
            }

            _selectedSkinName = selectedSkinName;
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

            RaisePreviewStateChanged();
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

        RaisePreviewStateChanged();
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

            if (playbackTimeChanged)
            {
                RaisePreviewStateChanged();
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
        set
        {
            if (SetProperty(ref _playbackSpeed, Math.Clamp(value, 0.1d, 3d)))
            {
                RaisePreviewStateChanged();
            }
        }
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
                RaisePreviewStateChanged();
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

    public string PlayPauseGlyph => IsPlaying ? "\uED5B" : "\uEE8D";

    internal void Apply(
        SpineAnimationPreviewDto preview,
        IReadOnlyDictionary<string, SpineAnimationPreviewTextureState> texturePreviews,
        WorkspaceSpinePreviewStateDto? previewState = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(texturePreviews);

        using var _ = SuppressPreviewStateChanged();
        Clear();
        Preview = preview;
        TexturePageContents = CreateTexturePageContents(texturePreviews);
        var skinNames = ResolveSkinNames(preview);
        SkinNames = skinNames.Count > 0 ? skinNames : ["default"];
        SelectedSkinName = ResolveInitialSkinName(preview) ?? SkinNames.FirstOrDefault();
        SelectedAnimationName ??= AnimationNames.FirstOrDefault();
        _isShowingSetupPose = false;
        IsPlaying = true;
        RestorePreviewState(previewState);
        NotifyStateChanged();
    }

    internal void Clear()
    {
        using var _ = SuppressPreviewStateChanged();
        AnimationNames = Array.Empty<string>();
        SkinNames = Array.Empty<string>();
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

    private static bool TryCoerceSelectorName(
        IReadOnlyList<string> availableNames,
        string? requestedName,
        out string? selectedName)
    {
        if (string.IsNullOrWhiteSpace(requestedName))
        {
            selectedName = null;
            return availableNames.Count == 0;
        }

        var trimmedName = requestedName.Trim();
        selectedName = availableNames.FirstOrDefault(name =>
            string.Equals(name, trimmedName, StringComparison.Ordinal));
        return selectedName is not null;
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
