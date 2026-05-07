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
    private double _playbackTime;
    private double _playbackSpeed = 1d;
    private bool _isPlaying = true;

    public ObservableCollection<string> AnimationNames { get; } = [];

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
                PlaybackTime = 0d;
                OnPropertyChanged(nameof(SelectedAnimation));
                OnPropertyChanged(nameof(Duration));
            }
        }
    }

    public SpineAnimationClipDto? SelectedAnimation =>
        Preview?.Animations.FirstOrDefault(animation =>
            string.Equals(animation.Name, SelectedAnimationName, StringComparison.Ordinal));

    public double Duration => Math.Max(0d, SelectedAnimation?.Duration ?? 0d);

    public double PlaybackTime
    {
        get => _playbackTime;
        private set => SetProperty(ref _playbackTime, Math.Max(0d, value));
    }

    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, Math.Clamp(value, 0.1d, 3d));
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

    public bool HasSpineAnimation => Preview is { Animations.Count: > 0 } &&
                                     TexturePageContents.Count > 0;

    public Visibility Visibility => HasSpineAnimation ? Visibility.Visible : Visibility.Collapsed;

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
        foreach (var animation in preview.Animations)
        {
            AnimationNames.Add(animation.Name);
        }

        SelectedAnimationName = AnimationNames.FirstOrDefault();
        IsPlaying = true;
        NotifyStateChanged();
    }

    internal void Clear()
    {
        AnimationNames.Clear();
        Preview = null;
        TexturePageContents = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        SelectedAnimationName = null;
        PlaybackTime = 0d;
        IsPlaying = false;
        NotifyStateChanged();
    }

    public void TogglePlayback()
    {
        if (!HasSpineAnimation)
        {
            return;
        }

        IsPlaying = !IsPlaying;
    }

    public void Restart()
    {
        PlaybackTime = 0d;
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

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasSpineAnimation));
        OnPropertyChanged(nameof(Visibility));
        OnPropertyChanged(nameof(SelectedAnimation));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(PlayPauseGlyph));
    }
}
