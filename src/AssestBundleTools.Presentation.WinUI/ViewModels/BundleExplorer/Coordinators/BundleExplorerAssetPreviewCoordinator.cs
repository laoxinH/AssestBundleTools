using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责调度应用层资产预览请求，并把呈现细节交给专用 Presenter。
/// </summary>
internal sealed class BundleExplorerAssetPreviewCoordinator
{
    private static readonly TimeSpan DefaultPreviewQueryTimeout = TimeSpan.FromSeconds(45);

    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly AssetPreviewQueryExecutor _previewQueryExecutor;
    private readonly AssetPreviewResultPresenter _previewResultPresenter;

    public BundleExplorerAssetPreviewCoordinator(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceContextService workspaceContextService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        IBackgroundTaskRunner backgroundTaskRunner,
        TimeSpan? previewQueryTimeout = null,
        int? previewWorkerCount = null,
        Func<int>? previewWorkerCountProvider = null,
        IBackgroundWorkScheduler? backgroundWorkScheduler = null)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        ArgumentNullException.ThrowIfNull(backgroundTaskRunner);
        _previewQueryExecutor = new AssetPreviewQueryExecutor(
            _workspaceAssetInspectionGateway,
            backgroundTaskRunner,
            NormalizePreviewQueryTimeout(previewQueryTimeout),
            AssetPreviewExecutionOptionsResolver.NormalizePreviewWorkerCount(previewWorkerCount),
            previewWorkerCountProvider,
            backgroundWorkScheduler,
            () => GetString(ResourceKeys.InspectorPreviewLoadingText));
        var texturePreviewPayloadLoader = new TexturePreviewPayloadLoader(_previewQueryExecutor);
        var meshTextureOverlayPreviewLoader = new MeshTextureOverlayPreviewLoader(texturePreviewPayloadLoader);
        var materialTexturePreviewLoader = new MaterialTexturePreviewLoader(texturePreviewPayloadLoader);
        _previewResultPresenter = new AssetPreviewResultPresenter(
            meshTextureOverlayPreviewLoader,
            materialTexturePreviewLoader,
            _appLogService,
            _localizationService);
    }

    public async Task LoadInspectorPreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Func<IReadOnlyList<AppError>, string> formatFirstErrorMessage,
        Func<bool>? isPreviewRelevant = null,
        AssetPreviewLoadPriority priority = AssetPreviewLoadPriority.Foreground)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(formatFirstErrorMessage);

        var previewRequest = assetTab.BeginPreviewRequest();
        if (!IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
        {
            ResetPreviewIfCurrent(assetTab, details, previewRequest);
            return;
        }

        assetTab.Inspector.SetPreviewLoading(GetString(ResourceKeys.InspectorPreviewLoadingText));
        try
        {
            await LoadInspectorPreviewCoreAsync(
                assetTab,
                details,
                previewRequest,
                formatFirstErrorMessage,
                priority,
                isPreviewRelevant);
        }
        catch (OperationCanceledException)
        {
            ResetPreviewIfCurrent(assetTab, details, previewRequest);
        }
        catch (TimeoutException)
        {
            if (!ApplyUnavailablePreviewIfCurrentAndRelevant(
                    assetTab,
                    details,
                    previewRequest,
                    isPreviewRelevant,
                    CreatePreviewFailureDiagnostic(new LocalizedMessageDto(
                        MessageToken: ResourceKeys.BundleExplorerPreviewLoadTimedOut))))
            {
                ResetPreviewIfCurrent(assetTab, details, previewRequest);
            }
            else
            {
                LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewLoadTimedOut));
            }
        }
        catch (Exception exception)
        {
            if (!ApplyUnavailablePreviewIfCurrentAndRelevant(
                    assetTab,
                    details,
                    previewRequest,
                    isPreviewRelevant,
                    CreatePreviewFailureDiagnostic(CreateExceptionDiagnosticMessage(exception))))
            {
                ResetPreviewIfCurrent(assetTab, details, previewRequest);
            }
            else
            {
                LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewUnexpectedDisplayFailure));
            }
        }
        finally
        {
            assetTab.CompletePreviewRequest(previewRequest);
        }
    }

    private async Task LoadInspectorPreviewCoreAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest,
        Func<IReadOnlyList<AppError>, string> formatFirstErrorMessage,
        AssetPreviewLoadPriority priority,
        Func<bool>? isPreviewRelevant)
    {
        if (!TryCreateAssetPreviewQuery(details, out var previewQuery))
        {
            if (!ApplyUnavailablePreviewIfCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
            {
                ResetPreviewIfCurrent(assetTab, details, previewRequest);
            }

            return;
        }

        var previewResult = await GetPreviewResultWithTimeoutAsync(
            previewQuery,
            previewRequest.CancellationToken,
            priority,
            () => IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant),
            CreatePreviewStatusTitle(details));
        if (previewResult is null)
        {
            ResetPreviewIfCurrent(assetTab, details, previewRequest);
            return;
        }

        if (!IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
        {
            ResetPreviewIfCurrent(assetTab, details, previewRequest);
            return;
        }

        if (previewResult.IsCancelled)
        {
            if (!ApplyUnavailablePreviewIfCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
            {
                ResetPreviewIfCurrent(assetTab, details, previewRequest);
            }

            return;
        }

        if (!previewResult.IsSuccess || previewResult.Value is null)
        {
            _appLogService.AppendWarning(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
                details.Name,
                formatFirstErrorMessage(previewResult.Errors)));
            assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            return;
        }

        var preview = previewResult.Value;
        try
        {
            if (!await _previewResultPresenter.TryApplyAsync(
                assetTab,
                details,
                previewQuery,
                previewRequest,
                preview,
                priority,
                () => IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant)))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
            {
                return;
            }

            LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewUnexpectedDisplayFailure));
            _previewResultPresenter.ApplyUnavailablePreview(
                assetTab.Inspector,
                ReplacePreviewDiagnosticDetail(preview.Diagnostic, CreateExceptionDiagnosticMessage(exception)));
        }
    }

    private bool TryCreateAssetPreviewQuery(
        AssetDetailsDto details,
        [NotNullWhen(true)] out GetAssetPreviewQuery? previewQuery)
    {
        if (_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) &&
            workspaceEntry is not null &&
            !string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceKey) &&
            !string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceDataRootPath))
        {
            previewQuery = new GetAssetPreviewQuery(
                WorkspaceId: details.WorkspaceId,
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                WorkspaceDataRootPath: workspaceEntry.WorkspaceDataRootPath,
                BundleId: details.BundleId,
                AssetId: details.AssetId);
            return true;
        }

        var activeWorkspaceNavigationRequest = _workspaceShell.ActiveWorkspaceNavigationRequest;
        if (activeWorkspaceNavigationRequest is null ||
            string.IsNullOrWhiteSpace(activeWorkspaceNavigationRequest.WorkspaceDataRootPath))
        {
            previewQuery = null;
            return false;
        }

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            activeWorkspaceNavigationRequest.SourcePath,
            activeWorkspaceNavigationRequest.WorkspaceDataRootPath);
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            previewQuery = null;
            return false;
        }

        previewQuery = new GetAssetPreviewQuery(
            WorkspaceId: details.WorkspaceId,
            WorkspaceKey: workspaceKey,
            WorkspaceDataRootPath: activeWorkspaceNavigationRequest.WorkspaceDataRootPath,
            BundleId: details.BundleId,
            AssetId: details.AssetId);
        return true;
    }

    private bool ApplyUnavailablePreviewIfCurrentAndRelevant(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest,
        Func<bool>? isPreviewRelevant,
        AssetPreviewDiagnosticDto? diagnostic = null)
    {
        if (!IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
        {
            return false;
        }

        if (diagnostic is null)
        {
            assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
        }
        else
        {
            _previewResultPresenter.ApplyUnavailablePreview(assetTab.Inspector, diagnostic);
        }

        return true;
    }

    private void ResetPreviewIfCurrent(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest)
    {
        if (assetTab.IsPreviewRequestCurrent(details, previewRequest))
        {
            assetTab.Inspector.Preview.ResetState();
        }
    }

    private async Task<Result<AssetPreviewDto>?> GetPreviewResultWithTimeoutAsync(
        GetAssetPreviewQuery previewQuery,
        CancellationToken previewCancellationToken,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant,
        string? statusTitle = null)
    {
        return await _previewQueryExecutor.ExecuteAsync(
            previewQuery,
            previewCancellationToken,
            priority,
            isPreviewRequestCurrentAndRelevant,
            statusTitle);
    }

    private void LogPreviewDisplayFailure(string assetName, string reason)
    {
        _appLogService.AppendWarning(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
            assetName,
            reason));
    }

    private static AssetPreviewDiagnosticDto CreatePreviewFailureDiagnostic(LocalizedMessageDto detailMessage)
    {
        return new AssetPreviewDiagnosticDto(
            CacheStatus: AssetPreviewCacheStatusKind.Missing,
            FallbackReason: AssetPreviewFallbackReasonKind.GenerationFailed,
            DetailMessage: detailMessage);
    }

    private static AssetPreviewDiagnosticDto ReplacePreviewDiagnosticDetail(
        AssetPreviewDiagnosticDto diagnostic,
        LocalizedMessageDto detailMessage)
    {
        return diagnostic with
        {
            FallbackReason = diagnostic.FallbackReason == AssetPreviewFallbackReasonKind.None
                ? AssetPreviewFallbackReasonKind.GenerationFailed
                : diagnostic.FallbackReason,
            DetailMessage = detailMessage
        };
    }

    private static LocalizedMessageDto CreateExceptionDiagnosticMessage(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new LocalizedMessageDto(FallbackText: exception.ToString());
    }

    private static TimeSpan NormalizePreviewQueryTimeout(TimeSpan? previewQueryTimeout)
    {
        return previewQueryTimeout is { } timeout && timeout > TimeSpan.Zero
            ? timeout
            : DefaultPreviewQueryTimeout;
    }

    private static bool IsPreviewRequestCurrentAndRelevant(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest,
        Func<bool>? isPreviewRelevant)
    {
        return assetTab.IsPreviewRequestCurrent(details, previewRequest) &&
               (isPreviewRelevant is null || isPreviewRelevant());
    }

    private string CreatePreviewStatusTitle(AssetDetailsDto details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var displayName = string.IsNullOrWhiteSpace(details.Name)
            ? details.AssetId.Value
            : details.Name.Trim();
        return _localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerStatusLoadPreviewFormat,
            displayName);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
