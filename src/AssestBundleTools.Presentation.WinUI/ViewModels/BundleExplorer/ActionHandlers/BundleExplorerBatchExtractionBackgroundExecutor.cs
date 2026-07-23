using System.IO;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 把多选提取的真实写出链路放到后台执行，并向 Shell 状态条报告进度。
/// </summary>
internal sealed class BundleExplorerBatchExtractionBackgroundExecutor
{
    private readonly IWorkspaceAssetOperationGateway _workspaceAssetOperationGateway;
    private readonly ILocalizationService _localizationService;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly IWorkspaceOperationChangeNotifier? _operationChangeNotifier;
    private readonly Func<bool> _useContentAwareSpineSkeletonFileExtensionProvider;

    public BundleExplorerBatchExtractionBackgroundExecutor(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        ILocalizationService localizationService,
        IBackgroundTaskRunner? backgroundTaskRunner,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService,
        IWorkspaceOperationChangeNotifier? operationChangeNotifier,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        Func<bool>? useContentAwareSpineSkeletonFileExtensionProvider = null)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _backgroundWorkScheduler = backgroundWorkScheduler ?? new BackgroundWorkScheduler(
            backgroundTaskRunner ?? InlineBackgroundTaskRunner.Instance,
            backgroundTaskStatusService ?? NullAppBackgroundTaskStatusService.Instance);
        _operationChangeNotifier = operationChangeNotifier;
        _useContentAwareSpineSkeletonFileExtensionProvider =
            useContentAwareSpineSkeletonFileExtensionProvider ?? (static () => false);
    }

    public async Task<BundleExplorerBatchAssetExtractionExecutionResult> ExtractAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<AssetListEntryViewModel> selectedEntries,
        string defaultBundleDisplayName,
        string selectedOutputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(selectedEntries);

        var extractionItems = selectedEntries
            .Select(entry => new AssetBatchExtractionItem(
                entry.Item.Name,
                new ExtractAssetCommand(
                    workspaceSummary.WorkspaceId,
                    entry.Item.BundleId,
                    entry.Item.AssetId,
                    workspaceSummary.WorkspaceKey,
                    workspaceSummary.WorkspaceDataRootPath,
                    BuildBatchExtractionSuggestedPath(
                        selectedOutputDirectory,
                        entry,
                        defaultBundleDisplayName))))
            .ToArray();

        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        try
        {
            return await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<BundleExplorerBatchAssetExtractionExecutionResult>(
                    BackgroundWorkProfile.BatchExtraction,
                    async context =>
            {
                var extractedResults = new List<AssetExtractionResultDto>(extractionItems.Length);
                var failures = new List<IReadOnlyList<AppError>>();

                for (var index = 0; index < extractionItems.Length; index++)
                {
                    var token = context.CancellationToken;
                    token.ThrowIfCancellationRequested();

                    var item = extractionItems[index];
                    context.Progress.ReportProgress(index, extractionItems.Length, item.DisplayName);

                    var result = await _workspaceAssetOperationGateway.ExtractAssetAsync(
                        item.Command,
                        token);

                    if (result.IsCancelled)
                    {
                        return new BundleExplorerBatchAssetExtractionExecutionResult(
                            extractedResults,
                            failures,
                            IsCancelled: true);
                    }

                    if (result.IsSuccess && result.Value is not null)
                    {
                        extractedResults.Add(result.Value);
                    }
                    else
                    {
                        failures.Add(result.Errors);
                    }

                    context.Progress.ReportProgress(index + 1, extractionItems.Length, item.DisplayName);
                }

                return new BundleExplorerBatchAssetExtractionExecutionResult(
                    extractedResults,
                    failures,
                    IsCancelled: false);
            })
            {
                SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                Priority = BackgroundWorkPriority.Visible,
                WorkspaceId = workspaceSummary.WorkspaceId.Value,
                WorkspaceKey = workspaceSummary.WorkspaceKey,
                StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedAssets),
                TotalCount = extractionItems.Length,
                CanCancel = true
            },
            cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new BundleExplorerBatchAssetExtractionExecutionResult(
                Array.Empty<AssetExtractionResultDto>(),
                Array.Empty<IReadOnlyList<AppError>>(),
                IsCancelled: true);
        }
    }

    public async Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceAssetsAsync(
        WorkspaceSummaryDto workspaceSummary,
        ExtractWorkspaceAssetsCommand command,
        Func<ExtractWorkspaceAssetsCommand, CancellationToken, Task<Result<WorkspaceBatchExtractionResultDto>>> executeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executeAsync);

        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        try
        {
            return await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceBatchExtractionResultDto>>(
                    BackgroundWorkProfile.BatchExtraction,
                    async context =>
                    {
                        var progress = new InlineProgress<WorkspaceBatchExtractionProgressDto>(
                            snapshot => ReportWorkspaceBatchProgress(context, snapshot));
                        var commandWithProgress = ApplySpineSkeletonExtensionPreference(command) with
                        {
                            Progress = progress
                        };
                        return await executeAsync(commandWithProgress, context.CancellationToken);
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = workspaceSummary.WorkspaceId.Value,
                    WorkspaceKey = workspaceSummary.WorkspaceKey,
                    StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedAssets),
                    TotalCount = command.SelectedAssetIds?.Count,
                    CanCancel = true
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }
    }

    public async Task<Result<WorkspaceBatchExtractionResultDto>> ExtractWorkspaceSpineGroupsAsync(
        WorkspaceSummaryDto workspaceSummary,
        ExtractWorkspaceAssetsCommand command,
        Func<ExtractWorkspaceAssetsCommand, CancellationToken, Task<Result<WorkspaceBatchExtractionResultDto>>> executeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(executeAsync);

        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        try
        {
            return await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<WorkspaceBatchExtractionResultDto>>(
                    BackgroundWorkProfile.BatchExtraction,
                    async context =>
                    {
                        var progress = new InlineProgress<WorkspaceBatchExtractionProgressDto>(
                            snapshot => ReportWorkspaceBatchProgress(context, snapshot));
                        var commandWithProgress = ApplySpineSkeletonExtensionPreference(command) with
                        {
                            Progress = progress
                        };
                        return await executeAsync(commandWithProgress, context.CancellationToken);
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = workspaceSummary.WorkspaceId.Value,
                    WorkspaceKey = workspaceSummary.WorkspaceKey,
                    StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedSpineGroups),
                    TotalCount = command.SelectedSpineGroupIds?.Count,
                    CanCancel = true
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBatchExtractionResultDto>.Cancelled();
        }
    }

    public async Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
        WorkspaceSummaryDto workspaceSummary,
        string displayName,
        ExtractAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(command);

        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        try
        {
            return await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<AssetExtractionResultDto>>(
                    BackgroundWorkProfile.BatchExtraction,
                    async context =>
                    {
                        context.Progress.ReportProgress(0, 1, displayName);
                        var result = await _workspaceAssetOperationGateway.ExtractAssetAsync(
                            command,
                            context.CancellationToken);
                        context.Progress.ReportProgress(1, 1, displayName);
                        return result;
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = workspaceSummary.WorkspaceId.Value,
                    WorkspaceKey = workspaceSummary.WorkspaceKey,
                    StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedAssets),
                    TotalCount = 1,
                    CanCancel = true
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<AssetExtractionResultDto>.Cancelled();
        }
    }

    public async Task<BundleExplorerBatchSpineGroupExtractionExecutionResult> ExtractSpineGroupsAsync(
        WorkspaceSummaryDto workspaceSummary,
        IReadOnlyList<SpineAssetGroupEntryViewModel> selectedEntries,
        string defaultBundleDisplayName,
        string selectedOutputDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(selectedEntries);

        var extractionItems = selectedEntries
            .Select(entry => new SpineGroupBatchExtractionItem(
                entry.DisplayName,
                new ExtractSpineAssetGroupCommand(
                    workspaceSummary.WorkspaceId,
                    workspaceSummary.WorkspaceKey ?? string.Empty,
                    workspaceSummary.WorkspaceDataRootPath ?? string.Empty,
                    entry.Item.BundleId,
                    entry.Item.GroupId,
                    SpineBatchExtractionOutputDirectoryResolver.Build(
                        selectedOutputDirectory,
                        entry,
                        defaultBundleDisplayName),
                    _useContentAwareSpineSkeletonFileExtensionProvider())))
            .ToArray();

        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        try
        {
            return await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<BundleExplorerBatchSpineGroupExtractionExecutionResult>(
                    BackgroundWorkProfile.BatchExtraction,
                    async context =>
            {
                var extractionResults = new List<SpineAssetGroupExtractionResultDto>(extractionItems.Length);
                var failures = new List<IReadOnlyList<AppError>>();

                for (var index = 0; index < extractionItems.Length; index++)
                {
                    var token = context.CancellationToken;
                    token.ThrowIfCancellationRequested();

                    var item = extractionItems[index];
                    context.Progress.ReportProgress(index, extractionItems.Length, item.DisplayName);

                    var result = await _workspaceAssetOperationGateway.ExtractSpineAssetGroupAsync(
                        item.Command,
                        token);

                    if (result.IsCancelled)
                    {
                        return new BundleExplorerBatchSpineGroupExtractionExecutionResult(
                            extractionResults,
                            failures,
                            IsCancelled: true);
                    }

                    if (result.IsSuccess && result.Value is not null)
                    {
                        extractionResults.Add(result.Value);
                    }
                    else
                    {
                        failures.Add(result.Errors);
                    }

                    context.Progress.ReportProgress(index + 1, extractionItems.Length, item.DisplayName);
                }

                return new BundleExplorerBatchSpineGroupExtractionExecutionResult(
                    extractionResults,
                    failures,
                    IsCancelled: false);
            })
            {
                SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                Priority = BackgroundWorkPriority.Visible,
                WorkspaceId = workspaceSummary.WorkspaceId.Value,
                WorkspaceKey = workspaceSummary.WorkspaceKey,
                StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedSpineGroups),
                TotalCount = extractionItems.Length,
                CanCancel = true
            },
            cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new BundleExplorerBatchSpineGroupExtractionExecutionResult(
                Array.Empty<SpineAssetGroupExtractionResultDto>(),
                Array.Empty<IReadOnlyList<AppError>>(),
                IsCancelled: true);
        }
    }

    public async Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineGroupAsync(
        WorkspaceSummaryDto workspaceSummary,
        string displayName,
        ExtractSpineAssetGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(command);

        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        try
        {
            return await _backgroundWorkScheduler.RunAsync(
                new BackgroundWorkRequest<Result<SpineAssetGroupExtractionResultDto>>(
                    BackgroundWorkProfile.BatchExtraction,
                    async context =>
                    {
                        context.Progress.ReportProgress(0, 1, displayName);
                        var result = await _workspaceAssetOperationGateway.ExtractSpineAssetGroupAsync(
                            command with
                            {
                                UseContentAwareSkeletonFileExtension =
                                    command.UseContentAwareSkeletonFileExtension ||
                                    _useContentAwareSpineSkeletonFileExtensionProvider()
                            },
                            context.CancellationToken);
                        context.Progress.ReportProgress(1, 1, displayName);
                        return result;
                    })
                {
                    SemanticKind = BackgroundWorkSemanticKind.VisibleOutput,
                    Priority = BackgroundWorkPriority.Visible,
                    WorkspaceId = workspaceSummary.WorkspaceId.Value,
                    WorkspaceKey = workspaceSummary.WorkspaceKey,
                    StatusTitle = _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedSpineGroups),
                    TotalCount = 1,
                    CanCancel = true
                },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<SpineAssetGroupExtractionResultDto>.Cancelled();
        }
    }

    private static string BuildBatchExtractionSuggestedPath(
        string selectedOutputDirectory,
        AssetListEntryViewModel entry,
        string defaultBundleDisplayName)
    {
        var fileName = string.IsNullOrWhiteSpace(entry.Item.ExportFileName)
            ? WorkspaceAssetExportNameProjection.SanitizeFileName(
                string.IsNullOrWhiteSpace(entry.Item.Name)
                    ? entry.Item.AssetId.Value
                    : entry.Item.Name)
            : entry.Item.ExportFileName;
        return Path.Combine(
            BundleBatchExtractionOutputDirectoryResolver.BuildAssetTypeDirectory(
                selectedOutputDirectory,
                defaultBundleDisplayName,
                entry.Item.BundleId,
                entry.Item.AssetTypeDisplayName,
                entry.Item.AssetType),
            fileName);
    }

    private static void ReportWorkspaceBatchProgress(
        BackgroundWorkExecutionContext context,
        WorkspaceBatchExtractionProgressDto snapshot)
    {
        var totalCount = snapshot.TotalItemCount > 0
            ? snapshot.TotalItemCount
            : Math.Max(snapshot.CompletedItemCount, 1);
        if (snapshot.Kind == WorkspaceBatchExtractionProgressKind.Preparing)
        {
            context.Progress.ReportIndeterminate(snapshot.ItemDisplayName);
            return;
        }

        context.Progress.ReportProgress(
            snapshot.CompletedItemCount,
            totalCount,
            snapshot.ItemDisplayName);
    }

    private ExtractWorkspaceAssetsCommand ApplySpineSkeletonExtensionPreference(
        ExtractWorkspaceAssetsCommand command) =>
        command with
        {
            UseContentAwareSpineSkeletonFileExtension =
                command.UseContentAwareSpineSkeletonFileExtension ||
                _useContentAwareSpineSkeletonFileExtensionProvider()
        };

    private sealed record AssetBatchExtractionItem(
        string DisplayName,
        ExtractAssetCommand Command);

    private sealed record SpineGroupBatchExtractionItem(
        string DisplayName,
        ExtractSpineAssetGroupCommand Command);

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void Report(T value) => _handler(value);
    }
}

internal sealed record BundleExplorerBatchAssetExtractionExecutionResult(
    IReadOnlyList<AssetExtractionResultDto> ExtractedResults,
    IReadOnlyList<IReadOnlyList<AppError>> Failures,
    bool IsCancelled);

internal sealed record BundleExplorerBatchSpineGroupExtractionExecutionResult(
    IReadOnlyList<SpineAssetGroupExtractionResultDto> ExtractionResults,
    IReadOnlyList<IReadOnlyList<AppError>> Failures,
    bool IsCancelled);
