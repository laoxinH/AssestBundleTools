using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Queries;
using AssestBundleTools.Application.Replacement;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Queries;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common;
using Microsoft.UI.Xaml;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 承载批处理任务页当前阶段的工作区变更摘要、批量提取、批量替换和保存前摘要状态。
/// </summary>
/// <remarks>
/// 当前页面仍以“工作区变更承载面”为核心职责。
/// 这一阶段已经补齐批量提取骨架、批量替换骨架、保存骨架与结果明细面板，
/// 但仍不执行真实文件导出、真实替换写回或真实保存写包。
/// </remarks>
public sealed class BatchTasksViewModel : ObservableObject, IDisposable
{
    private const int MaxRecentTaskResults = 8;

    private readonly IWorkspaceBrowserService _workspaceBrowserService;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly ILocalizationService _localizationService;
    private readonly IAppLogService _appLogService;
    private readonly IWorkspaceImportCoordinatorService _workspaceImportCoordinatorService;
    private bool _isInitialized;
    private bool _isBusy;
    private string? _activeWorkspaceId;
    private string _workspaceOverviewText = string.Empty;
    private string _changeSummaryText = string.Empty;
    private AssetTypeOptionViewModel? _selectedBatchExtractAssetTypeOption;
    private string _batchExtractScopeText = string.Empty;
    private string _batchExtractOutputDirectoryText = string.Empty;
    private string _batchExtractFileNamePolicyText = string.Empty;
    private string _batchExtractConflictPolicyText = string.Empty;
    private string _batchExtractSummaryText = string.Empty;
    private string _batchExtractLastRunText = string.Empty;
    private BatchReplacementSourceOptionViewModel? _selectedBatchReplaceSourceOption;
    private string _batchReplaceScopeText = string.Empty;
    private string _batchReplaceSummaryText = string.Empty;
    private string _batchReplaceLastRunText = string.Empty;
    private string _saveOutputDirectoryText = string.Empty;
    private string _saveFileNamePolicyText = string.Empty;
    private string _savePreviewSummaryText = string.Empty;
    private string _saveLastRunText = string.Empty;

    public BatchTasksViewModel(
        IWorkspaceBrowserService workspaceBrowserService,
        IWorkspaceContextService workspaceContextService,
        ILocalizationService localizationService,
        IAppLogService appLogService,
        IWorkspaceImportCoordinatorService workspaceImportCoordinatorService)
    {
        _workspaceBrowserService = workspaceBrowserService ?? throw new ArgumentNullException(nameof(workspaceBrowserService));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _workspaceImportCoordinatorService = workspaceImportCoordinatorService ?? throw new ArgumentNullException(nameof(workspaceImportCoordinatorService));

        ReplaceBatchExtractAssetTypeOptions(CreateAssetTypeOptions());
        ReplaceBatchReplacementSourceOptions(CreateBatchReplacementSourceOptions());
        BatchExtractFileNamePolicyText = GetString(ResourceKeys.BatchTasksBatchExtractFileNamePolicyRetainOriginal);
        BatchExtractConflictPolicyText = GetString(ResourceKeys.BatchTasksBatchExtractConflictPolicySkipDuplicates);
        ResetWorkspaceScopedExecutionState(clearTaskResults: true);

        RefreshCommand = new AsyncRelayCommand(
            executeAsync: RefreshAsync,
            canExecute: () => !IsBusy);

        ImportBundleFilesCommand = new AsyncRelayCommand(
            executeAsync: RequestBundleFileImportAsync,
            canExecute: () => !IsBusy && HasWorkspace);

        ImportBundleDirectoryCommand = new AsyncRelayCommand(
            executeAsync: RequestBundleDirectoryImportAsync,
            canExecute: () => !IsBusy && HasWorkspace);

        RunBatchExtractCommand = new AsyncRelayCommand(
            executeAsync: RunBatchExtractAsync,
            canExecute: () => !IsBusy && HasWorkspace && HasBatchExtractionCandidates);

        RunBatchReplaceCommand = new AsyncRelayCommand(
            executeAsync: RunBatchReplacementAsync,
            canExecute: () => !IsBusy && HasWorkspace && HasBatchReplacementCandidates);

        RunSaveCommand = new AsyncRelayCommand(
            executeAsync: RunSaveAsync,
            canExecute: () => !IsBusy && HasWorkspace && HasSavePreviewItems);
    }

    public ObservableCollection<BatchTaskSummaryCardViewModel> SummaryCards { get; } = new();

    public ObservableCollection<BatchTaskChangeItemViewModel> RecentChanges { get; } = new();

    public ObservableCollection<AssetTypeOptionViewModel> BatchExtractAssetTypeOptions { get; } = new();

    public ObservableCollection<BatchTaskExtractionBundleItemViewModel> BatchExtractionBundles { get; } = new();

    public ObservableCollection<BatchReplacementSourceOptionViewModel> BatchReplaceSourceOptions { get; } = new();

