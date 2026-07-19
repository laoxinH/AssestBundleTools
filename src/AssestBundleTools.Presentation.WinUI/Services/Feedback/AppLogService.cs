using AssestBundleTools.Application.Common.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 持有应用范围内共享的日志面板状态。
/// </summary>
/// <remarks>
/// 日志级别同步进入共享展示模型，用于日志中心筛选与复制；
/// 工作区文件副本仍由诊断日志存储抽象独立维护。
/// </remarks>
public sealed class AppLogService : IAppLogService
{
    private readonly IWorkspaceDiagnosticLogStore? _workspaceDiagnosticLogStore;
    private readonly Func<string?>? _workspaceDataRootPathProvider;

    public AppLogService(
        ILocalizationService localizationService,
        IWorkspaceDiagnosticLogStore? workspaceDiagnosticLogStore = null,
        Func<string?>? workspaceDataRootPathProvider = null)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        _workspaceDiagnosticLogStore = workspaceDiagnosticLogStore;
        _workspaceDataRootPathProvider = workspaceDataRootPathProvider;

        LogPanel = new LogPanelViewModel
        {
            EmptyStateTitleText = localizationService.GetRequiredString(ResourceKeys.DiagnosticsLogEmptyStateTitle),
            EmptyStateText = localizationService.GetRequiredString(ResourceKeys.DiagnosticsLogEmptyState),
            FilterEmptyStateTitleText = localizationService.GetRequiredString(ResourceKeys.DiagnosticsLogFilterEmptyStateTitle),
            FilterEmptyStateText = localizationService.GetRequiredString(ResourceKeys.DiagnosticsLogFilterEmptyState)
        };
    }

    public LogPanelViewModel LogPanel { get; }

    public void Clear() => LogPanel.Clear();

    public void AppendInfo(string message) => Append(message, WorkspaceDiagnosticLogLevel.Information);

    public void AppendWarning(string message) => Append(message, WorkspaceDiagnosticLogLevel.Warning);

    public void AppendError(string message) => Append(message, WorkspaceDiagnosticLogLevel.Error);

    private void Append(string message, WorkspaceDiagnosticLogLevel level)
    {
        LogPanel.Append(message, level);

        var workspaceDataRootPath = _workspaceDataRootPathProvider?.Invoke();
        if (_workspaceDiagnosticLogStore is null || string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return;
        }

        _workspaceDiagnosticLogStore.Append(
            workspaceDataRootPath,
            new WorkspaceDiagnosticLogEntryDto(DateTimeOffset.Now, level, message));
    }
}

