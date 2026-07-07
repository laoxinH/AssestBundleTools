using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class WorkspaceExportFailureDisplayFormatter
{
    private readonly ILocalizationService _localizationService;

    public WorkspaceExportFailureDisplayFormatter(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string Format(WorkspaceSaveBundleExecutionResultDto failedBundle)
    {
        ArgumentNullException.ThrowIfNull(failedBundle);

        var reason = ResolveReason(failedBundle.DiagnosticCode, failedBundle.DiagnosticDetail);
        return _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogFailureDetailFormat,
            failedBundle.BundleDisplayName,
            reason);
    }

    public bool TryFormat(AppError error, out string message)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (!string.Equals(error.Code, "Bundle.Save.OutputBlocked", StringComparison.Ordinal))
        {
            message = string.Empty;
            return false;
        }

        var diagnosticCode = GetParameter(error, "DiagnosticCode");
        if (!string.IsNullOrWhiteSpace(diagnosticCode) &&
            TryResolveKnownOutputReason(diagnosticCode, out var reason))
        {
            message = reason;
            return true;
        }

        message = _localizationService.GetFormattedString(
            error.MessageToken,
            GetParameter(error, "OutputPath") ?? string.Empty);
        return true;
    }

    private string ResolveReason(string? diagnosticCode, string? diagnosticDetail)
    {
        var combined = string.Join(
            ";",
            new[] { diagnosticCode, diagnosticDetail }
                .Where(static value => !string.IsNullOrWhiteSpace(value)));

        if (TryResolveKnownOutputReason(combined, out var reason))
        {
            return reason;
        }

        return _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceExportDialogOutputUnknownFailureFormat,
            string.IsNullOrWhiteSpace(diagnosticCode) ? "-" : diagnosticCode,
            string.IsNullOrWhiteSpace(diagnosticDetail) ? "-" : diagnosticDetail);
    }

    private bool TryResolveKnownOutputReason(string value, out string reason)
    {
        if (ContainsDiagnostic(value, "Save.Output.FileAlreadyExists"))
        {
            reason = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogOutputFileAlreadyExists);
            return true;
        }

        if (ContainsDiagnostic(value, "Save.Output.TargetIsDirectory"))
        {
            reason = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogOutputTargetIsDirectory);
            return true;
        }

        if (ContainsDiagnostic(value, "Save.Output.DirectoryBlockedByFile"))
        {
            reason = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogOutputDirectoryBlockedByFile);
            return true;
        }

        if (ContainsDiagnostic(value, "Save.Output.InvalidPath"))
        {
            reason = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogOutputInvalidPath);
            return true;
        }

        if (ContainsDiagnostic(value, "Save.Output.ProbeFailed"))
        {
            reason = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogOutputProbeFailed);
            return true;
        }

        if (ContainsDiagnostic(value, "Save.BundleWrite.AudioPayloadContainerMismatch"))
        {
            reason = _localizationService.GetRequiredString(ResourceKeys.WorkspaceExportDialogAudioPayloadContainerMismatch);
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static string? GetParameter(AppError error, string key) =>
        error.Parameters.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool ContainsDiagnostic(string value, string diagnosticCode) =>
        value.Contains(diagnosticCode, StringComparison.OrdinalIgnoreCase);
}
