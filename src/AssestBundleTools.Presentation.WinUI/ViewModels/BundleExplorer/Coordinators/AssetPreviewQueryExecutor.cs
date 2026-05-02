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
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly SemaphoreSlim _foregroundPreviewQueryGate = new(1, 1);
    private readonly SemaphoreSlim? _backgroundPreviewQueryGate;
    private readonly TimeSpan _previewQueryTimeout;

    public AssetPreviewQueryExecutor(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IBackgroundTaskRunner backgroundTaskRunner,
        TimeSpan previewQueryTimeout,
        int previewWorkerCount)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _backgroundTaskRunner = backgroundTaskRunner ?? throw new ArgumentNullException(nameof(backgroundTaskRunner));
        _previewQueryTimeout = previewQueryTimeout > TimeSpan.Zero
            ? previewQueryTimeout
            : throw new ArgumentOutOfRangeException(nameof(previewQueryTimeout));
        var normalizedPreviewWorkerCount = AssetPreviewExecutionOptionsResolver.NormalizePreviewWorkerCount(previewWorkerCount);
        var backgroundWorkerCount = normalizedPreviewWorkerCount - 1;
        _backgroundPreviewQueryGate = backgroundWorkerCount > 0
            ? new SemaphoreSlim(backgroundWorkerCount, backgroundWorkerCount)
            : null;
    }

    public async Task<Result<AssetPreviewDto>?> ExecuteAsync(
        GetAssetPreviewQuery previewQuery,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        ArgumentNullException.ThrowIfNull(previewQuery);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        var previewQueryGate = ResolvePreviewQueryGate(priority);
        if (previewQueryGate is null)
        {
            if (isPreviewRequestCurrentAndRelevant())
            {
                await Task.Yield();
            }

            return null;
        }

        using var timeoutCancellationSource = new CancellationTokenSource(_previewQueryTimeout);
        var linkedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCancellationSource.Token,
            previewCancellationToken);
        var cancellationToken = linkedCancellationSource.Token;
        Task<Result<AssetPreviewDto>>? previewTask = null;
        var shouldReleaseGate = false;
        var shouldDisposeLinkedCancellationSource = true;

        try
        {
            await previewQueryGate.WaitAsync(cancellationToken);
            shouldReleaseGate = true;
            if (!isPreviewRequestCurrentAndRelevant())
            {
                return null;
            }

            previewTask = _backgroundTaskRunner.RunAsync(
                token => _workspaceAssetInspectionGateway.GetAssetPreviewAsync(previewQuery, token),
                cancellationToken);
            return await previewTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (previewCancellationToken.IsCancellationRequested)
        {
            if (shouldReleaseGate && previewTask is not null && !previewTask.IsCompleted)
            {
                previewQueryGate.Release();
                _ = ObserveAbandonedPreviewTaskAsync(previewTask, linkedCancellationSource);
                shouldDisposeLinkedCancellationSource = false;
                shouldReleaseGate = false;
            }

            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellationSource.IsCancellationRequested)
        {
            if (shouldReleaseGate && previewTask is not null && !previewTask.IsCompleted)
            {
                previewQueryGate.Release();
                _ = ObserveAbandonedPreviewTaskAsync(previewTask, linkedCancellationSource);
                shouldDisposeLinkedCancellationSource = false;
                shouldReleaseGate = false;
            }

            throw new TimeoutException();
        }
        finally
        {
            if (shouldReleaseGate)
            {
                previewQueryGate.Release();
            }

            if (shouldDisposeLinkedCancellationSource)
            {
                linkedCancellationSource.Dispose();
            }
        }
    }

    private SemaphoreSlim? ResolvePreviewQueryGate(AssetPreviewLoadPriority priority) =>
        priority == AssetPreviewLoadPriority.Foreground
            ? _foregroundPreviewQueryGate
            : _backgroundPreviewQueryGate;

    private static async Task ObserveAbandonedPreviewTaskAsync(
        Task previewTask,
        CancellationTokenSource linkedCancellationSource)
    {
        try
        {
            await previewTask.ConfigureAwait(false);
        }
        catch
        {
            // 旧请求已被新的活动标签取代，异常只需被观察避免未处理任务噪声。
        }
        finally
        {
            linkedCancellationSource.Dispose();
        }
    }
}
