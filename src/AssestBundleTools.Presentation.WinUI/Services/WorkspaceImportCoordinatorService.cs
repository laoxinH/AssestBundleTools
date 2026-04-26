using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 将“文件导入 / 目录导入”入口统一收口到 Presentation 协调层。
/// </summary>
/// <remarks>
/// 这里负责把 Presentation 层的“选文件 / 选目录 / 拖拽导入”统一接到应用层：
/// 1. 校验活动工作区；
/// 2. 收集导入来源；
/// 3. 登记来源并触发真实导入处理；
/// 4. 统一刷新最近工作区上下文、日志与 Toast。
/// </remarks>
public sealed class WorkspaceImportCoordinatorService : IWorkspaceImportCoordinatorService
{
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IWorkspaceBrowserService _workspaceBrowserService;
    private readonly IFilePickerService _filePickerService;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;

    public WorkspaceImportCoordinatorService(
        IWorkspaceContextService workspaceContextService,
        IWorkspaceBrowserService workspaceBrowserService,
        IFilePickerService filePickerService,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _workspaceBrowserService = workspaceBrowserService ?? throw new ArgumentNullException(nameof(workspaceBrowserService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public bool HasActiveWorkspace => _workspaceContextService.CurrentWorkspaceSummary is not null;

    public async Task<bool> RequestBundleFileImportAsync(CancellationToken cancellationToken = default)
    {
        if (!_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) ||
            _workspaceContextService.CurrentWorkspaceSummary is not { } workspaceSummary)
        {
            return false;
        }

        var selectedPaths = await _filePickerService.PickBundleFilePathsAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (selectedPaths.Count == 0)
        {
            return false;
        }

        return await RegisterImportAsync(
            workspaceEntry!,
            workspaceSummary,
            WorkspaceImportOriginKind.BundleFiles,
            selectedPaths,
            cancellationToken);
    }

    public async Task<bool> RequestBundleDirectoryImportAsync(CancellationToken cancellationToken = default)
    {
        if (!_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) ||
            _workspaceContextService.CurrentWorkspaceSummary is not { } workspaceSummary)
        {
            return false;
        }

        var selectedDirectoryPath = await _filePickerService.PickBundleImportDirectoryAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(selectedDirectoryPath))
        {
            return false;
        }

        return await RegisterImportAsync(
            workspaceEntry!,
            workspaceSummary,
            WorkspaceImportOriginKind.BundleDirectory,
            new[] { selectedDirectoryPath },
            cancellationToken);
    }

