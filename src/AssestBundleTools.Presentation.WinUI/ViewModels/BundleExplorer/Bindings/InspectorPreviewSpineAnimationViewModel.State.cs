using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class InspectorPreviewSpineAnimationViewModel
{
    private const double DefaultViewportZoom = 1d;
    private const double MinViewportZoom = 0.2d;
    private const double MaxViewportZoom = 8d;

    private int _previewStateChangeSuppressionDepth;
    private double _viewportZoom = DefaultViewportZoom;
    private double _viewportPanX;
    private double _viewportPanY;

    public event EventHandler? PreviewStateChanged;

    public double ViewportZoom
    {
        get => _viewportZoom;
        private set => SetProperty(ref _viewportZoom, NormalizeViewportZoom(value));
    }

    public double ViewportPanX
    {
        get => _viewportPanX;
        private set => SetProperty(ref _viewportPanX, NormalizeViewportPan(value));
    }

    public double ViewportPanY
    {
        get => _viewportPanY;
        private set => SetProperty(ref _viewportPanY, NormalizeViewportPan(value));
    }

    internal WorkspaceSpinePreviewStateDto CapturePreviewState() =>
        new(
            SelectedAnimationName: SelectedAnimationName,
            SelectedSkinName: SelectedSkinName,
            PlaybackSpeed: PlaybackSpeed,
            PlaybackTime: PlaybackTime,
            IsPlaying: IsPlaying,
            IsShowingSetupPose: IsShowingSetupPose,
            BackgroundMode: _backgroundMode.ToString(),
            ViewportZoom: ViewportZoom,
            ViewportPanX: ViewportPanX,
            ViewportPanY: ViewportPanY);

    internal void RestorePreviewState(WorkspaceSpinePreviewStateDto? state)
    {
        if (state is null)
        {
            return;
        }

        using var _ = SuppressPreviewStateChanged();
        RestoreViewportState(
            state.ViewportZoom,
            state.ViewportPanX,
            state.ViewportPanY,
            notifyPreviewStateChanged: false);
        SetBackgroundMode(ParseBackgroundMode(state.BackgroundMode));

        if (TryResolveExistingName(SkinNames, state.SelectedSkinName, out var selectedSkinName))
        {
            SelectedSkinName = selectedSkinName;
        }

        if (TryResolveExistingName(AnimationNames, state.SelectedAnimationName, out var selectedAnimationName))
        {
            SelectedAnimationName = selectedAnimationName;
        }

        if (state.PlaybackSpeed.HasValue)
        {
            PlaybackSpeed = state.PlaybackSpeed.Value;
        }

        if (state.PlaybackTime.HasValue)
        {
            PlaybackTime = state.PlaybackTime.Value;
        }

        if (state.IsShowingSetupPose == true)
        {
            ShowSetupPose();
        }
        else
        {
            _isShowingSetupPose = false;
            OnPropertyChanged(nameof(IsShowingSetupPose));
            IsPlaying = state.IsPlaying ?? true;
        }

        SyncCurrentFrame();
        SynchronizeDisplayPlaybackTime(force: true);
        NotifyStateChanged();
    }

    internal void RestoreViewportState(
        double? zoom,
        double? panX,
        double? panY,
        bool notifyPreviewStateChanged)
    {
        var normalizedZoom = NormalizeViewportZoom(zoom ?? DefaultViewportZoom);
        var normalizedPanX = NormalizeViewportPan(panX ?? 0d);
        var normalizedPanY = NormalizeViewportPan(panY ?? 0d);
        var changed = false;
        changed = SetProperty(ref _viewportZoom, normalizedZoom, nameof(ViewportZoom)) || changed;
        changed = SetProperty(ref _viewportPanX, normalizedPanX, nameof(ViewportPanX)) || changed;
        changed = SetProperty(ref _viewportPanY, normalizedPanY, nameof(ViewportPanY)) || changed;
        if (changed && notifyPreviewStateChanged)
        {
            RaisePreviewStateChanged();
        }
    }

    internal void UpdateViewportState(
        double zoom,
        double panX,
        double panY)
    {
        RestoreViewportState(
            zoom,
            panX,
            panY,
            notifyPreviewStateChanged: true);
    }

    private IDisposable SuppressPreviewStateChanged()
    {
        _previewStateChangeSuppressionDepth++;
        return new PreviewStateChangeSuppressionScope(this);
    }

    private void RaisePreviewStateChanged()
    {
        if (_previewStateChangeSuppressionDepth > 0)
        {
            return;
        }

        PreviewStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryResolveExistingName(
        IReadOnlyList<string> names,
        string? candidate,
        out string resolvedName)
    {
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            var match = names.FirstOrDefault(name =>
                string.Equals(name, candidate.Trim(), StringComparison.Ordinal));
            if (match is not null)
            {
                resolvedName = match;
                return true;
            }
        }

        resolvedName = string.Empty;
        return false;
    }

    private static InspectorPreviewImageBackgroundMode ParseBackgroundMode(string? mode)
    {
        return Enum.TryParse<InspectorPreviewImageBackgroundMode>(
            mode,
            ignoreCase: true,
            out var parsedMode)
            ? parsedMode
            : InspectorPreviewImageBackgroundMode.Solid;
    }

    private static double NormalizeViewportZoom(double zoom) =>
        double.IsFinite(zoom)
            ? Math.Clamp(zoom, MinViewportZoom, MaxViewportZoom)
            : DefaultViewportZoom;

    private static double NormalizeViewportPan(double pan) =>
        double.IsFinite(pan) ? pan : 0d;

    private readonly struct PreviewStateChangeSuppressionScope : IDisposable
    {
        private readonly InspectorPreviewSpineAnimationViewModel _owner;

        public PreviewStateChangeSuppressionScope(InspectorPreviewSpineAnimationViewModel owner)
        {
            _owner = owner;
        }

        public void Dispose()
        {
            if (_owner._previewStateChangeSuppressionDepth <= 0)
            {
                _owner._previewStateChangeSuppressionDepth = 0;
                return;
            }

            _owner._previewStateChangeSuppressionDepth--;
        }
    }
}
