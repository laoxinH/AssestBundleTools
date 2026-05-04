using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed class InspectorPreviewAnimationClipViewModel : ObservableObject
{
    private AssetPreviewAnimationClipDto? _animationClip;
    private string _assetNameText = string.Empty;
    private string _timingText = string.Empty;
    private string _curveCountText = string.Empty;
    private string _legacyText = string.Empty;
    private string _loopText = string.Empty;
    private string _eventsTitle = string.Empty;
    private string _emptyEventsText = string.Empty;
    private string _truncatedText = string.Empty;
    private IReadOnlyList<AnimationClipEventRowViewModel> _eventRows = Array.Empty<AnimationClipEventRowViewModel>();

    public AssetPreviewAnimationClipDto? AnimationClip
    {
        get => _animationClip;
        private set
        {
            if (SetProperty(ref _animationClip, value))
            {
                OnPropertyChanged(nameof(HasAnimationClip));
                OnPropertyChanged(nameof(Visibility));
            }
        }
    }

    public string AssetNameText
    {
        get => _assetNameText;
        private set => SetProperty(ref _assetNameText, value);
    }

    public string TimingText
    {
        get => _timingText;
        private set => SetProperty(ref _timingText, value);
    }

    public string CurveCountText
    {
        get => _curveCountText;
        private set => SetProperty(ref _curveCountText, value);
    }

    public string LegacyText
    {
        get => _legacyText;
        private set => SetProperty(ref _legacyText, value);
    }

    public string LoopText
    {
        get => _loopText;
        private set => SetProperty(ref _loopText, value);
    }

    public string EventsTitle
    {
        get => _eventsTitle;
        private set => SetProperty(ref _eventsTitle, value);
    }

    public string EmptyEventsText
    {
        get => _emptyEventsText;
        private set => SetProperty(ref _emptyEventsText, value);
    }

    public string TruncatedText
    {
        get => _truncatedText;
        private set
        {
            if (SetProperty(ref _truncatedText, value))
            {
                OnPropertyChanged(nameof(TruncatedTextVisibility));
            }
        }
    }

    public IReadOnlyList<AnimationClipEventRowViewModel> EventRows
    {
        get => _eventRows;
        private set
        {
            if (SetProperty(ref _eventRows, value))
            {
                OnPropertyChanged(nameof(EventRowsVisibility));
                OnPropertyChanged(nameof(EventRowsEmptyVisibility));
            }
        }
    }

    public bool HasAnimationClip => AnimationClip is not null;

    public Visibility Visibility => HasAnimationClip ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EventRowsVisibility => EventRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EventRowsEmptyVisibility => EventRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TruncatedTextVisibility =>
        string.IsNullOrWhiteSpace(TruncatedText) ? Visibility.Collapsed : Visibility.Visible;

    internal void Apply(
        AssetPreviewAnimationClipDto animationClip,
        AnimationClipPreviewDisplayState displayState)
    {
        ArgumentNullException.ThrowIfNull(animationClip);
        ArgumentNullException.ThrowIfNull(displayState);

        AnimationClip = animationClip;
        AssetNameText = displayState.AssetNameText;
        TimingText = displayState.TimingText;
        CurveCountText = displayState.CurveCountText;
        LegacyText = displayState.LegacyText;
        LoopText = displayState.LoopText;
        EventsTitle = displayState.EventsTitle;
        EmptyEventsText = displayState.EmptyEventsText;
        TruncatedText = displayState.TruncatedText;
        EventRows = displayState.EventRows.Select(static row => new AnimationClipEventRowViewModel(row)).ToArray();
    }

    internal void Clear()
    {
        AnimationClip = null;
        AssetNameText = string.Empty;
        TimingText = string.Empty;
        CurveCountText = string.Empty;
        LegacyText = string.Empty;
        LoopText = string.Empty;
        EventsTitle = string.Empty;
        EmptyEventsText = string.Empty;
        TruncatedText = string.Empty;
        EventRows = Array.Empty<AnimationClipEventRowViewModel>();
    }
}

public sealed class AnimationClipEventRowViewModel
{
    public AnimationClipEventRowViewModel(AnimationClipEventRowDisplayState row)
    {
        TimeText = row.TimeText;
        FunctionNameText = row.FunctionNameText;
        ParameterText = row.ParameterText;
    }

    public string TimeText { get; }

    public string FunctionNameText { get; }

    public string ParameterText { get; }
}
