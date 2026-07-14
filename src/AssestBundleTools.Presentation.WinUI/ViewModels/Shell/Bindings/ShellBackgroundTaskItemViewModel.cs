using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.Shell.Bindings;

/// <summary>
/// Shell 后台任务悬浮面板中的单个任务投影。
/// </summary>
public sealed class ShellBackgroundTaskItemViewModel : ObservableObject
{
    private readonly ILocalizationService _localizationService;
    private readonly Action<Guid> _cancelTask;
    private Guid _taskId;
    private string _title = string.Empty;
    private string _detailText = string.Empty;
    private string _statusText = string.Empty;
    private string _progressText = string.Empty;
    private string _statusGlyph = "\uE508";
    private double _progressValue;
    private bool _isProgressIndeterminate;
    private bool _canCancel;
    private Visibility _cancelVisibility = Visibility.Collapsed;
    private Visibility _progressVisibility = Visibility.Collapsed;

    public ShellBackgroundTaskItemViewModel(
        ILocalizationService localizationService,
        Action<Guid> cancelTask,
        BackgroundWorkSnapshot snapshot)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _cancelTask = cancelTask ?? throw new ArgumentNullException(nameof(cancelTask));
        CancelCommand = new RelayCommand(Cancel, CanCancelTask);
        Update(snapshot);
    }

    public Guid TaskId
    {
        get => _taskId;
        private set => SetProperty(ref _taskId, value);
    }

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => SetProperty(ref _detailText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public string StatusGlyph
    {
        get => _statusGlyph;
        private set => SetProperty(ref _statusGlyph, value);
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        private set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public bool CanCancel
    {
        get => _canCancel;
        private set
        {
            if (SetProperty(ref _canCancel, value))
            {
                CancelVisibility = value ? Visibility.Visible : Visibility.Collapsed;
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public Visibility CancelVisibility
    {
        get => _cancelVisibility;
        private set => SetProperty(ref _cancelVisibility, value);
    }

    public Visibility ProgressVisibility
    {
        get => _progressVisibility;
        private set => SetProperty(ref _progressVisibility, value);
    }

    public string CancelToolTipText =>
        _localizationService.GetRequiredString(ResourceKeys.AppStatusBarTaskCancelToolTip);

    public RelayCommand CancelCommand { get; }

    public void Update(BackgroundWorkSnapshot snapshot)
    {
        TaskId = snapshot.TaskId;
        Title = ResolveTitle(snapshot);
        StatusText = ResolveStatusText(snapshot.Status);
        DetailText = string.IsNullOrWhiteSpace(snapshot.Detail)
            ? StatusText
            : snapshot.Detail.Trim();
        StatusGlyph = ResolveStatusGlyph(snapshot.Status);
        CanCancel = snapshot.CanCancel;
        ProgressVisibility = snapshot.Status == BackgroundWorkTaskStatus.Queued
            ? Visibility.Collapsed
            : Visibility.Visible;
        IsProgressIndeterminate =
            snapshot.Status == BackgroundWorkTaskStatus.Running &&
            (snapshot.IsIndeterminate || snapshot.TotalCount is null);

        if (snapshot.TotalCount is { } totalCount and > 0)
        {
            var completedCount = Math.Clamp(snapshot.CompletedCount, 0, totalCount);
            ProgressValue = completedCount * 100d / totalCount;
            ProgressText = _localizationService.GetFormattedString(
                ResourceKeys.AppStatusBarProgressFormat,
                completedCount,
                totalCount);
            return;
        }

        ProgressValue = 0d;
        ProgressText = snapshot.Status == BackgroundWorkTaskStatus.Running
            ? _localizationService.GetRequiredString(ResourceKeys.AppStatusBarWorkingText)
            : StatusText;
    }

    private bool CanCancelTask() => CanCancel;

    private void Cancel()
    {
        if (CanCancel)
        {
            _cancelTask(TaskId);
        }
    }

    private string ResolveTitle(BackgroundWorkSnapshot snapshot)
    {
        return string.IsNullOrWhiteSpace(snapshot.Title)
            ? _localizationService.GetRequiredString(ResolveProfileTitleKey(snapshot.Profile))
            : snapshot.Title.Trim();
    }

    private string ResolveStatusText(BackgroundWorkTaskStatus status)
    {
        var resourceKey = status switch
        {
            BackgroundWorkTaskStatus.Running => ResourceKeys.AppStatusBarTaskStatusRunning,
            BackgroundWorkTaskStatus.CancellationRequested => ResourceKeys.AppStatusBarTaskStatusCancelling,
            _ => ResourceKeys.AppStatusBarTaskStatusQueued
        };
        return _localizationService.GetRequiredString(resourceKey);
    }

    private static string ResolveStatusGlyph(BackgroundWorkTaskStatus status) =>
        status switch
        {
            BackgroundWorkTaskStatus.Running => "\uE171",
            BackgroundWorkTaskStatus.CancellationRequested => "\uE6D3",
            _ => "\uE508"
        };

    private static string ResolveProfileTitleKey(BackgroundWorkProfile profile) =>
        profile switch
        {
            BackgroundWorkProfile.WorkspaceOpen => ResourceKeys.AppStatusBarTaskProfileWorkspaceOpen,
            BackgroundWorkProfile.WorkspaceImport => ResourceKeys.AppStatusBarTaskProfileWorkspaceImport,
            BackgroundWorkProfile.PreviewForeground => ResourceKeys.AppStatusBarTaskProfilePreviewForeground,
            BackgroundWorkProfile.PreviewPrewarm => ResourceKeys.AppStatusBarTaskProfilePreviewPrewarm,
            BackgroundWorkProfile.SpineRuntimeFrame => ResourceKeys.AppStatusBarTaskProfileSpineRuntimeFrame,
            BackgroundWorkProfile.TransientRead => ResourceKeys.AppStatusBarTaskProfileTransientRead,
            BackgroundWorkProfile.SingleExtraction => ResourceKeys.AppStatusBarTaskProfileSingleExtraction,
            BackgroundWorkProfile.BatchExtraction => ResourceKeys.AppStatusBarTaskProfileBatchExtraction,
            BackgroundWorkProfile.BatchReplacement => ResourceKeys.AppStatusBarTaskProfileBatchReplacement,
            BackgroundWorkProfile.Export => ResourceKeys.AppStatusBarTaskProfileExport,
            BackgroundWorkProfile.SaveRebuild => ResourceKeys.AppStatusBarTaskProfileSaveRebuild,
            BackgroundWorkProfile.Maintenance => ResourceKeys.AppStatusBarTaskProfileMaintenance,
            _ => ResourceKeys.AppStatusBarBackgroundTaskFallbackText
        };
}
