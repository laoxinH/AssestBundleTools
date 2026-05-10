using System.ComponentModel;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer.Controls;

public sealed partial class InspectorPanelView
{
    private const int MaxSectionScrollRestoreAttempts = 30;
    private const double SectionScrollRestoreTolerance = 0.5d;
    private static readonly TimeSpan SectionScrollRestoreRetryInterval = TimeSpan.FromMilliseconds(80);

    private InspectorPanelViewModel? _observedViewModel;
    private bool _isRestoringSectionScrollPositions;
    private bool _hasPendingSectionScrollRestore;
    private bool _isSectionScrollRestoreQueued;
    private bool _suppressSectionScrollCapture;
    private int _sectionScrollRestoreAttempts;
    private DispatcherQueueTimer? _sectionScrollRestoreTimer;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachObservedViewModel(ViewModel);
        Bindings.Update();
        QueueSectionScrollPositionRestore();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CaptureCurrentSectionScrollPositions(_observedViewModel);
        DetachObservedViewModel(_observedViewModel);
        _hasPendingSectionScrollRestore = false;
        _isSectionScrollRestoreQueued = false;
        _sectionScrollRestoreAttempts = 0;
        _suppressSectionScrollCapture = false;
        StopSectionScrollRestoreTimer();
    }

    /// <summary>
    /// 内层详情切换保持单选语义，点击后统一回写到同一个 SectionIndex，
    /// 避免控件自己的开关行为把“当前选中页”状态打散。
    /// </summary>
    private void OnSectionRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null || sender is not RadioButton { Tag: string sectionTag })
        {
            return;
        }

        if (int.TryParse(sectionTag, out var sectionIndex))
        {
            ViewModel.SelectedSectionIndex = sectionIndex;
        }
    }

    private void OnSectionScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isRestoringSectionScrollPositions ||
            _suppressSectionScrollCapture ||
            ViewModel is null ||
            sender is not ScrollViewer scrollViewer ||
            !TryGetSectionIndex(scrollViewer, out var sectionIndex))
        {
            return;
        }

        ViewModel.CaptureSectionScrollPosition(
            sectionIndex,
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset);
    }

    private void OnSectionSelectorScrollViewerViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_isRestoringSectionScrollPositions ||
            _suppressSectionScrollCapture ||
            ViewModel is null ||
            sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        ViewModel.CaptureSectionSelectorScrollPosition(
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset);
    }

    private void OnSectionScrollViewerLayoutUpdated(object? sender, object e)
    {
        if (!_hasPendingSectionScrollRestore || _isSectionScrollRestoreQueued)
        {
            return;
        }

        QueueSectionScrollPositionRestoreAttempt();
    }

    private static void OnViewModelPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not InspectorPanelView view)
        {
            return;
        }

        view.CaptureCurrentSectionScrollPositions(args.OldValue as InspectorPanelViewModel);
        view.DetachObservedViewModel(args.OldValue as InspectorPanelViewModel);
        view.AttachObservedViewModel(args.NewValue as InspectorPanelViewModel);
        view.Bindings.Update();
        view.QueueSectionScrollPositionRestore();
    }

    private void AttachObservedViewModel(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        _observedViewModel = viewModel;
        _observedViewModel.PropertyChanged += OnObservedViewModelPropertyChanged;
    }

    private void DetachObservedViewModel(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || !ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        viewModel.PropertyChanged -= OnObservedViewModelPropertyChanged;
        _observedViewModel = null;
    }

    private void OnObservedViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (
            nameof(InspectorPanelViewModel.SelectedSectionIndex) or
            nameof(InspectorPanelViewModel.DetailsVisibility) or
            nameof(InspectorPanelViewModel.DetailsSectionVisibility) or
            nameof(InspectorPanelViewModel.DependenciesSectionVisibility) or
            nameof(InspectorPanelViewModel.ReplacementSectionVisibility) or
            nameof(InspectorPanelViewModel.ChangesSectionVisibility)))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            QueueSectionScrollPositionRestore();
            return;
        }

        DispatcherQueue.TryEnqueue(QueueSectionScrollPositionRestore);
    }

    private void QueueSectionScrollPositionRestore()
    {
        _suppressSectionScrollCapture = true;
        _hasPendingSectionScrollRestore = true;
        _sectionScrollRestoreAttempts = 0;
        QueueSectionScrollPositionRestoreAttempt();
    }

    private void QueueSectionScrollPositionRestoreAttempt()
    {
        if (_isSectionScrollRestoreQueued)
        {
            return;
        }

        _isSectionScrollRestoreQueued = true;
        if (!DispatcherQueue.TryEnqueue(RestoreQueuedSectionScrollPositions))
        {
            RestoreQueuedSectionScrollPositions();
        }
    }

    private void RestoreQueuedSectionScrollPositions()
    {
        _isSectionScrollRestoreQueued = false;
        StopSectionScrollRestoreTimer();

        if (!_hasPendingSectionScrollRestore)
        {
            ClearSectionScrollCaptureSuppression();
            return;
        }

        _sectionScrollRestoreAttempts++;
        var needsRetry = false;
        try
        {
            needsRetry = RestoreSectionScrollPositions(_observedViewModel);
        }
        finally
        {
            if (needsRetry && _sectionScrollRestoreAttempts < MaxSectionScrollRestoreAttempts)
            {
                ScheduleSectionScrollRestoreRetry();
            }
            else
            {
                CompleteSectionScrollPositionRestore();
            }
        }
    }

    private bool RestoreSectionScrollPositions(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null)
        {
            return false;
        }

        var needsRetry = false;
        _isRestoringSectionScrollPositions = true;
        try
        {
            RestoreSectionSelectorScrollPosition(SectionSelectorScrollViewer, viewModel);
            needsRetry |= RestoreSectionScrollPosition(DetailsSectionScrollViewer, viewModel, sectionIndex: 0);
            needsRetry |= RestoreSectionScrollPosition(DependenciesSectionScrollViewer, viewModel, sectionIndex: 1);
            needsRetry |= RestoreSectionScrollPosition(ReplacementSectionScrollViewer, viewModel, sectionIndex: 2);
            needsRetry |= RestoreSectionScrollPosition(ChangesSectionScrollViewer, viewModel, sectionIndex: 3);
        }
        finally
        {
            _isRestoringSectionScrollPositions = false;
        }

        return needsRetry;
    }

    private void CaptureCurrentSectionScrollPositions(InspectorPanelViewModel? viewModel)
    {
        if (viewModel is null || _hasPendingSectionScrollRestore)
        {
            return;
        }

        CaptureSectionSelectorScrollPosition(viewModel, SectionSelectorScrollViewer);
        CaptureSectionScrollPosition(viewModel, DetailsSectionScrollViewer, sectionIndex: 0);
        CaptureSectionScrollPosition(viewModel, DependenciesSectionScrollViewer, sectionIndex: 1);
        CaptureSectionScrollPosition(viewModel, ReplacementSectionScrollViewer, sectionIndex: 2);
        CaptureSectionScrollPosition(viewModel, ChangesSectionScrollViewer, sectionIndex: 3);
    }

    private static void RestoreSectionSelectorScrollPosition(
        ScrollViewer scrollViewer,
        InspectorPanelViewModel viewModel)
    {
        var position = viewModel.GetSectionSelectorScrollPosition();
        scrollViewer.ChangeView(
            position.HorizontalOffset,
            position.VerticalOffset,
            zoomFactor: null,
            disableAnimation: true);
    }

    private static bool RestoreSectionScrollPosition(
        ScrollViewer scrollViewer,
        InspectorPanelViewModel viewModel,
        int sectionIndex)
    {
        var position = viewModel.GetSectionScrollPosition(sectionIndex);
        scrollViewer.ChangeView(
            position.HorizontalOffset,
            position.VerticalOffset,
            zoomFactor: null,
            disableAnimation: true);
        return ShouldRetrySectionScrollRestore(scrollViewer, position);
    }

    private void ScheduleSectionScrollRestoreRetry()
    {
        _suppressSectionScrollCapture = true;
        _sectionScrollRestoreTimer ??= CreateSectionScrollRestoreTimer();
        _sectionScrollRestoreTimer.Interval = SectionScrollRestoreRetryInterval;
        _sectionScrollRestoreTimer.Start();
    }

    private DispatcherQueueTimer CreateSectionScrollRestoreTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += OnSectionScrollRestoreTimerTick;
        return timer;
    }

    private void OnSectionScrollRestoreTimerTick(DispatcherQueueTimer sender, object args)
    {
        QueueSectionScrollPositionRestoreAttempt();
    }

    private void StopSectionScrollRestoreTimer()
    {
        _sectionScrollRestoreTimer?.Stop();
    }

    private void CompleteSectionScrollPositionRestore()
    {
        _hasPendingSectionScrollRestore = false;
        _sectionScrollRestoreAttempts = 0;
        if (!DispatcherQueue.TryEnqueue(ClearSectionScrollCaptureSuppression))
        {
            ClearSectionScrollCaptureSuppression();
        }
    }

    private void ClearSectionScrollCaptureSuppression()
    {
        if (!_hasPendingSectionScrollRestore)
        {
            _suppressSectionScrollCapture = false;
        }
    }

    private static bool ShouldRetrySectionScrollRestore(
        ScrollViewer scrollViewer,
        InspectorPanelScrollPosition position)
    {
        if (scrollViewer.Visibility != Visibility.Visible)
        {
            return false;
        }

        var shouldRestoreHorizontal = position.HorizontalOffset > SectionScrollRestoreTolerance;
        var shouldRestoreVertical = position.VerticalOffset > SectionScrollRestoreTolerance;
        if (!shouldRestoreHorizontal && !shouldRestoreVertical)
        {
            return false;
        }

        var scrollableWidth = NormalizeScrollableOffset(scrollViewer.ScrollableWidth);
        var scrollableHeight = NormalizeScrollableOffset(scrollViewer.ScrollableHeight);
        if (shouldRestoreHorizontal &&
            position.HorizontalOffset > scrollableWidth + SectionScrollRestoreTolerance)
        {
            return true;
        }

        if (shouldRestoreVertical &&
            position.VerticalOffset > scrollableHeight + SectionScrollRestoreTolerance)
        {
            return true;
        }

        return (shouldRestoreHorizontal &&
                Math.Abs(scrollViewer.HorizontalOffset - position.HorizontalOffset) > SectionScrollRestoreTolerance) ||
               (shouldRestoreVertical &&
                Math.Abs(scrollViewer.VerticalOffset - position.VerticalOffset) > SectionScrollRestoreTolerance);
    }

    private static double NormalizeScrollableOffset(double offset)
    {
        return double.IsFinite(offset) && offset > 0d
            ? offset
            : 0d;
    }

    private static void CaptureSectionSelectorScrollPosition(
        InspectorPanelViewModel viewModel,
        ScrollViewer scrollViewer)
    {
        viewModel.CaptureSectionSelectorScrollPosition(
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset);
    }

    private static void CaptureSectionScrollPosition(
        InspectorPanelViewModel viewModel,
        ScrollViewer scrollViewer,
        int sectionIndex)
    {
        viewModel.CaptureSectionScrollPosition(
            sectionIndex,
            scrollViewer.HorizontalOffset,
            scrollViewer.VerticalOffset);
    }

    private static bool TryGetSectionIndex(FrameworkElement element, out int sectionIndex)
    {
        sectionIndex = 0;
        return element.Tag is string sectionTag &&
               int.TryParse(sectionTag, out sectionIndex);
    }
}
