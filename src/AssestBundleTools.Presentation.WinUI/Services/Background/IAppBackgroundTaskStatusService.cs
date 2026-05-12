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

    IAppBackgroundTaskHandle BeginTask(string title, int? totalCount = null);
}

public interface IAppBackgroundTaskHandle : IDisposable
{
    void ReportProgress(int completedCount, int totalCount, string? detail = null);

    void ReportIndeterminate(string? detail = null);
}
