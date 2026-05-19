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
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.Common.Support;
using Microsoft.UI.Xaml;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;

/// <summary>
/// 承载批处理任务页当前阶段的工作区变更摘要、批量提取、批量替换和保存前摘要状态。
/// </summary>
/// <remarks>
/// 当前页面仍以“工作区变更承载面”为核心职责。
/// 批量提取已经接入真实导出；批量替换与保存仍保留当前阶段的摘要执行面。
/// </remarks>
public sealed partial class BatchTasksViewModel : ObservableObject, IDisposable
{
    private const int MaxRecentTaskResults = 8;

    private readonly IWorkspaceBatchOperationsGateway _workspaceBatchOperationsGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly WorkspaceOperationChangeRefreshCoordinator _operationChangeRefreshCoordinator;
    private readonly ILocalizationService _localizationService;
    private readonly IAppLogService _appLogService;
    private readonly IWorkspaceImportCoordinatorService _workspaceImportCoordinatorService;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IAppBackgroundTaskStatusService _backgroundTaskStatusService;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly BatchTasksPresentationPresenter _presentationPresenter;
    private bool _isInitialized;
    private bool _isBusy;
    private bool _hasPendingRefreshRequest;
    private bool _isRefreshLoopActive;
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
        IWorkspaceBatchOperationsGateway workspaceBatchOperationsGateway,
        IWorkspaceContextService workspaceContextService,
        IWorkspaceOperationChangeNotifier workspaceOperationChangeNotifier,
        ILocalizationService localizationService,
        IAppLogService appLogService,
        IWorkspaceImportCoordinatorService workspaceImportCoordinatorService,
        IBackgroundTaskRunner? backgroundTaskRunner = null,
        IAppBackgroundTaskStatusService? backgroundTaskStatusService = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null)
    {
        _workspaceBatchOperationsGateway = workspaceBatchOperationsGateway ?? throw new ArgumentNullException(nameof(workspaceBatchOperationsGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _workspaceImportCoordinatorService = workspaceImportCoordinatorService ?? throw new ArgumentNullException(nameof(workspaceImportCoordinatorService));
        _backgroundTaskRunner = backgroundTaskRunner ?? InlineBackgroundTaskRunner.Instance;
        _backgroundTaskStatusService = backgroundTaskStatusService ?? NullAppBackgroundTaskStatusService.Instance;
        _backgroundWorkScheduler = backgroundWorkScheduler ?? new BackgroundWorkScheduler(
            _backgroundTaskRunner,
            _backgroundTaskStatusService);
        _operationChangeRefreshCoordinator = new WorkspaceOperationChangeRefreshCoordinator(
            workspaceOperationChangeNotifier ?? throw new ArgumentNullException(nameof(workspaceOperationChangeNotifier)),
            () => _workspaceContextService.CurrentWorkspaceSummary?.WorkspaceId,
            () => IsBusy,
            RefreshAsync);
        _presentationPresenter = new BatchTasksPresentationPresenter(this, _localizationService);

        _presentationPresenter.InitializeStaticState();
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

                if (!value && _hasPendingRefreshRequest)
                {
                    _ = RefreshAsync();
                }

                _operationChangeRefreshCoordinator.HandleBusyStateChanged(value);
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
        _operationChangeRefreshCoordinator.Dispose();
        _isInitialized = false;
    }

    public async Task RefreshAsync()
    {
        if (IsBusy || _isRefreshLoopActive)
        {
            _hasPendingRefreshRequest = true;
            return;
        }

        _isRefreshLoopActive = true;
        try
        {
            do
            {
                _hasPendingRefreshRequest = false;
                await RefreshOnceAsync();
            }
            while (_hasPendingRefreshRequest);
        }
        finally
        {
            _isRefreshLoopActive = false;
        }
    }

    private async Task RefreshOnceAsync()
    {
        var currentWorkspaceSummary = _workspaceContextService.CurrentWorkspaceSummary;
        if (currentWorkspaceSummary is null)
        {
            _presentationPresenter.ApplyNoWorkspaceState();
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
            var changeSummaryResult = await _workspaceBatchOperationsGateway.GetWorkspaceChangeSummaryAsync(
                new GetWorkspaceChangeSummaryQuery(currentWorkspaceSummary.WorkspaceId));

            if (!TryGetValue(changeSummaryResult, out var summary))
            {
                return;
            }

            var savePreviewResult = await _workspaceBatchOperationsGateway.GetWorkspaceSavePreviewAsync(
                new GetWorkspaceSavePreviewQuery(
                    currentWorkspaceSummary.WorkspaceId,
                    WorkspaceKey: currentWorkspaceSummary.WorkspaceKey,
                    WorkspaceDataRootPath: currentWorkspaceSummary.WorkspaceDataRootPath));

            if (!TryGetValue(savePreviewResult, out var savePreview))
            {
                return;
            }

            var batchExtractionPreviewResult = await _workspaceBatchOperationsGateway.GetWorkspaceBatchExtractionPreviewAsync(
                new GetWorkspaceBatchExtractionPreviewQuery(
                    currentWorkspaceSummary.WorkspaceId,
                    SelectedBatchExtractAssetTypeOption?.AssetType));

            if (!TryGetValue(batchExtractionPreviewResult, out var batchExtractionPreview))
            {
                return;
            }

            var batchReplacementPreviewResult = await _workspaceBatchOperationsGateway.GetWorkspaceBatchReplacementPreviewAsync(
                new GetWorkspaceBatchReplacementPreviewQuery(
                    currentWorkspaceSummary.WorkspaceId,
                    SelectedBatchReplaceSourceOption?.SourceKind ?? BatchReplacementSourceKind.All));

            if (!TryGetValue(batchReplacementPreviewResult, out var batchReplacementPreview))
            {
                return;
            }

            _presentationPresenter.ApplyWorkspaceSummary(summary);
            _presentationPresenter.ApplyBatchExtractionPreview(batchExtractionPreview);
            _presentationPresenter.ApplyBatchReplacementPreview(batchReplacementPreview);
            _presentationPresenter.ApplySavePreview(savePreview);
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

