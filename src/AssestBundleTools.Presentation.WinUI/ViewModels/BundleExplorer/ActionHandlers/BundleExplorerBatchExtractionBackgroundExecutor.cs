using System.IO;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
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
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IAppBackgroundTaskStatusService _backgroundTaskStatusService;
    private readonly IWorkspaceOperationChangeNotifier? _operationChangeNotifier;

    public BundleExplorerBatchExtractionBackgroundExecutor(
        IWorkspaceAssetOperationGateway workspaceAssetOperationGateway,
        ILocalizationService localizationService,
        IBackgroundTaskRunner? backgroundTaskRunner,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService,
        IWorkspaceOperationChangeNotifier? operationChangeNotifier)
    {
        _workspaceAssetOperationGateway = workspaceAssetOperationGateway ?? throw new ArgumentNullException(nameof(workspaceAssetOperationGateway));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _backgroundTaskRunner = backgroundTaskRunner ?? InlineBackgroundTaskRunner.Instance;
        _backgroundTaskStatusService = backgroundTaskStatusService ?? NullAppBackgroundTaskStatusService.Instance;
        _operationChangeNotifier = operationChangeNotifier;
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

        using var taskHandle = _backgroundTaskStatusService.BeginTask(
            _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedAssets),
            extractionItems.Length);
        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        return await _backgroundTaskRunner.RunAsync(
            async token =>
            {
                var extractedResults = new List<AssetExtractionResultDto>(extractionItems.Length);
                var failures = new List<IReadOnlyList<AppError>>();

                for (var index = 0; index < extractionItems.Length; index++)
                {
                    token.ThrowIfCancellationRequested();

                    var item = extractionItems[index];
                    taskHandle.ReportProgress(index, extractionItems.Length, item.DisplayName);

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

                    taskHandle.ReportProgress(index + 1, extractionItems.Length, item.DisplayName);
                }

                return new BundleExplorerBatchAssetExtractionExecutionResult(
                    extractedResults,
                    failures,
                    IsCancelled: false);
            },
            cancellationToken);
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
                        defaultBundleDisplayName))))
            .ToArray();

        using var taskHandle = _backgroundTaskStatusService.BeginTask(
            _localizationService.GetRequiredString(ResourceKeys.BundleExplorerStatusExtractSelectedSpineGroups),
            extractionItems.Length);
        using var notificationBatch = _operationChangeNotifier?.BeginNotificationBatch();

        return await _backgroundTaskRunner.RunAsync(
            async token =>
            {
                var extractionResults = new List<SpineAssetGroupExtractionResultDto>(extractionItems.Length);
                var failures = new List<IReadOnlyList<AppError>>();

                for (var index = 0; index < extractionItems.Length; index++)
                {
                    token.ThrowIfCancellationRequested();

                    var item = extractionItems[index];
                    taskHandle.ReportProgress(index, extractionItems.Length, item.DisplayName);

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

                    taskHandle.ReportProgress(index + 1, extractionItems.Length, item.DisplayName);
                }

                return new BundleExplorerBatchSpineGroupExtractionExecutionResult(
                    extractionResults,
                    failures,
                    IsCancelled: false);
            },
            cancellationToken);
    }

    private static string BuildBatchExtractionSuggestedPath(
        string selectedOutputDirectory,
        AssetListEntryViewModel entry,
        string defaultBundleDisplayName)
    {
        var placeholderFileName = string.IsNullOrWhiteSpace(entry.Item.Name)
            ? entry.Item.AssetId.Value
            : entry.Item.Name;
        return Path.Combine(
            BundleBatchExtractionOutputDirectoryResolver.BuildAssetTypeDirectory(
                selectedOutputDirectory,
                defaultBundleDisplayName,
                entry.Item.BundleId,
                entry.Item.AssetTypeDisplayName,
                entry.Item.AssetType),
            placeholderFileName);
    }

    private sealed record AssetBatchExtractionItem(
        string DisplayName,
        ExtractAssetCommand Command);

    private sealed record SpineGroupBatchExtractionItem(
        string DisplayName,
        ExtractSpineAssetGroupCommand Command);
}

internal sealed record BundleExplorerBatchAssetExtractionExecutionResult(
    IReadOnlyList<AssetExtractionResultDto> ExtractedResults,
    IReadOnlyList<IReadOnlyList<AppError>> Failures,
    bool IsCancelled);

internal sealed record BundleExplorerBatchSpineGroupExtractionExecutionResult(
    IReadOnlyList<SpineAssetGroupExtractionResultDto> ExtractionResults,
    IReadOnlyList<IReadOnlyList<AppError>> Failures,
    bool IsCancelled);