    public ObservableCollection<BatchTaskReplacementBundleItemViewModel> BatchReplacementBundles { get; } = new();

    public ObservableCollection<BatchTaskSaveBundleItemViewModel> SavePreviewBundles { get; } = new();

    public ObservableCollection<BatchTaskExecutionResultItemViewModel> RecentTaskResults { get; } = new();

    public ObservableCollection<BatchTaskTextItemViewModel> SaveRiskNotes { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand ImportBundleFilesCommand { get; }

    public AsyncRelayCommand ImportBundleDirectoryCommand { get; }

    public AsyncRelayCommand RunBatchExtractCommand { get; }

    public AsyncRelayCommand RunBatchReplaceCommand { get; }

    public AsyncRelayCommand RunSaveCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.NotifyCanExecuteChanged();
                ImportBundleFilesCommand.NotifyCanExecuteChanged();
                ImportBundleDirectoryCommand.NotifyCanExecuteChanged();
                RunBatchExtractCommand.NotifyCanExecuteChanged();
                RunBatchReplaceCommand.NotifyCanExecuteChanged();
                RunSaveCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string WorkspaceOverviewText
    {
        get => _workspaceOverviewText;
        private set => SetProperty(ref _workspaceOverviewText, value);
    }

    public string ChangeSummaryText
    {
        get => _changeSummaryText;
        private set => SetProperty(ref _changeSummaryText, value);
    }

    public AssetTypeOptionViewModel? SelectedBatchExtractAssetTypeOption
    {
        get => _selectedBatchExtractAssetTypeOption;
        set
        {
            if (SetProperty(ref _selectedBatchExtractAssetTypeOption, value) && _isInitialized)
            {
                _ = RefreshAsync();
            }
        }
    }

    public string BatchExtractScopeText
    {
        get => _batchExtractScopeText;
        private set => SetProperty(ref _batchExtractScopeText, value);
    }

    public string BatchExtractOutputDirectoryText
    {
        get => _batchExtractOutputDirectoryText;
        private set => SetProperty(ref _batchExtractOutputDirectoryText, value);
    }

    public string BatchExtractFileNamePolicyText
    {
        get => _batchExtractFileNamePolicyText;
        private set => SetProperty(ref _batchExtractFileNamePolicyText, value);
    }

    public string BatchExtractConflictPolicyText
    {
        get => _batchExtractConflictPolicyText;
        private set => SetProperty(ref _batchExtractConflictPolicyText, value);
    }

    public string BatchExtractSummaryText
    {
        get => _batchExtractSummaryText;
        private set => SetProperty(ref _batchExtractSummaryText, value);
    }

    public string BatchExtractLastRunText
    {
        get => _batchExtractLastRunText;
        private set => SetProperty(ref _batchExtractLastRunText, value);
    }

    public BatchReplacementSourceOptionViewModel? SelectedBatchReplaceSourceOption
    {
        get => _selectedBatchReplaceSourceOption;
        set
        {
            if (SetProperty(ref _selectedBatchReplaceSourceOption, value) && _isInitialized)
            {
                _ = RefreshAsync();
            }
        }
    }

    public string BatchReplaceScopeText
    {
        get => _batchReplaceScopeText;
        private set => SetProperty(ref _batchReplaceScopeText, value);
    }

    public string BatchReplaceSummaryText
    {
        get => _batchReplaceSummaryText;
        private set => SetProperty(ref _batchReplaceSummaryText, value);
    }

    public string BatchReplaceLastRunText
    {
        get => _batchReplaceLastRunText;
        private set => SetProperty(ref _batchReplaceLastRunText, value);
    }

    public string SaveOutputDirectoryText
    {
        get => _saveOutputDirectoryText;
        private set => SetProperty(ref _saveOutputDirectoryText, value);
    }

    public string SaveFileNamePolicyText
    {
        get => _saveFileNamePolicyText;
        private set => SetProperty(ref _saveFileNamePolicyText, value);
    }

    public string SavePreviewSummaryText
    {
        get => _savePreviewSummaryText;
        private set => SetProperty(ref _savePreviewSummaryText, value);
    }

    public string SaveLastRunText
    {
        get => _saveLastRunText;
        private set => SetProperty(ref _saveLastRunText, value);
    }

    public bool HasWorkspace => _workspaceContextService.HasCurrentWorkspace;

    public bool HasChanges => RecentChanges.Count > 0;

    public bool HasBatchExtractionCandidates => BatchExtractionBundles.Count > 0;

    public bool HasBatchReplacementCandidates => BatchReplacementBundles.Count > 0;

    public bool HasSavePreviewItems => SavePreviewBundles.Count > 0;

    public bool HasTaskResults => RecentTaskResults.Count > 0;

    public Visibility WorkspaceEmptyStateVisibility => HasWorkspace ? Visibility.Collapsed : Visibility.Visible;

    public Visibility WorkspaceContentVisibility => HasWorkspace ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChangeListVisibility => HasChanges ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ChangeEmptyStateVisibility => HasChanges ? Visibility.Collapsed : Visibility.Visible;

    public Visibility BatchExtractListVisibility => HasBatchExtractionCandidates ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BatchExtractEmptyStateVisibility => HasBatchExtractionCandidates ? Visibility.Collapsed : Visibility.Visible;

    public Visibility BatchReplaceListVisibility => HasBatchReplacementCandidates ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BatchReplaceEmptyStateVisibility => HasBatchReplacementCandidates ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SavePreviewListVisibility => HasSavePreviewItems ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SavePreviewEmptyStateVisibility => HasSavePreviewItems ? Visibility.Collapsed : Visibility.Visible;

    public Visibility TaskResultListVisibility => HasTaskResults ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TaskResultEmptyStateVisibility => HasTaskResults ? Visibility.Collapsed : Visibility.Visible;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        _workspaceContextService.CurrentWorkspaceChanged += OnCurrentWorkspaceChanged;
        _workspaceContextService.WorkspaceEntriesChanged += OnWorkspaceEntriesChanged;
        await RefreshAsync();
    }

