using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;
using Microsoft.UI.Dispatching;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed partial class BundleExplorerWorkspaceExportCoordinator
{
    private sealed class WorkspaceSaveExecutionProgressBridge : IProgress<WorkspaceSaveExecutionProgressDto>
    {
        private readonly IBackgroundWorkProgress _progress;
        private readonly WorkspaceExportDialogViewModel _viewModel;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly ILocalizationService _localizationService;

        public WorkspaceSaveExecutionProgressBridge(
            IBackgroundWorkProgress progress,
            WorkspaceExportDialogViewModel viewModel,
            DispatcherQueue dispatcherQueue,
            ILocalizationService localizationService)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
            _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        public void Report(WorkspaceSaveExecutionProgressDto value)
        {
            ArgumentNullException.ThrowIfNull(value);

            var totalCount = Math.Max(1, value.TotalBundleCount);
            var completedCount = Math.Clamp(value.CompletedBundleCount, 0, totalCount);
            var detail = value.Stage == WorkspaceSaveExecutionStageDto.PostExportPluginLaunching
                ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogStatusPostPluginLaunching)
                : string.IsNullOrWhiteSpace(value.CurrentBundleDisplayName)
                    ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogStatusPreparing)
                    : _localizationService.GetFormattedString(
                        ResourceKeys.WorkspaceExportDialogStatusBundleFormat,
                        value.CurrentBundleDisplayName,
                        value.SucceededBundleCount,
                        value.FailedBundleCount);
            _progress.ReportProgress(completedCount, totalCount, detail);
            _dispatcherQueue.TryEnqueue(() =>
                _viewModel.ApplyExecutionProgress(completedCount, totalCount, detail));
        }
    }
}
