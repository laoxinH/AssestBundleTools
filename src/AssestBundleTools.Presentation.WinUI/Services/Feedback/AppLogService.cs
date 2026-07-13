using AssestBundleTools.Application.Common.Diagnostics;
using AssestBundleTools.Presentation.WinUI.Localization;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 持有应用范围内共享的日志面板状态。
/// </summary>
/// <remarks>
/// 当前阶段先复用既有日志面板 ViewModel 作为日志中心的展示模型，
/// 后续如果补充日志级别筛选、导出、持久化，也只需要继续扩展这一层。
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
            EmptyStateText = localizationService.GetRequiredString(ResourceKeys.DiagnosticsLogEmptyState)
        };
    }

    public LogPanelViewModel LogPanel { get; }

    public void Clear() => LogPanel.Clear();

    public void AppendInfo(string message) => Append(message, WorkspaceDiagnosticLogLevel.Information);

    public void AppendWarning(string message) => Append(message, WorkspaceDiagnosticLogLevel.Warning);

    public void AppendError(string message) => Append(message, WorkspaceDiagnosticLogLevel.Error);

    private void Append(string message, WorkspaceDiagnosticLogLevel level)
    {
        LogPanel.Append(message);

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