    public async Task<bool> RequestDragDropImportAsync(
        IReadOnlyList<string> droppedPaths,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) ||
            _workspaceContextService.CurrentWorkspaceSummary is not { } workspaceSummary)
        {
            return false;
        }

        var normalizedPaths = NormalizePaths(droppedPaths);
        if (normalizedPaths.Count == 0)
        {
            return false;
        }

        return await RegisterImportAsync(
            workspaceEntry!,
            workspaceSummary,
            WorkspaceImportOriginKind.DragDrop,
            normalizedPaths,
            cancellationToken);
    }

    private async Task<bool> RegisterImportAsync(
        WorkspaceContextEntry workspaceEntry,
        WorkspaceSummaryDto workspaceSummary,
        WorkspaceImportOriginKind importOrigin,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
    {
        var result = await _workspaceBrowserService.RegisterWorkspaceImportAsync(
            new RegisterWorkspaceImportCommand(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                WorkspaceDisplayName: workspaceSummary.DisplayName,
                WorkspaceSourcePath: workspaceSummary.SourcePath,
                ImportOrigin: importOrigin,
                Paths: paths),
            cancellationToken);

        if (result.IsCancelled)
        {
            return false;
        }

        if (!result.IsSuccess || result.Value is null)
        {
            AnnounceFailure(result.Errors);
            return false;
        }

        var processingResult = await _workspaceBrowserService.ProcessWorkspaceImportsAsync(
            new ProcessWorkspaceImportsCommand(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath),
            cancellationToken);
        if (processingResult.IsCancelled)
        {
            return false;
        }

        if (!processingResult.IsSuccess || processingResult.Value is null)
        {
            AnnounceFailure(processingResult.Errors);
            return false;
        }

        // 导入处理可能已经把“自动识别”解析成真实解密器，需要先刷新工作区目录项，
        // 这样随后页面重载时才能拿到最新的工作区级解密器状态。
        _workspaceContextService.ReloadWorkspaceCatalogEntries();
        AnnounceSuccess(result.Value, processingResult.Value);
        return true;
    }

    private void AnnounceSuccess(
        WorkspaceImportRegistrationResultDto result,
        WorkspaceImportProcessingResultDto processingResult)
    {
        var logMessage = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceImportLogProcessedFormat,
            result.WorkspaceDisplayName,
            processingResult.RegisteredSourceCount,
            processingResult.DiscoveredFileCount,
            processingResult.ScreenedOutFileCount,
            processingResult.IndexedBundleCount,
            processingResult.ReusedBundleCount,
            processingResult.SkippedFileCount,
            processingResult.FailedFileCount);
        var notificationMessage = _localizationService.GetFormattedString(
            ResourceKeys.WorkspaceImportNotificationProcessedFormat,
            result.WorkspaceDisplayName,
            processingResult.ScreenedOutFileCount,
            processingResult.IndexedBundleCount,
            processingResult.ReusedBundleCount,
            processingResult.SkippedFileCount,
            processingResult.FailedFileCount);
        var hasIssues = processingResult.ScreenedOutFileCount > 0 ||
                        processingResult.SkippedFileCount > 0 ||
                        processingResult.FailedFileCount > 0;

        _appLogService.AppendInfo(logMessage);
        AppendAutoDetectionDiagnostics(result.WorkspaceDisplayName, processingResult);
        AppendCacheDiagnostics(processingResult);
        AppendIssueDiagnostics(processingResult);
        _appNotificationService.Show(
            _localizationService.GetRequiredString(
                hasIssues
                    ? ResourceKeys.AppNotificationWarningTitle
                    : ResourceKeys.AppNotificationSuccessTitle),
            notificationMessage,
            hasIssues
                ? AppNotificationSeverity.Warning
                : AppNotificationSeverity.Success);
    }

    private void AnnounceFailure(IReadOnlyList<AppError> errors)
    {
        var firstError = errors.FirstOrDefault();
        if (firstError is null)
        {
            return;
        }

        var message = AppErrorDisplayFormatter.Format(firstError, _localizationService);
        _appLogService.AppendWarning(message);
        _appNotificationService.Show(
            _localizationService.GetRequiredString(MapNotificationTitle(firstError.Severity)),
            message,
            MapNotificationSeverity(firstError.Severity));
    }

    private void AppendAutoDetectionDiagnostics(
        string workspaceDisplayName,
        WorkspaceImportProcessingResultDto processingResult)
    {
        if (!processingResult.UsedAutoDetection || processingResult.AutoDetectionDiagnostic is null)
        {
            return;
        }

        var diagnostic = processingResult.AutoDetectionDiagnostic;
        var summaryResourceKey = diagnostic.UsedFallbackDecryptor
            ? ResourceKeys.WorkspaceImportLogAutoDetectionFallbackSummaryFormat
            : ResourceKeys.WorkspaceImportLogAutoDetectionSummaryFormat;
        var resolvedDecryptorName = AssetBundleDecryptorDisplayText.GetDisplayName(
            _localizationService,
            processingResult.ResolvedDecryptorId);

        _appLogService.AppendInfo(_localizationService.GetFormattedString(
            summaryResourceKey,
            workspaceDisplayName,
            resolvedDecryptorName,
            diagnostic.ProbeDiagnostics.Count,
            diagnostic.DiscoveredFileCount));

        foreach (var probeDiagnostic in diagnostic.ProbeDiagnostics)
        {
            var selectedSuffix = probeDiagnostic.WasSelected
                ? _localizationService.GetRequiredString(ResourceKeys.WorkspaceImportLogAutoDetectionProbeSelectedSuffix)
                : string.Empty;

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.WorkspaceImportLogAutoDetectionProbeFormat,
                probeDiagnostic.DecryptorDisplayName,
                probeDiagnostic.DecryptorId,
                probeDiagnostic.MatchedFileCount,
                probeDiagnostic.SuccessfulProbeCount,
                probeDiagnostic.TotalConfidence,
                selectedSuffix));

            foreach (var sampleMessage in probeDiagnostic.SampleMessages)
            {
                _appLogService.AppendInfo(_localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogAutoDetectionProbeMessageFormat,
                    probeDiagnostic.DecryptorDisplayName,
                    sampleMessage));
            }
        }
    }

    private void AppendCacheDiagnostics(WorkspaceImportProcessingResultDto processingResult)
    {
        foreach (var cacheDiagnostic in processingResult.CacheDiagnostics)
        {
            var message = cacheDiagnostic.Kind switch
            {
                WorkspaceImportCacheDecisionKind.SourceFingerprintChanged => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogCacheSourceFingerprintChangedFormat,
                    cacheDiagnostic.SourcePath,
                    cacheDiagnostic.LogicalPath,
                    cacheDiagnostic.Detail ?? string.Empty),
                WorkspaceImportCacheDecisionKind.DecryptorChanged => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogCacheDecryptorChangedFormat,
                    cacheDiagnostic.SourcePath,
                    cacheDiagnostic.LogicalPath,
                    cacheDiagnostic.Detail ?? string.Empty),
                WorkspaceImportCacheDecisionKind.DecryptorVersionChanged => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogCacheDecryptorVersionChangedFormat,
                    cacheDiagnostic.SourcePath,
                    cacheDiagnostic.LogicalPath,
                    cacheDiagnostic.Detail ?? string.Empty),
                WorkspaceImportCacheDecisionKind.ToolVersionChanged => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogCacheToolVersionChangedFormat,
                    cacheDiagnostic.SourcePath,
                    cacheDiagnostic.LogicalPath,
                    cacheDiagnostic.Detail ?? string.Empty),
                WorkspaceImportCacheDecisionKind.ParserVersionChanged => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogCacheParserVersionChangedFormat,
                    cacheDiagnostic.SourcePath,
                    cacheDiagnostic.LogicalPath,
                    cacheDiagnostic.Detail ?? string.Empty),
                WorkspaceImportCacheDecisionKind.SourceRemoved => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogCacheSourceRemovedFormat,
                    cacheDiagnostic.SourcePath,
                    cacheDiagnostic.LogicalPath),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            _appLogService.AppendInfo(message);
        }
    }

    private void AppendIssueDiagnostics(WorkspaceImportProcessingResultDto processingResult)
    {
        foreach (var issueDiagnostic in processingResult.IssueDiagnostics)
        {
            var message = issueDiagnostic.Kind switch
            {
                WorkspaceImportIssueKind.CandidateScreenedOut => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogIssueCandidateScreenedOutFormat,
                    issueDiagnostic.SourcePath,
                    issueDiagnostic.LogicalPath,
                    issueDiagnostic.Detail ?? string.Empty),
                WorkspaceImportIssueKind.DecryptionFailed => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogIssueDecryptionFailedFormat,
                    issueDiagnostic.SourcePath,
                    issueDiagnostic.LogicalPath,
                    ResolveDecryptorDisplayName(issueDiagnostic.DecryptorId)),
                WorkspaceImportIssueKind.ReaderRejected => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogIssueReaderRejectedFormat,
                    issueDiagnostic.SourcePath,
                    issueDiagnostic.LogicalPath,
                    ResolveDecryptorDisplayName(issueDiagnostic.DecryptorId),
                    issueDiagnostic.Detail ?? string.Empty),
                WorkspaceImportIssueKind.ReadFailed => _localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogIssueReadFailedFormat,
                    issueDiagnostic.SourcePath,
                    issueDiagnostic.LogicalPath,
                    ResolveDecryptorDisplayName(issueDiagnostic.DecryptorId)),
                _ => issueDiagnostic.Detail ?? issueDiagnostic.SourcePath
            };

            switch (issueDiagnostic.Kind)
            {
                case WorkspaceImportIssueKind.CandidateScreenedOut:
                case WorkspaceImportIssueKind.ReaderRejected:
                    _appLogService.AppendWarning(message);
                    break;
                default:
                    _appLogService.AppendError(message);
                    break;
            }

            foreach (var error in issueDiagnostic.Errors)
            {
                _appLogService.AppendError(_localizationService.GetFormattedString(
                    ResourceKeys.WorkspaceImportLogIssueErrorDetailFormat,
                    AppErrorDisplayFormatter.Format(error, _localizationService),
                    error.Code));
            }
        }
    }

    private string ResolveDecryptorDisplayName(string? decryptorId)
    {
        return AssetBundleDecryptorDisplayText.GetDisplayName(_localizationService, decryptorId);
    }

    private static IReadOnlyList<string> NormalizePaths(IReadOnlyList<string> paths)
    {
        return paths
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string MapNotificationTitle(ErrorSeverity severity) =>
        severity >= ErrorSeverity.Error
            ? ResourceKeys.AppNotificationErrorTitle
            : ResourceKeys.AppNotificationWarningTitle;

    private static AppNotificationSeverity MapNotificationSeverity(ErrorSeverity severity) =>
        severity switch
        {
            ErrorSeverity.Critical => AppNotificationSeverity.Error,
            ErrorSeverity.Error => AppNotificationSeverity.Error,
            _ => AppNotificationSeverity.Warning
        };
}
