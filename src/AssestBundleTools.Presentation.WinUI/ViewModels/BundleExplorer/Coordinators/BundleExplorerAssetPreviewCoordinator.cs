using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责把应用层预览 DTO 转成检查器可直接渲染的 UI 状态。
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
    private readonly LocalizedValueFormatter _localizedValueFormatter;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;

    public BundleExplorerAssetPreviewCoordinator(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceContextService workspaceContextService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IAppLogService appLogService,
        ILocalizationService localizationService,
        IBackgroundTaskRunner backgroundTaskRunner,
        TimeSpan? previewQueryTimeout = null,
        int? previewWorkerCount = null)
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
            AssetPreviewExecutionOptionsResolver.NormalizePreviewWorkerCount(previewWorkerCount));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
        _assetPreviewDisplayFormatter = new AssetPreviewDisplayFormatter(_localizationService);
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
            if (!ApplyUnavailablePreviewIfCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
            {
                ResetPreviewIfCurrent(assetTab, details, previewRequest);
            }
            else
            {
                LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewLoadTimedOut));
            }
        }
        catch (Exception)
        {
            if (!ApplyUnavailablePreviewIfCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
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
            () => IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant));
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
        var previewInfoRows = _assetPreviewDisplayFormatter.BuildPreviewInfoRows(preview.PreviewInfoItems);
        if (preview.PreferredDisplaySizeInBytes is { } preferredDisplaySizeInBytes && preferredDisplaySizeInBytes > 0)
        {
            assetTab.Inspector.OverrideSizeText(_localizedValueFormatter.FormatCompactByteSize(preferredDisplaySizeInBytes));
        }

        try
        {
            switch (preview.Kind)
            {
                case AssetPreviewKind.Image:
                {
                    var imageSource = await AssetPreviewMediaSourceFactory.CreateImageSourceAsync(preview.BinaryContent);
                    var spriteFocusPreviewDisplayState = await CreateSpriteFocusPreviewDisplayStateAsync(preview);
                    if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
                    {
                        return;
                    }

                    if (imageSource is null)
                    {
                        LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewImageDecodeFailed));
                        ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplyImagePreview(
                        imageSource,
                        imageSource.PixelWidth,
                        imageSource.PixelHeight,
                        previewInfoRows,
                        preview.PreviewPayloadFilePath,
                        _assetPreviewDisplayFormatter.BuildSpritePreviewOverlayDisplayState(preview),
                        spriteFocusPreviewDisplayState);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Text:
                {
                    if (string.IsNullOrWhiteSpace(preview.TextContent))
                    {
                        ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplyTextPreview(
                        preview.TextContent,
                        previewInfoRows,
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Audio:
                {
                    var audioPreviewSource = await AssetPreviewMediaSourceFactory.CreateAudioSourceAsync(preview.BinaryContent, preview.ContentType);
                    if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
                    {
                        audioPreviewSource?.Stream.Dispose();
                        return;
                    }

                    if (audioPreviewSource is null)
                    {
                        LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewAudioDecodeFailed));
                        ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplyAudioPreview(
                        audioPreviewSource.MediaSource,
                        audioPreviewSource.Stream,
                        previewInfoRows,
                        _assetPreviewDisplayFormatter.BuildAudioPreviewDisplayState(
                            preview.PreviewInfoItems,
                            preview.PreferredDisplaySizeInBytes),
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                case AssetPreviewKind.Summary:
                {
                    var summaryText = !string.IsNullOrWhiteSpace(preview.TextContent)
                        ? preview.TextContent
                        : _assetPreviewDisplayFormatter.BuildPreviewMessageText(preview.PreviewMessage);

                    if (string.IsNullOrWhiteSpace(summaryText))
                    {
                        ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
                        return;
                    }

                    assetTab.Inspector.ApplySummaryPreview(
                        summaryText,
                        previewInfoRows,
                        preview.PreviewPayloadFilePath);
                    ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
                    return;
                }
                default:
                    ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
                    return;
            }
        }
        catch (Exception)
        {
            if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
            {
                return;
            }

            LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewUnexpectedDisplayFailure));
            ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
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

    /// <summary>
    /// 应用层预览诊断只暴露稳定枚举和结构化 detail message，
    /// 真正面向用户的中文文案在 Presentation 层完成映射，便于后续工作台和诊断页复用同一份诊断语义。
    /// </summary>
    private void ApplyPreviewDiagnostic(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ApplyPreviewDiagnostic(
            cacheStatusText: GetPreviewCacheStatusText(diagnostic.CacheStatus),
            fallbackReasonText: GetPreviewFallbackReasonText(diagnostic.FallbackReason),
            diagnosticDetailText: LocalizedMessageDisplayFormatter.Format(
                diagnostic.DetailMessage,
                _localizationService));
    }

    private void ApplyUnavailablePreview(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
        ApplyPreviewDiagnostic(inspector, diagnostic);
    }

    private bool ApplyUnavailablePreviewIfCurrentAndRelevant(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest,
        Func<bool>? isPreviewRelevant)
    {
        if (!IsPreviewRequestCurrentAndRelevant(assetTab, details, previewRequest, isPreviewRelevant))
        {
            return false;
        }

        assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
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
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        return await _previewQueryExecutor.ExecuteAsync(
            previewQuery,
            previewCancellationToken,
            priority,
            isPreviewRequestCurrentAndRelevant);
    }

    private void LogPreviewDisplayFailure(string assetName, string reason)
    {
        _appLogService.AppendWarning(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
            assetName,
            reason));
    }

    private async Task<SpriteFocusPreviewDisplayState?> CreateSpriteFocusPreviewDisplayStateAsync(AssetPreviewDto preview)
    {
        var imageFocus = preview.ImageFocus;
        if (imageFocus?.BinaryContent is not { Length: > 0 })
        {
            return null;
        }

        var imageSource = await AssetPreviewMediaSourceFactory.CreateImageSourceAsync(imageFocus.BinaryContent);
        if (imageSource is null)
        {
            return null;
        }

        return new SpriteFocusPreviewDisplayState(
            imageSource,
            GetString(ResourceKeys.InspectorSpriteFocusPreviewTitle),
            _localizationService.GetFormattedString(
                ResourceKeys.InspectorSpriteFocusPreviewSizeFormat,
                imageFocus.PixelWidth,
                imageFocus.PixelHeight));
    }

    private string GetPreviewCacheStatusText(AssetPreviewCacheStatusKind cacheStatus)
    {
        return cacheStatus switch
        {
            AssetPreviewCacheStatusKind.Hit => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusHit),
            AssetPreviewCacheStatusKind.Missing => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusMissing),
            AssetPreviewCacheStatusKind.IdentityChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusIdentityChanged),
            AssetPreviewCacheStatusKind.SourceFingerprintChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusSourceFingerprintChanged),
            AssetPreviewCacheStatusKind.GeneratorVersionChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusGeneratorVersionChanged),
            AssetPreviewCacheStatusKind.AudioTranscoderChanged => GetString(ResourceKeys.InspectorPreviewDiagnosticCacheStatusAudioTranscoderChanged),
            _ => cacheStatus.ToString()
        };
    }

    private string GetPreviewFallbackReasonText(AssetPreviewFallbackReasonKind fallbackReason)
    {
        return fallbackReason switch
        {
            AssetPreviewFallbackReasonKind.None => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonNone),
            AssetPreviewFallbackReasonKind.MissingLocator => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonMissingLocator),
            AssetPreviewFallbackReasonKind.DecryptorResolveFailed => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonDecryptorResolveFailed),
            AssetPreviewFallbackReasonKind.DecryptionFailed => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonDecryptionFailed),
            AssetPreviewFallbackReasonKind.GenerationFailed => GetString(ResourceKeys.InspectorPreviewDiagnosticFallbackReasonGenerationFailed),
            _ => fallbackReason.ToString()
        };
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

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

}
