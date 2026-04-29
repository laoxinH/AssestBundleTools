using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BatchTasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AssestBundleTools.Presentation.WinUI.Views.BatchTasks;

/// <summary>
/// 承载工作区变更摘要、批量提取、批量替换和保存前摘要的批处理任务页。
/// </summary>
public sealed partial class BatchTasksPage : Page
{
    public BatchTasksPage()
    {
        ViewModel = new BatchTasksViewModel(
            App.CurrentApp.WorkspaceBatchOperationsGateway,
            App.CurrentApp.WorkspaceContextService,
            App.CurrentApp.LocalizationService,
            App.CurrentApp.AppLogService,
            App.CurrentApp.WorkspaceImportCoordinatorService);

        InitializeComponent();
        ApplyLocalizedText();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public BatchTasksViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Dispose();
    }

    private void ApplyLocalizedText()
    {
        SetText(PageEyebrowText, ResourceKeys.BatchTasksPageEyebrow);
        SetText(PageTitleText, ResourceKeys.BatchTasksPageTitle);
        SetText(PageSubtitleText, ResourceKeys.BatchTasksPageSubtitle);
        SetText(WorkspaceOverviewTitleText, ResourceKeys.BatchTasksWorkspaceOverviewTitle);
        SetText(WorkspaceOverviewHintText, ResourceKeys.BatchTasksWorkspaceOverviewHint);
        SetText(ImportBundleFilesButtonText, ResourceKeys.WorkspaceImportFilesButtonText);
        SetText(ImportBundleDirectoryButtonText, ResourceKeys.WorkspaceImportDirectoryButtonText);
        SetContent(RefreshButton, ResourceKeys.BatchTasksRefreshButton);
        SetText(EmptyStateTitleText, ResourceKeys.BatchTasksEmptyStateTitle);
        SetText(EmptyStateBodyText, ResourceKeys.BatchTasksEmptyStateBody);
        SetText(SummaryTitleText, ResourceKeys.BatchTasksSummaryTitle);
        SetText(SummaryHintText, ResourceKeys.BatchTasksSummaryHint);
        SetText(BatchExtractTitleText, ResourceKeys.BatchTasksBatchExtractTitle);
        SetText(BatchExtractHintText, ResourceKeys.BatchTasksBatchExtractHint);
        SetText(BatchExtractScopeLabelText, ResourceKeys.BatchTasksBatchExtractScopeLabel);
        SetText(BatchExtractAssetTypeLabelText, ResourceKeys.BatchTasksBatchExtractAssetTypeLabel);
        SetText(BatchExtractOutputDirectoryLabelText, ResourceKeys.BatchTasksBatchExtractOutputDirectoryLabel);
        SetText(BatchExtractFileNamePolicyLabelText, ResourceKeys.BatchTasksBatchExtractFileNamePolicyLabel);
        SetText(BatchExtractConflictPolicyLabelText, ResourceKeys.BatchTasksBatchExtractConflictPolicyLabel);
        SetText(BatchExtractEmptyText, ResourceKeys.BatchTasksBatchExtractEmptyText);
        SetContent(BatchExtractExecuteButton, ResourceKeys.BatchTasksBatchExtractExecuteButton);
        SetText(BatchExtractLastRunLabelText, ResourceKeys.BatchTasksBatchExtractLastRunLabel);
        SetText(BatchReplaceTitleText, ResourceKeys.BatchTasksBatchReplaceTitle);
        SetText(BatchReplaceHintText, ResourceKeys.BatchTasksBatchReplaceHint);
        SetText(BatchReplaceScopeLabelText, ResourceKeys.BatchTasksBatchReplaceScopeLabel);
        SetText(BatchReplaceSourceFilterLabelText, ResourceKeys.BatchTasksBatchReplaceSourceFilterLabel);
        SetText(BatchReplaceEmptyText, ResourceKeys.BatchTasksBatchReplaceEmptyText);
        SetContent(BatchReplaceExecuteButton, ResourceKeys.BatchTasksBatchReplaceExecuteButton);
        SetText(BatchReplaceLastRunLabelText, ResourceKeys.BatchTasksBatchReplaceLastRunLabel);
        SetText(SavePreviewTitleText, ResourceKeys.BatchTasksSavePreviewTitle);
        SetText(SavePreviewHintText, ResourceKeys.BatchTasksSavePreviewHint);
        SetText(SavePreviewOutputDirectoryLabelText, ResourceKeys.BatchTasksSavePreviewOutputDirectoryLabel);
        SetText(SavePreviewFileNamePolicyLabelText, ResourceKeys.BatchTasksSavePreviewFileNamePolicyLabel);
        SetText(SavePreviewEmptyText, ResourceKeys.BatchTasksSavePreviewEmptyText);
        SetText(SavePreviewRiskTitleText, ResourceKeys.BatchTasksSavePreviewRiskTitle);
        SetContent(SaveExecuteButton, ResourceKeys.BatchTasksSaveExecuteButton);
        SetText(SaveLastRunLabelText, ResourceKeys.BatchTasksSaveLastRunLabel);
        SetText(StageNoteTitleText, ResourceKeys.BatchTasksStageNoteTitle);
        SetText(StageNoteBodyText, ResourceKeys.BatchTasksStageNoteBody);
        SetText(TaskResultsTitleText, ResourceKeys.BatchTasksTaskResultsTitle);
        SetText(TaskResultsHintText, ResourceKeys.BatchTasksTaskResultsHint);
        SetText(TaskResultsEmptyText, ResourceKeys.BatchTasksTaskResultsEmptyText);
        SetText(RecentChangesTitleText, ResourceKeys.BatchTasksRecentChangesTitle);
        SetText(RecentChangesHintText, ResourceKeys.BatchTasksRecentChangesHint);
        SetText(NoChangesText, ResourceKeys.BatchTasksNoChangesText);
    }

    private static void SetText(TextBlock textBlock, string resourceKey)
    {
        textBlock.Text = UiText.Get(resourceKey);
    }

    private static void SetContent(Button button, string resourceKey)
    {
        button.Content = UiText.Get(resourceKey);
    }
}
