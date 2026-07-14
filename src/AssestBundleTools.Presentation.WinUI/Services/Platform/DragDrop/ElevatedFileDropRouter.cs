using AssestBundleTools.Presentation.WinUI.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace AssestBundleTools.Presentation.WinUI.Services;

internal sealed class ElevatedFileDropRouter : IElevatedFileDropRouter
{
    private readonly object _gate = new();
    private readonly IElevatedDragDropCompatibilityService _source;
    private readonly IElevatedDropCoordinateMapper _coordinateMapper;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly List<RegisteredTarget> _targets = [];
    private long _registrationSequence;

    public ElevatedFileDropRouter(
        IElevatedDragDropCompatibilityService source,
        IElevatedDropCoordinateMapper coordinateMapper,
        IAppLogService appLogService,
        ILocalizationService localizationService)
    {
        _source = source;
        _coordinateMapper = coordinateMapper;
        _appLogService = appLogService;
        _localizationService = localizationService;
        _source.FilesDropped += OnFilesDropped;
    }

    public IDisposable RegisterTarget(ElevatedFileDropTargetRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrWhiteSpace(registration.TargetId))
        {
            throw new ArgumentException("A stable target ID is required.", nameof(registration));
        }
        ArgumentNullException.ThrowIfNull(registration.SourceWindow);
        ArgumentNullException.ThrowIfNull(registration.TargetElement);
        ArgumentNullException.ThrowIfNull(registration.CanHandle);
        ArgumentNullException.ThrowIfNull(registration.HandleDropAsync);

        var registeredTarget = new RegisteredTarget(
            Interlocked.Increment(ref _registrationSequence),
            registration);
        lock (_gate)
        {
            _targets.Add(registeredTarget);
        }

        return new RegistrationLease(this, registeredTarget.Sequence);
    }

    private async void OnFilesDropped(object? sender, ElevatedFileDropReceivedEventArgs e)
    {
        try
        {
            await RouteAsync(e);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log($"Elevated file-drop routing failed: {exception}");
            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.ElevatedDropLogRoutingFailedFormat,
                exception.GetType().Name,
                $"0x{exception.HResult:X8}",
                exception.Message));
        }
    }

    private async Task RouteAsync(ElevatedFileDropReceivedEventArgs e)
    {
        var candidates = CaptureCandidates(e.SourceWindow);
        if (!_coordinateMapper.TryMapToRoot(
                e.SourceWindow,
                e.Position,
                out var rootMapping) ||
            FindExactMatch(candidates, rootMapping) is not { } match)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.ElevatedDropLogNoTargetFormat,
                e.Paths.Count,
                e.Position.ClientX,
                e.Position.ClientY,
                e.Position.ClientWidth,
                e.Position.ClientHeight));
            return;
        }

        var coordinates = new ElevatedDropCoordinateMapping(
            rootMapping.RootPosition,
            match.LocalPosition,
            rootMapping.ScaleX,
            rootMapping.ScaleY,
            rootMapping.ValidationMode);
        var context = new ElevatedFileDropContext(
            match.Target.Registration.TargetId,
            e.Paths,
            e.Position,
            coordinates);

        AppendMatchedDiagnostic(context);
        try
        {
            await match.Target.Registration.HandleDropAsync(context);
        }
        catch (Exception exception)
        {
            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.ElevatedDropLogTargetFailedFormat,
                context.TargetId,
                exception.GetType().Name,
                $"0x{exception.HResult:X8}",
                exception.Message));
        }
    }

    private IReadOnlyList<RegisteredTarget> CaptureCandidates(Window sourceWindow)
    {
        lock (_gate)
        {
            return _targets
                .Where(target => ReferenceEquals(target.Registration.SourceWindow, sourceWindow))
                .OrderByDescending(static target => target.Registration.Priority)
                .ThenByDescending(static target => target.Sequence)
                .ToArray();
        }
    }

    private TargetMatch? FindExactMatch(
        IReadOnlyList<RegisteredTarget> candidates,
        ElevatedDropRootCoordinateMapping rootMapping)
    {
        foreach (var candidate in candidates)
        {
            if (!CanHandle(candidate) ||
                !_coordinateMapper.TryMapToElement(
                    rootMapping,
                    candidate.Registration.TargetElement,
                    out var localPosition) ||
                !Contains(candidate.Registration.TargetElement, localPosition))
            {
                continue;
            }

            return new TargetMatch(candidate, localPosition);
        }

        return null;
    }

    private static bool CanHandle(RegisteredTarget target)
    {
        try
        {
            return target.Registration.TargetElement.IsLoaded &&
                   target.Registration.TargetElement.Visibility == Visibility.Visible &&
                   target.Registration.CanHandle();
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Log(
                $"Elevated drop target '{target.Registration.TargetId}' availability check failed: {exception}");
            return false;
        }
    }

    private static bool Contains(FrameworkElement element, Point localPosition) =>
        element.ActualWidth > 0d &&
        element.ActualHeight > 0d &&
        localPosition.X >= 0d &&
        localPosition.X <= element.ActualWidth &&
        localPosition.Y >= 0d &&
        localPosition.Y <= element.ActualHeight;

    private void AppendMatchedDiagnostic(ElevatedFileDropContext context)
    {
        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.ElevatedDropLogTargetMatchedFormat,
            context.TargetId,
            context.Paths.Count,
            context.NativePosition.ClientX,
            context.NativePosition.ClientY,
            context.NativePosition.ClientWidth,
            context.NativePosition.ClientHeight,
            context.NativePosition.IsClientArea,
            context.Coordinates.ValidationMode,
            context.NativePosition.ScreenX,
            context.NativePosition.ScreenY,
            context.NativePosition.HasScreenPosition,
            context.Coordinates.RootPosition.X,
            context.Coordinates.RootPosition.Y,
            context.Coordinates.LocalPosition.X,
            context.Coordinates.LocalPosition.Y,
            context.Coordinates.ScaleX,
            context.Coordinates.ScaleY));
    }

    private void Unregister(long sequence)
    {
        lock (_gate)
        {
            _targets.RemoveAll(target => target.Sequence == sequence);
        }
    }

    private readonly record struct RegisteredTarget(
        long Sequence,
        ElevatedFileDropTargetRegistration Registration);

    private readonly record struct TargetMatch(
        RegisteredTarget Target,
        Point LocalPosition);

    private sealed class RegistrationLease(
        ElevatedFileDropRouter owner,
        long sequence) : IDisposable
    {
        private ElevatedFileDropRouter? _owner = owner;

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.Unregister(sequence);
        }
    }
}