    public void Dispose()
    {
        _workspaceContextService.CurrentWorkspaceChanged -= OnCurrentWorkspaceChanged;
        _workspaceContextService.WorkspaceEntriesChanged -= OnWorkspaceEntriesChanged;
        _isInitialized = false;
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var currentWorkspaceSummary = _workspaceContextService.CurrentWorkspaceSummary;
        if (currentWorkspaceSummary is null)
        {
            ApplyNoWorkspaceState();
            return;
        }

        if (!string.Equals(_activeWorkspaceId, currentWorkspaceSummary.WorkspaceId.Value, StringComparison.Ordinal))
        {
            _activeWorkspaceId = currentWorkspaceSummary.WorkspaceId.Value;
            ResetWorkspaceScopedExecutionState(clearTaskResults: true);
        }

        IsBusy = true;
        try
        {
            var changeSummaryResult = await _workspaceBrowserService.GetWorkspaceChangeSummaryAsync(
                new GetWorkspaceChangeSummaryQuery(currentWorkspaceSummary.WorkspaceId));

            if (!TryGetValue(changeSummaryResult, out var summary))
            {
                return;
            }

            var savePreviewResult = await _workspaceBrowserService.GetWorkspaceSavePreviewAsync(
                new GetWorkspaceSavePreviewQuery(currentWorkspaceSummary.WorkspaceId));

            if (!TryGetValue(savePreviewResult, out var savePreview))
            {
                return;
            }

            var batchExtractionPreviewResult = await _workspaceBrowserService.GetWorkspaceBatchExtractionPreviewAsync(
                new GetWorkspaceBatchExtractionPreviewQuery(
                    currentWorkspaceSummary.WorkspaceId,
                    SelectedBatchExtractAssetTypeOption?.AssetType));

            if (!TryGetValue(batchExtractionPreviewResult, out var batchExtractionPreview))
            {
                return;
            }

            var batchReplacementPreviewResult = await _workspaceBrowserService.GetWorkspaceBatchReplacementPreviewAsync(
                new GetWorkspaceBatchReplacementPreviewQuery(
                    currentWorkspaceSummary.WorkspaceId,
                    SelectedBatchReplaceSourceOption?.SourceKind ?? BatchReplacementSourceKind.All));

            if (!TryGetValue(batchReplacementPreviewResult, out var batchReplacementPreview))
            {
                return;
            }

            ApplyWorkspaceSummary(summary);
            ApplyBatchExtractionPreview(batchExtractionPreview);
            ApplyBatchReplacementPreview(batchReplacementPreview);
            ApplySavePreview(savePreview);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnCurrentWorkspaceChanged(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    /// <summary>
    /// 当前阶段批处理页除了关注活动工作区切换，也要跟随目录项刷新。
    /// 这样当最近工作区目录或会话登记状态变化时，页面摘要不会残留旧上下文。
    /// </summary>
    private async void OnWorkspaceEntriesChanged(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RequestBundleFileImportAsync()
    {
        await _workspaceImportCoordinatorService.RequestBundleFileImportAsync();
    }

    private async Task RequestBundleDirectoryImportAsync()
    {
        await _workspaceImportCoordinatorService.RequestBundleDirectoryImportAsync();
    }

    private void ApplyNoWorkspaceState()
    {
        _activeWorkspaceId = null;
        WorkspaceOverviewText = GetString(ResourceKeys.BatchTasksWorkspaceOverviewEmpty);
        ChangeSummaryText = GetString(ResourceKeys.BatchTasksChangeSummaryEmpty);
        BatchExtractScopeText = GetString(ResourceKeys.BatchTasksBatchExtractScopeEmpty);
        BatchExtractOutputDirectoryText = GetString(ResourceKeys.BatchTasksBatchExtractOutputDirectoryEmpty);
        BatchExtractSummaryText = GetString(ResourceKeys.BatchTasksBatchExtractEmpty);
        BatchReplaceScopeText = GetString(ResourceKeys.BatchTasksBatchReplaceScopeEmpty);
        BatchReplaceSummaryText = GetString(ResourceKeys.BatchTasksBatchReplaceEmpty);
        SaveOutputDirectoryText = GetString(ResourceKeys.BatchTasksSavePreviewOutputDirectoryEmpty);
        SaveFileNamePolicyText = GetString(ResourceKeys.BatchTasksSavePreviewFileNamePolicyRetainOriginal);
        SavePreviewSummaryText = GetString(ResourceKeys.BatchTasksSavePreviewEmpty);
        ResetWorkspaceScopedExecutionState(clearTaskResults: true);
        SummaryCards.Clear();
        RecentChanges.Clear();
        BatchExtractionBundles.Clear();
        BatchReplacementBundles.Clear();
        SavePreviewBundles.Clear();
        SaveRiskNotes.Clear();
        NotifyWorkspaceState();
    }

    private void ApplyWorkspaceSummary(WorkspaceChangeSummaryDto summary)
    {
        var sourceText = string.IsNullOrWhiteSpace(summary.SourcePath)
            ? GetString(ResourceKeys.WorkspaceSourceNotImported)
            : summary.SourcePath;

        WorkspaceOverviewText = _localizationService.GetFormattedString(
            ResourceKeys.BatchTasksWorkspaceOverviewFormat,
            summary.WorkspaceDisplayName,
            summary.BundleCount,
            summary.AssetCount,
            sourceText);

        ChangeSummaryText = _localizationService.GetFormattedString(
            ResourceKeys.BatchTasksChangeSummaryFormat,
            summary.TotalChangeCount,
            summary.ModifiedAssetCount,
            summary.ExtractedAssetCount,
            summary.WorkspaceReplacementCount,
            summary.FileReplacementCount);

        SummaryCards.Clear();
        SummaryCards.Add(new BatchTaskSummaryCardViewModel(
            GetString(ResourceKeys.BatchTasksSummaryTotalChangesTitle),
            summary.TotalChangeCount.ToString(),
            GetString(ResourceKeys.BatchTasksSummaryTotalChangesDescription)));
        SummaryCards.Add(new BatchTaskSummaryCardViewModel(
            GetString(ResourceKeys.BatchTasksSummaryModifiedTitle),
            summary.ModifiedAssetCount.ToString(),
            GetString(ResourceKeys.BatchTasksSummaryModifiedDescription)));
        SummaryCards.Add(new BatchTaskSummaryCardViewModel(
            GetString(ResourceKeys.BatchTasksSummaryExtractedTitle),
            summary.ExtractedAssetCount.ToString(),
            GetString(ResourceKeys.BatchTasksSummaryExtractedDescription)));
        SummaryCards.Add(new BatchTaskSummaryCardViewModel(
            GetString(ResourceKeys.BatchTasksSummaryReplacedTitle),
            summary.WorkspaceReplacementCount.ToString(),
            GetString(ResourceKeys.BatchTasksSummaryReplacedDescription)));
        SummaryCards.Add(new BatchTaskSummaryCardViewModel(
            GetString(ResourceKeys.BatchTasksSummaryFileReplacedTitle),
            summary.FileReplacementCount.ToString(),
            GetString(ResourceKeys.BatchTasksSummaryFileReplacedDescription)));

        RecentChanges.Clear();
        foreach (var item in summary.Items)
        {
            RecentChanges.Add(new BatchTaskChangeItemViewModel(
                AssetName: item.AssetName,
                BundleDisplayName: item.BundleDisplayName,
                AssetTypeText: GetAssetTypeDisplayName(item.AssetType),
                StateText: BuildStateText(item),
                DetailText: BuildDetailText(item),
                TimeText: item.LastChangedAtUtc.HasValue
                    ? FormatTimestamp(item.LastChangedAtUtc.Value)
                    : GetString(ResourceKeys.BatchTasksChangeTimeUnknown)));
        }

        NotifyWorkspaceState();
    }

    /// <summary>
    /// 应用批量提取预览结果。
    /// 当前阶段只展示当前工作区范围、筛选后的候选数量和资源包级聚合结果。
    /// </summary>
    private void ApplyBatchExtractionPreview(WorkspaceBatchExtractionPreviewDto preview)
    {
        BatchExtractScopeText = _localizationService.GetFormattedString(
            ResourceKeys.BatchTasksBatchExtractScopeFormat,
            preview.WorkspaceDisplayName);
        BatchExtractOutputDirectoryText = preview.SuggestedOutputDirectory;
        BatchExtractSummaryText = preview.CandidateAssetCount > 0
            ? _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchExtractSummaryFormat,
                preview.CandidateAssetCount,
                preview.CandidateBundleCount,
                FormatSize(preview.TotalSizeInBytes))
            : GetString(ResourceKeys.BatchTasksBatchExtractEmpty);

        BatchExtractionBundles.Clear();
        foreach (var bundlePlan in preview.BundlePlans)
        {
            BatchExtractionBundles.Add(new BatchTaskExtractionBundleItemViewModel(
                BundleDisplayName: bundlePlan.BundleDisplayName,
                SummaryText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksBatchExtractBundlePlanSummaryFormat,
                    bundlePlan.AssetCount,
                    FormatSize(bundlePlan.TotalSizeInBytes))));
        }

        NotifyWorkspaceState();
    }

    /// <summary>
    /// 应用批量替换预览结果。
    /// 当前阶段把工作区中已进入替换队列的资源聚合到批处理页，先稳定承载面与结果反馈。
    /// </summary>
    private void ApplyBatchReplacementPreview(WorkspaceBatchReplacementPreviewDto preview)
    {
        BatchReplaceScopeText = _localizationService.GetFormattedString(
            ResourceKeys.BatchTasksBatchReplaceScopeFormat,
            preview.WorkspaceDisplayName);
        BatchReplaceSummaryText = preview.CandidateAssetCount > 0
            ? _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchReplaceSummaryFormat,
                preview.CandidateAssetCount,
                preview.CandidateBundleCount,
                preview.WorkspaceReplacementCount,
                preview.FileReplacementCount)
            : GetString(ResourceKeys.BatchTasksBatchReplaceEmpty);

        BatchReplacementBundles.Clear();
        foreach (var bundlePlan in preview.BundlePlans)
        {
            BatchReplacementBundles.Add(new BatchTaskReplacementBundleItemViewModel(
                BundleDisplayName: bundlePlan.BundleDisplayName,
                SummaryText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksBatchReplaceBundlePlanSummaryFormat,
                    bundlePlan.AssetCount,
                    bundlePlan.WorkspaceReplacementCount,
                    bundlePlan.FileReplacementCount)));
        }

        NotifyWorkspaceState();
    }

