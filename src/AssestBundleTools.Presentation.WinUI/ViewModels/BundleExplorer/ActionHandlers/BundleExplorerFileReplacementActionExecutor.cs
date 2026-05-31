using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 负责文件来源替换的选择、候选校验和立即应用。
/// </summary>
internal sealed class BundleExplorerFileReplacementActionExecutor
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;
    private readonly IFilePickerService _filePickerService;

    public BundleExplorerFileReplacementActionExecutor(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService,
        IFilePickerService filePickerService)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
    }

    public async Task<FileReplacementCandidateDto?> PickFileReplacementCandidateAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDetailsDto details,
        Action<IReadOnlyList<AppError>> appendResultFailure) =>
        await PickFileReplacementCandidateAsync(
            workspaceSummary,
            details.BundleId,
            details.AssetId,
            details.AssetType,
            details.Name,
            appendResultFailure);

    public async Task<FileReplacementCandidateDto?> PickFileReplacementCandidateAsync(
        WorkspaceSummaryDto workspaceSummary,
        BundleId targetBundleId,
        AssetId targetAssetId,
        AssetType targetAssetType,
        string targetAssetName,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        string? selectedFilePath;
        try
        {
            selectedFilePath = await _filePickerService.PickReplacementSourceFileAsync(targetAssetType);
        }
        catch (Exception ex)
        {
            _appLogService.AppendError(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFilePickerOpenFailedFormat,
                ex.GetType().Name));
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationErrorTitle),
                GetString(ResourceKeys.BundleExplorerNotificationFilePickerOpenFailed),
                AppNotificationSeverity.Error);
            return null;
        }

        if (string.IsNullOrWhiteSpace(selectedFilePath))
        {
            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogFileReplacementSelectionCancelled,
                targetAssetName));
            return null;
        }

        var result = await _workspaceAssetOperationGateway.GetFileReplacementCandidateAsync(
            new GetFileReplacementCandidateQuery(
                workspaceSummary.WorkspaceId,
                targetBundleId,
                targetAssetId,
                selectedFilePath));

        return TryGetValue(result, out var candidate, appendResultFailure)
            ? candidate
            : null;
    }

    public async Task<FileReplacementCandidateDto?> GetFileReplacementCandidateAsync(
        WorkspaceSummaryDto workspaceSummary,
        BundleId targetBundleId,
        AssetId targetAssetId,
        string sourceFilePath,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(targetBundleId);
        ArgumentNullException.ThrowIfNull(targetAssetId);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return null;
        }

        var result = await _workspaceAssetOperationGateway.GetFileReplacementCandidateAsync(
            new GetFileReplacementCandidateQuery(
                workspaceSummary.WorkspaceId,
                targetBundleId,
                targetAssetId,
                sourceFilePath));

        return TryGetValue(result, out var candidate, appendResultFailure)
            ? candidate
            : null;
    }

    public async Task<bool> ApplyFileReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDetailsDto details,
        FileReplacementCandidateDto candidate,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        ReplacementOperationContextDto? operationContext = null) =>
        await ApplyFileReplacementAsync(
            workspaceSummary,
            details.BundleId,
            details.AssetId,
            details.Name,
            candidate,
            appendResultFailure,
            operationContext);

    public async Task<bool> ApplyFileReplacementAsync(
        WorkspaceSummaryDto workspaceSummary,
        BundleId targetBundleId,
        AssetId targetAssetId,
        string targetAssetName,
        FileReplacementCandidateDto candidate,
        Action<IReadOnlyList<AppError>> appendResultFailure,
        ReplacementOperationContextDto? operationContext = null)
    {
        var fileResult = await _workspaceAssetOperationGateway.ReplaceAssetFromFileAsync(
            new ReplaceAssetFromFileCommand(
                workspaceSummary.WorkspaceId,
                targetBundleId,
                targetAssetId,
                candidate.SourceFilePath,
                candidate.SourceAssetType,
                workspaceSummary.WorkspaceKey,
                workspaceSummary.WorkspaceDataRootPath,
                operationContext));

        if (!TryGetValue(fileResult, out var fileReplacementResult, appendResultFailure))
        {
            return false;
        }

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogFileReplacementApplied,
            fileReplacementResult.TargetAssetName,
            fileReplacementResult.SourceFileName,
            fileReplacementResult.SourceFilePath));

        ShowOperationNotification(
            GetString(ResourceKeys.AppNotificationSuccessTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationFileReplacementApplied,
                    string.IsNullOrWhiteSpace(fileReplacementResult.TargetAssetName)
                        ? targetAssetName
                        : fileReplacementResult.TargetAssetName),
                AppNotificationSeverity.Success);

        return true;
    }

    private bool TryGetValue<T>(
        Result<T> result,
        [NotNullWhen(true)] out T? value,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            value = result.Value;
            return true;
        }

        value = default;
        if (result.IsCancelled)
        {
            return false;
        }

        appendResultFailure(result.Errors);
        return false;
    }

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
