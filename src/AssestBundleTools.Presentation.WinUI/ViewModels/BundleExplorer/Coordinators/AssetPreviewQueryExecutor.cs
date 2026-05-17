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
    private static readonly TimeSpan DefaultBackgroundPrewarmRetreatDuration = TimeSpan.FromMinutes(2);

    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IBackgroundWorkScheduler _backgroundWorkScheduler;
    private readonly TimeSpan _previewQueryTimeout;
    private readonly TimeSpan _backgroundPrewarmRetreatDuration;
    private readonly Func<int> _previewWorkerCountProvider;
    private readonly Func<string>? _foregroundStatusTitleProvider;
    private readonly Func<DateTimeOffset> _utcNowProvider;
    private readonly Dictionary<string, DateTimeOffset> _prewarmRetreatUntilByBundleKey = new(StringComparer.Ordinal);
    private readonly object _prewarmRetreatSyncRoot = new();

    public AssetPreviewQueryExecutor(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        TimeSpan previewQueryTimeout,
        int previewWorkerCount,
        Func<int>? previewWorkerCountProvider = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null,
        Func<string>? foregroundStatusTitleProvider = null,
        TimeSpan? backgroundPrewarmRetreatDuration = null,
        Func<DateTimeOffset>? utcNowProvider = null)
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
        _backgroundPrewarmRetreatDuration = backgroundPrewarmRetreatDuration is { } duration && duration > TimeSpan.Zero
            ? duration
            : DefaultBackgroundPrewarmRetreatDuration;
        _utcNowProvider = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
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

        if (priority == AssetPreviewLoadPriority.BackgroundPrewarm &&
            IsBundlePrewarmRetreated(previewQuery))
        {
            if (isPreviewRequestCurrentAndRelevant())
            {
                await Task.Yield();
            }

            return null;
        }

        var previewResult = await _backgroundWorkScheduler.RunAsync(
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
        UpdateBundlePrewarmRetreat(previewQuery, previewResult);
        return previewResult;
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

    private bool IsBundlePrewarmRetreated(GetAssetPreviewQuery previewQuery)
    {
        var retreatKey = CreateBundlePrewarmRetreatKey(previewQuery);
        var now = _utcNowProvider();
        lock (_prewarmRetreatSyncRoot)
        {
            if (!_prewarmRetreatUntilByBundleKey.TryGetValue(retreatKey, out var retreatUntil))
            {
                return false;
            }

            if (retreatUntil > now)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(
                    $"preview.prewarm retreat-skip workspace={previewQuery.WorkspaceKey} bundle={previewQuery.BundleId.Value} until={retreatUntil:O}");
#endif
                return true;
            }

            _prewarmRetreatUntilByBundleKey.Remove(retreatKey);
            return false;
        }
    }

    private void UpdateBundlePrewarmRetreat(
        GetAssetPreviewQuery previewQuery,
        Result<AssetPreviewDto>? previewResult)
    {
        if (previewResult is not { IsSuccess: true, Value: { } preview })
        {
            return;
        }

        if (preview.Diagnostic.FallbackReason == AssetPreviewFallbackReasonKind.OutOfMemory)
        {
            MarkBundlePrewarmRetreated(previewQuery);
            return;
        }

        ClearBundlePrewarmRetreat(previewQuery);
    }

    private void MarkBundlePrewarmRetreated(GetAssetPreviewQuery previewQuery)
    {
        var retreatKey = CreateBundlePrewarmRetreatKey(previewQuery);
        var retreatUntil = _utcNowProvider().Add(_backgroundPrewarmRetreatDuration);
        lock (_prewarmRetreatSyncRoot)
        {
            _prewarmRetreatUntilByBundleKey[retreatKey] = retreatUntil;
        }

#if DEBUG
        System.Diagnostics.Debug.WriteLine(
            $"preview.prewarm retreat-set workspace={previewQuery.WorkspaceKey} bundle={previewQuery.BundleId.Value} until={retreatUntil:O}");
#endif
    }

    private void ClearBundlePrewarmRetreat(GetAssetPreviewQuery previewQuery)
    {
        var retreatKey = CreateBundlePrewarmRetreatKey(previewQuery);
        var removed = false;
        lock (_prewarmRetreatSyncRoot)
        {
            removed = _prewarmRetreatUntilByBundleKey.Remove(retreatKey);
        }

#if DEBUG
        if (removed)
        {
            System.Diagnostics.Debug.WriteLine(
                $"preview.prewarm retreat-clear workspace={previewQuery.WorkspaceKey} bundle={previewQuery.BundleId.Value}");
        }
#endif
    }

    private static string CreateBundlePrewarmRetreatKey(GetAssetPreviewQuery previewQuery) =>
        $"{previewQuery.WorkspaceKey}\u001f{previewQuery.BundleId.Value}";

    private string? ResolveForegroundStatusTitle(string? statusTitle)
    {
        if (!string.IsNullOrWhiteSpace(statusTitle))
        {
            return statusTitle.Trim();
        }

        return _foregroundStatusTitleProvider?.Invoke();
    }
}