    private async Task RunBatchExtractAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        WorkspaceBatchExtractionResultDto? extractionResult = null;
        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.ExtractWorkspaceAssetsAsync(
                new ExtractWorkspaceAssetsCommand(
                    WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId,
                    AssetTypeFilter: SelectedBatchExtractAssetTypeOption?.AssetType));

            if (!TryGetValue(result, out extractionResult))
            {
                return;
            }

            BatchExtractLastRunText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchExtractLastRunFormat,
                extractionResult.ExtractedAssetCount,
                extractionResult.ExtractedBundleCount,
                FormatTimestamp(extractionResult.CompletedAtUtc));

            AddTaskExecutionResult(CreateBatchExtractTaskResult(extractionResult));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchExtractLogExecutedFormat,
                extractionResult.ExtractedAssetCount,
                extractionResult.ExtractedBundleCount,
                extractionResult.SuggestedOutputDirectory));
        }
        finally
        {
            IsBusy = false;
        }

        if (extractionResult is not null)
        {
            await RefreshAsync();
        }
    }

    /// <summary>
    /// 执行批量替换骨架。
    /// 当前阶段只验证并汇总已经进入替换队列的资源，不重新回放真实替换动作。
    /// </summary>
    private async Task RunBatchReplacementAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.ExecuteWorkspaceBatchReplacementAsync(
                new ExecuteWorkspaceBatchReplacementCommand(
                    WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId,
                    SourceFilter: SelectedBatchReplaceSourceOption?.SourceKind ?? BatchReplacementSourceKind.All));

            if (!TryGetValue(result, out WorkspaceBatchReplacementResultDto? replacementResult))
            {
                return;
            }

            BatchReplaceLastRunText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchReplaceLastRunFormat,
                replacementResult.ProcessedAssetCount,
                replacementResult.ProcessedBundleCount,
                replacementResult.WorkspaceReplacementCount,
                replacementResult.FileReplacementCount,
                FormatTimestamp(replacementResult.CompletedAtUtc));

            AddTaskExecutionResult(CreateBatchReplacementTaskResult(replacementResult));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BatchTasksBatchReplaceLogExecutedFormat,
                replacementResult.ProcessedAssetCount,
                replacementResult.ProcessedBundleCount,
                GetBatchReplacementSourceDisplayName(replacementResult.SourceFilter)));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 执行“另存为新包”骨架。
    /// 当前阶段不写真实文件，只返回一份稳定执行摘要并记录到应用日志。
    /// </summary>
    private async Task RunSaveAsync()
    {
        if (IsBusy || _workspaceContextService.CurrentWorkspaceSummary is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _workspaceBrowserService.SaveWorkspaceAsNewBundleAsync(
                new SaveWorkspaceAsNewBundleCommand(
                    WorkspaceId: _workspaceContextService.CurrentWorkspaceSummary.WorkspaceId));

            if (!TryGetValue(result, out WorkspaceSaveExecutionResultDto? saveResult))
            {
                return;
            }

            SaveLastRunText = _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksSaveExecutionLastRunFormat,
                saveResult.PlannedBundleCount,
                saveResult.TotalChangeCount,
                FormatTimestamp(saveResult.CompletedAtUtc));

            AddTaskExecutionResult(CreateSaveTaskResult(saveResult));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BatchTasksSaveExecutionLogExecutedFormat,
                saveResult.PlannedBundleCount,
                saveResult.TotalChangeCount,
                saveResult.OutputDirectory));
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 应用保存前摘要读模型。
    /// 当前阶段仍只展示预览，不执行真实写入，因此这里专注于“输出去向”和“将写出哪些资源包”。
    /// </summary>
    private void ApplySavePreview(WorkspaceSavePreviewDto preview)
    {
        SaveOutputDirectoryText = preview.OutputDirectory;
        SaveFileNamePolicyText = GetString(ResourceKeys.BatchTasksSavePreviewFileNamePolicyRetainOriginal);
        SavePreviewSummaryText = preview.PlannedBundleCount > 0
            ? _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksSavePreviewSummaryFormat,
                preview.PlannedBundleCount,
                preview.TotalChangeCount,
                preview.OutputDirectory)
            : GetString(ResourceKeys.BatchTasksSavePreviewEmpty);

        SavePreviewBundles.Clear();
        foreach (var bundlePlan in preview.BundlePlans)
        {
            SavePreviewBundles.Add(new BatchTaskSaveBundleItemViewModel(
                BundleDisplayName: bundlePlan.BundleDisplayName,
                OutputPath: bundlePlan.OutputFilePath,
                SummaryText: _localizationService.GetFormattedString(
                    ResourceKeys.BatchTasksSavePreviewBundlePlanSummaryFormat,
                    bundlePlan.ChangeCount,
                    bundlePlan.ModifiedAssetCount,
                    bundlePlan.ExtractedAssetCount,
                    bundlePlan.WorkspaceReplacementCount,
                    bundlePlan.FileReplacementCount)));
        }

        SaveRiskNotes.Clear();
        SaveRiskNotes.Add(new BatchTaskTextItemViewModel(GetString(ResourceKeys.BatchTasksSavePreviewRiskNoOverwrite)));
        SaveRiskNotes.Add(new BatchTaskTextItemViewModel(GetString(ResourceKeys.BatchTasksSavePreviewRiskConflictDeferred)));
        SaveRiskNotes.Add(new BatchTaskTextItemViewModel(GetString(ResourceKeys.BatchTasksSavePreviewRiskExecutionDeferred)));

        NotifyWorkspaceState();
    }

    private BatchTaskExecutionResultItemViewModel CreateBatchExtractTaskResult(WorkspaceBatchExtractionResultDto extractionResult) =>
        new(
            Title: GetString(ResourceKeys.BatchTasksTaskResultBatchExtractTitle),
            SummaryText: _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksTaskResultBatchExtractSummaryFormat,
                extractionResult.ExtractedAssetCount,
                extractionResult.ExtractedBundleCount),
            DetailText: _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksTaskResultBatchExtractDetailFormat,
                extractionResult.SuggestedOutputDirectory),
            StatusText: GetString(ResourceKeys.BatchTasksTaskResultStatusCompleted),
            TimeText: FormatTimestamp(extractionResult.CompletedAtUtc));

    private BatchTaskExecutionResultItemViewModel CreateBatchReplacementTaskResult(WorkspaceBatchReplacementResultDto replacementResult) =>
        new(
            Title: GetString(ResourceKeys.BatchTasksTaskResultBatchReplaceTitle),
            SummaryText: _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksTaskResultBatchReplaceSummaryFormat,
                replacementResult.ProcessedAssetCount,
                replacementResult.ProcessedBundleCount),
            DetailText: _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksTaskResultBatchReplaceDetailFormat,
                GetBatchReplacementSourceDisplayName(replacementResult.SourceFilter),
                replacementResult.WorkspaceReplacementCount,
                replacementResult.FileReplacementCount),
            StatusText: GetString(ResourceKeys.BatchTasksTaskResultStatusCompleted),
            TimeText: FormatTimestamp(replacementResult.CompletedAtUtc));

    private BatchTaskExecutionResultItemViewModel CreateSaveTaskResult(WorkspaceSaveExecutionResultDto saveResult) =>
        new(
            Title: GetString(ResourceKeys.BatchTasksTaskResultSaveTitle),
            SummaryText: _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksTaskResultSaveSummaryFormat,
                saveResult.PlannedBundleCount,
                saveResult.TotalChangeCount),
            DetailText: _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksTaskResultSaveDetailFormat,
                saveResult.OutputDirectory),
            StatusText: GetString(ResourceKeys.BatchTasksTaskResultStatusCompleted),
            TimeText: FormatTimestamp(saveResult.CompletedAtUtc));

    /// <summary>
    /// 维护最近批处理结果面板，确保页面只保留有限条目，避免把右侧结果区膨胀成日志面板。
    /// </summary>
    private void AddTaskExecutionResult(BatchTaskExecutionResultItemViewModel item)
    {
        RecentTaskResults.Insert(0, item);

        while (RecentTaskResults.Count > MaxRecentTaskResults)
        {
            RecentTaskResults.RemoveAt(RecentTaskResults.Count - 1);
        }

        NotifyWorkspaceState();
    }

    private void ResetWorkspaceScopedExecutionState(bool clearTaskResults)
    {
        BatchExtractLastRunText = GetString(ResourceKeys.BatchTasksBatchExtractLastRunNone);
        BatchReplaceLastRunText = GetString(ResourceKeys.BatchTasksBatchReplaceLastRunNone);
        SaveLastRunText = GetString(ResourceKeys.BatchTasksSaveExecutionLastRunNone);

        if (clearTaskResults)
        {
            RecentTaskResults.Clear();
        }
    }

    private string BuildStateText(WorkspaceChangeItemDto item)
    {
        var hasWorkspaceReplacement = !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName);
        var hasFileReplacement = !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName);
        var hasExtraction = item.LastExtractedAtUtc.HasValue;

        return (hasWorkspaceReplacement, hasFileReplacement, hasExtraction, item.IsModified) switch
        {
            (true, false, true, _) => GetString(ResourceKeys.BatchTasksChangeStateWorkspaceReplacementAndExtracted),
            (true, false, false, _) => GetString(ResourceKeys.BatchTasksChangeStateWorkspaceReplacementOnly),
            (false, true, true, _) => GetString(ResourceKeys.BatchTasksChangeStateFileReplacementAndExtracted),
            (false, true, false, _) => GetString(ResourceKeys.BatchTasksChangeStateFileReplacementOnly),
            (false, false, true, true) => GetString(ResourceKeys.BatchTasksChangeStateModifiedAndExtracted),
            (false, false, true, false) => GetString(ResourceKeys.BatchTasksChangeStateExtractedOnly),
            _ => GetString(ResourceKeys.BatchTasksChangeStateModifiedOnly)
        };
    }

    private string BuildDetailText(WorkspaceChangeItemDto item)
    {
        var hasWorkspaceReplacement = !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName) &&
                                      !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceBundleDisplayName);
        var hasFileReplacement = !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName);
        var hasExtraction = item.LastExtractedAtUtc.HasValue;

        if (hasWorkspaceReplacement && hasExtraction)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksChangeDetailWorkspaceReplacementAndExtractedFormat,
                item.WorkspaceReplacementSourceBundleDisplayName!,
                item.WorkspaceReplacementSourceAssetName!,
                FormatTimestamp(item.LastExtractedAtUtc!.Value));
        }

        if (hasWorkspaceReplacement)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksChangeDetailWorkspaceReplacementOnlyFormat,
                item.WorkspaceReplacementSourceBundleDisplayName!,
                item.WorkspaceReplacementSourceAssetName!);
        }

        if (hasFileReplacement && hasExtraction)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksChangeDetailFileReplacementAndExtractedFormat,
                item.FileReplacementSourceFileName!,
                FormatTimestamp(item.LastExtractedAtUtc!.Value));
        }

        if (hasFileReplacement)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksChangeDetailFileReplacementOnlyFormat,
                item.FileReplacementSourceFileName!,
                item.FileReplacementSourcePath ?? item.FileReplacementSourceFileName!);
        }

        if (hasExtraction)
        {
            return _localizationService.GetFormattedString(
                ResourceKeys.BatchTasksChangeDetailExtractedOnlyFormat,
                FormatTimestamp(item.LastExtractedAtUtc!.Value));
        }

        return GetString(ResourceKeys.BatchTasksChangeDetailModifiedOnly);
    }

    private string GetAssetTypeDisplayName(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D => GetString(ResourceKeys.AssetTypeDisplayTexture2D),
            AssetType.Sprite => GetString(ResourceKeys.AssetTypeDisplaySprite),
            AssetType.TextAsset => GetString(ResourceKeys.AssetTypeDisplayTextAsset),
            AssetType.AudioClip => GetString(ResourceKeys.AssetTypeDisplayAudioClip),
            AssetType.Mesh => GetString(ResourceKeys.AssetTypeDisplayMesh),
            AssetType.Material => GetString(ResourceKeys.AssetTypeDisplayMaterial),
            _ => GetString(ResourceKeys.AssetTypeDisplayUnknown)
        };

    private string GetBatchReplacementSourceDisplayName(BatchReplacementSourceKind sourceKind) =>
        sourceKind switch
        {
            BatchReplacementSourceKind.Workspace => GetString(ResourceKeys.BatchTasksBatchReplaceSourceFilterWorkspace),
            BatchReplacementSourceKind.File => GetString(ResourceKeys.BatchTasksBatchReplaceSourceFilterFile),
            _ => GetString(ResourceKeys.BatchTasksBatchReplaceSourceFilterAll)
        };

    private string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

    private string FormatSize(long sizeInBytes)
    {
        if (sizeInBytes >= 1024 * 1024)
        {
            return $"{sizeInBytes / 1024d / 1024d:F1} MB";
        }

        if (sizeInBytes >= 1024)
        {
            return $"{sizeInBytes / 1024d:F1} KB";
        }

        return $"{sizeInBytes:N0} B";
    }

    private void ReplaceBatchExtractAssetTypeOptions(IEnumerable<AssetTypeOptionViewModel> options)
    {
        BatchExtractAssetTypeOptions.Clear();
        foreach (var option in options)
        {
            BatchExtractAssetTypeOptions.Add(option);
        }

        SelectedBatchExtractAssetTypeOption = BatchExtractAssetTypeOptions.FirstOrDefault();
    }

    private void ReplaceBatchReplacementSourceOptions(IEnumerable<BatchReplacementSourceOptionViewModel> options)
    {
        BatchReplaceSourceOptions.Clear();
        foreach (var option in options)
        {
            BatchReplaceSourceOptions.Add(option);
        }

        SelectedBatchReplaceSourceOption = BatchReplaceSourceOptions.FirstOrDefault();
    }

    private IEnumerable<AssetTypeOptionViewModel> CreateAssetTypeOptions() =>
        new[]
        {
            new AssetTypeOptionViewModel(GetString(ResourceKeys.AssetTypeFilterAll), null),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Texture2D), AssetType.Texture2D),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Sprite), AssetType.Sprite),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.TextAsset), AssetType.TextAsset),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.AudioClip), AssetType.AudioClip),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Mesh), AssetType.Mesh),
            new AssetTypeOptionViewModel(GetAssetTypeDisplayName(AssetType.Material), AssetType.Material)
        };

    private IEnumerable<BatchReplacementSourceOptionViewModel> CreateBatchReplacementSourceOptions() =>
        new[]
        {
            new BatchReplacementSourceOptionViewModel(GetString(ResourceKeys.BatchTasksBatchReplaceSourceFilterAll), BatchReplacementSourceKind.All),
            new BatchReplacementSourceOptionViewModel(GetString(ResourceKeys.BatchTasksBatchReplaceSourceFilterWorkspace), BatchReplacementSourceKind.Workspace),
            new BatchReplacementSourceOptionViewModel(GetString(ResourceKeys.BatchTasksBatchReplaceSourceFilterFile), BatchReplacementSourceKind.File)
        };

    private void NotifyWorkspaceState()
    {
        OnPropertyChanged(nameof(HasWorkspace));
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasBatchExtractionCandidates));
        OnPropertyChanged(nameof(HasBatchReplacementCandidates));
        OnPropertyChanged(nameof(HasSavePreviewItems));
        OnPropertyChanged(nameof(HasTaskResults));
        OnPropertyChanged(nameof(WorkspaceEmptyStateVisibility));
        OnPropertyChanged(nameof(WorkspaceContentVisibility));
        OnPropertyChanged(nameof(ChangeListVisibility));
        OnPropertyChanged(nameof(ChangeEmptyStateVisibility));
        OnPropertyChanged(nameof(BatchExtractListVisibility));
        OnPropertyChanged(nameof(BatchExtractEmptyStateVisibility));
        OnPropertyChanged(nameof(BatchReplaceListVisibility));
        OnPropertyChanged(nameof(BatchReplaceEmptyStateVisibility));
        OnPropertyChanged(nameof(SavePreviewListVisibility));
        OnPropertyChanged(nameof(SavePreviewEmptyStateVisibility));
        OnPropertyChanged(nameof(TaskResultListVisibility));
        OnPropertyChanged(nameof(TaskResultEmptyStateVisibility));
        ImportBundleFilesCommand.NotifyCanExecuteChanged();
        ImportBundleDirectoryCommand.NotifyCanExecuteChanged();
        RunBatchExtractCommand.NotifyCanExecuteChanged();
        RunBatchReplaceCommand.NotifyCanExecuteChanged();
        RunSaveCommand.NotifyCanExecuteChanged();
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private bool TryGetValue<T>(Result<T> result, [NotNullWhen(true)] out T? value)
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

        AppendResultFailure(result.Errors);
        return false;
    }

    private void AppendResultFailure(IReadOnlyList<AppError> errors)
    {
        if (errors.Count == 0)
        {
            return;
        }

        var firstError = errors[0];
        var message = firstError.Parameters.Count > 0
            ? _localizationService.GetFormattedString(
                firstError.MessageToken,
                firstError.Parameters.Values.Select(static value => value ?? string.Empty).ToArray())
            : _localizationService.GetRequiredString(firstError.MessageToken);

        _appLogService.AppendError(message);
    }
}
