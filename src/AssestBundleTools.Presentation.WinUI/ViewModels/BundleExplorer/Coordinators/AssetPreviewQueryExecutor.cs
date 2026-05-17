using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责把资产预览查询放入有界后台执行队列。
/// </summary>
internal sealed class AssetPreviewQueryExecutor
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly TimeSpan _previewQueryTimeout;
    private readonly Func<int> _previewWorkerCountProvider;
    private readonly Func<string>? _foregroundStatusTitleProvider;

    public AssetPreviewQueryExecutor(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        TimeSpan previewQueryTimeout,
        int previewWorkerCount,
        Func<int>? previewWorkerCountProvider = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        Func<string>? foregroundStatusTitleProvider = null)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        ArgumentNullException.ThrowIfNull(backgroundTaskRunner);
        _previewQueryTimeout = previewQueryTimeout > TimeSpan.Zero
            ? previewQueryTimeout
            : throw new ArgumentOutOfRangeException(nameof(previewQueryTimeout));
        _previewWorkerCountProvider = previewWorkerCountProvider
            ?? (() => AssetPreviewExecutionOptionsResolver.NormalizePreviewWorkerCount(previewWorkerCount));
        _backgroundWorkScheduler = backgroundWorkScheduler ?? new BackgroundWorkScheduler(
            backgroundTaskRunner,
            NullAppBackgroundTaskStatusService.Instance,
            _previewWorkerCountProvider);
        _foregroundStatusTitleProvider = foregroundStatusTitleProvider;
    }

    public async Task<Result<AssetPreviewDto>?> ExecuteAsync(
        GetAssetPreviewQuery previewQuery,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant,
        string? statusTitle = null)
    {
        ArgumentNullException.ThrowIfNull(previewQuery);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        if (priority == AssetPreviewLoadPriority.BackgroundPrewarm &&
            AssetPreviewExecutionOptionsResolver.NormalizePreviewWorkerCount(_previewWorkerCountProvider()) <= 1)
        {
            if (isPreviewRequestCurrentAndRelevant())
            {
                await Task.Yield();
            }

            return null;
        }

        return await _backgroundWorkScheduler.RunAsync(
            new BackgroundWorkRequest<Result<AssetPreviewDto>?>(
                ResolvePreviewProfile(priority),
                context =>
            {
                if (!isPreviewRequestCurrentAndRelevant())
                {
                    return Task.FromResult<Result<AssetPreviewDto>?>(null);
                }

                return GetPreviewAsync(previewQuery, context.CancellationToken);
            })
            {
                SemanticKind = BackgroundWorkSemanticKind.TransientRead,
                Priority = priority == AssetPreviewLoadPriority.Foreground
                    ? BackgroundWorkPriority.UserBlocking
                    : BackgroundWorkPriority.Background,
                WorkspaceId = previewQuery.WorkspaceId.Value,
                WorkspaceKey = previewQuery.WorkspaceKey,
                BundleCacheKey = previewQuery.BundleId.Value,
                ResourceKey = previewQuery.AssetId.Value,
                StatusTitle = priority == AssetPreviewLoadPriority.Foreground
                    ? ResolveForegroundStatusTitle(statusTitle)
                    : null,
                ShowInStatusBar = priority == AssetPreviewLoadPriority.Foreground,
                Timeout = _previewQueryTimeout,
                ReleaseProfileGateOnCallerCancellation = priority == AssetPreviewLoadPriority.Foreground
            },
            previewCancellationToken);
    }

    private async Task<Result<AssetPreviewDto>?> GetPreviewAsync(
        GetAssetPreviewQuery previewQuery,
        CancellationToken cancellationToken)
    {
        return await _workspaceAssetInspectionGateway.GetAssetPreviewAsync(previewQuery, cancellationToken);
    }

    private static BackgroundWorkProfile ResolvePreviewProfile(AssetPreviewLoadPriority priority) =>
        priority == AssetPreviewLoadPriority.Foreground
            ? BackgroundWorkProfile.PreviewForeground
            : BackgroundWorkProfile.PreviewPrewarm;

    private string? ResolveForegroundStatusTitle(string? statusTitle)
    {
        if (!string.IsNullOrWhiteSpace(statusTitle))
        {
            return statusTitle.Trim();
        }

        return _foregroundStatusTitleProvider?.Invoke();
    }
}
