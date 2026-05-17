using System.ComponentModel;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

public interface IAppBackgroundTaskStatusService : INotifyPropertyChanged
{
    string StatusText { get; }

    string ProgressText { get; }

    double ProgressValue { get; }

    bool IsProgressIndeterminate { get; }

    Visibility ProgressVisibility { get; }

    string StatusGlyph { get; }

    bool CanCancelCurrentTask => false;

    Visibility CancelVisibility => Visibility.Collapsed;

    IAppBackgroundTaskHandle BeginTask(string title, int? totalCount = null);

    IAppBackgroundTaskHandle BeginTask(BackgroundTaskStatusRequest request) =>
        BeginTask(request.Title, request.TotalCount);

    void CancelCurrentTask()
    {
    }
}

public interface IAppBackgroundTaskHandle : IDisposable
{
    void ReportProgress(int completedCount, int totalCount, string? detail = null);

    void ReportIndeterminate(string? detail = null);
}

public sealed record BackgroundTaskStatusRequest(
    string Title,
    int? TotalCount = null,
    Guid? TaskId = null,
    Action? CancelAction = null);
