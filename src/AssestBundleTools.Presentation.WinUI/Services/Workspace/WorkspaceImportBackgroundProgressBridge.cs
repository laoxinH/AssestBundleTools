using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将 Application 导入进度同时投影给局部导入遮罩和统一后台任务状态条。
/// </summary>
internal sealed class WorkspaceImportBackgroundProgressBridge : IProgress<WorkspaceImportProgressDto>
{
    private readonly IProgress<WorkspaceImportProgressDto>? _innerProgress;
    private readonly IBackgroundWorkProgress _backgroundProgress;
    private readonly ILocalizationService _localizationService;

    public WorkspaceImportBackgroundProgressBridge(
        IProgress<WorkspaceImportProgressDto>? innerProgress,
        IBackgroundWorkProgress backgroundProgress,
        ILocalizationService localizationService)
    {
        _innerProgress = innerProgress;
        _backgroundProgress = backgroundProgress ?? throw new ArgumentNullException(nameof(backgroundProgress));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public void Report(WorkspaceImportProgressDto value)
    {
        _innerProgress?.Report(value);

        if (value.Stage == WorkspaceImportProgressStage.ProcessingBundles &&
            value.TotalBundleCount > 0)
        {
            _backgroundProgress.ReportProgress(
                value.CompletedBundleCount,
                value.TotalBundleCount,
                FormatDetail(value));
            return;
        }

        _backgroundProgress.ReportIndeterminate(FormatStage(value.Stage));
    }

    private string FormatDetail(WorkspaceImportProgressDto progress)
    {
        if (!string.IsNullOrWhiteSpace(progress.CurrentBundleLogicalPath))
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceImportBusyOverlayProgressCurrentBundleFormat,
                progress.CurrentBundleLogicalPath);
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceImportBusyOverlayProgressProcessingFormat,
            progress.CompletedBundleCount,
            progress.TotalBundleCount);
    }

    private string FormatStage(WorkspaceImportProgressStage stage) =>
        stage switch
        {
            WorkspaceImportProgressStage.Committing => GetString(ResourceKeys.WorkspaceImportBusyOverlayProgressCommitting),
            WorkspaceImportProgressStage.Completed => GetString(ResourceKeys.WorkspaceImportBusyOverlayProgressCompleted),
            _ => GetString(ResourceKeys.WorkspaceImportBusyOverlayProgressPreparing)
        };

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
