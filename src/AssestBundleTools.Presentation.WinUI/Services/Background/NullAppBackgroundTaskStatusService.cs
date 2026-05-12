using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.Services;

public sealed class NullAppBackgroundTaskStatusService : IAppBackgroundTaskStatusService
{
    public static NullAppBackgroundTaskStatusService Instance { get; } = new();

    private NullAppBackgroundTaskStatusService()
    {
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged
    {
        add { }
        remove { }
    }

    public string StatusText => string.Empty;

    public string ProgressText => string.Empty;

    public double ProgressValue => 0d;

    public bool IsProgressIndeterminate => false;

    public Visibility ProgressVisibility => Visibility.Collapsed;

    public string StatusGlyph => string.Empty;

    public IAppBackgroundTaskHandle BeginTask(string title, int? totalCount = null) =>
        NullAppBackgroundTaskHandle.Instance;

    private sealed class NullAppBackgroundTaskHandle : IAppBackgroundTaskHandle
    {
        public static NullAppBackgroundTaskHandle Instance { get; } = new();

        private NullAppBackgroundTaskHandle()
        {
        }

        public void ReportProgress(int completedCount, int totalCount, string? detail = null)
        {
        }

        public void ReportIndeterminate(string? detail = null)
        {
        }

        public void Dispose()
        {
        }
    }
}
