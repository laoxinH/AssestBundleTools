using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Dispatching;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal sealed class WorkspaceSaveExecutionDiagnosticLogBridge : IProgress<WorkspaceSaveExecutionDiagnosticDto>
{
    private readonly IAppLogService _appLogService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly ILocalizationService _localizationService;

    public WorkspaceSaveExecutionDiagnosticLogBridge(
        IAppLogService appLogService,
        DispatcherQueue dispatcherQueue,
        ILocalizationService localizationService)
    {
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _dispatcherQueue = dispatcherQueue ?? throw new ArgumentNullException(nameof(dispatcherQueue));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public void Report(WorkspaceSaveExecutionDiagnosticDto value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var message = FormatMessage(value);
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _dispatcherQueue.TryEnqueue(() => _appLogService.AppendInfo(message));
    }

    private string? FormatMessage(WorkspaceSaveExecutionDiagnosticDto value) =>
        value.Kind switch
        {
            WorkspaceSaveExecutionDiagnosticKindDto.BundleWritePlan => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogLogBundleWritePlanFormat,
                value.TotalBundleCount,
                value.BundleWriteConcurrency ?? 0),
            WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteStarted => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogLogBundleWriteStartedFormat,
                value.BundleDisplayName ?? value.BundleId ?? string.Empty,
                value.ActiveBundleWriteCount ?? 0,
                value.BundleWriteConcurrency ?? 0,
                value.ManagedThreadId ?? 0,
                value.OutputFilePath ?? string.Empty),
            WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteFinished => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogLogBundleWriteFinishedFormat,
                value.BundleDisplayName ?? value.BundleId ?? string.Empty,
                FormatStatus(value.Status),
                value.ElapsedMilliseconds ?? 0,
                value.ActiveBundleWriteCount ?? 0,
                value.BundleWriteConcurrency ?? 0),
            WorkspaceSaveExecutionDiagnosticKindDto.BundleWriteSkipped => _localizationService.GetFormattedString(
                ResourceKeys.WorkspaceExportDialogLogBundleWriteSkippedFormat,
                value.BundleDisplayName ?? value.BundleId ?? string.Empty,
                value.DiagnosticCode ?? string.Empty,
                value.CompletedBundleCount ?? 0),
            _ => null
        };

    private string FormatStatus(string? status) =>
        status switch
        {
            nameof(WorkspaceSaveBundleExecutionStatusDto.Succeeded) => _localizationService.GetRequiredString(
                ResourceKeys.WorkspaceExportDialogLogStatusSucceeded),
            nameof(WorkspaceSaveBundleExecutionStatusDto.Failed) => _localizationService.GetRequiredString(
                ResourceKeys.WorkspaceExportDialogLogStatusFailed),
            "Cancelled" => _localizationService.GetRequiredString(
                ResourceKeys.WorkspaceExportDialogLogStatusCancelled),
            _ => _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogLogStatusUnknown)
        };
}
