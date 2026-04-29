using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Storage.Streams;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 负责把应用层预览 DTO 转成检查器可直接渲染的 UI 状态。
/// </summary>
internal sealed class BundleExplorerAssetPreviewCoordinator
{
    private readonly IWorkspaceAssetInspectionGateway _workspaceAssetInspectionGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly LocalizedValueFormatter _localizedValueFormatter;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;

    public BundleExplorerAssetPreviewCoordinator(
        IWorkspaceAssetInspectionGateway workspaceAssetInspectionGateway,
        IWorkspaceContextService workspaceContextService,
        IBundleExplorerWorkspaceShellHost workspaceShell,
        IAppLogService appLogService,
        ILocalizationService localizationService)
    {
        _workspaceAssetInspectionGateway = workspaceAssetInspectionGateway ?? throw new ArgumentNullException(nameof(workspaceAssetInspectionGateway));
        _workspaceContextService = workspaceContextService ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
        _assetPreviewDisplayFormatter = new AssetPreviewDisplayFormatter(_localizationService);
    }

    public Task LoadInspectorPreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Func<IReadOnlyList<AppError>, string> formatFirstErrorMessage)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(formatFirstErrorMessage);

        var previewRequestVersion = assetTab.BeginPreviewRequest();
        assetTab.Inspector.SetPreviewLoading(GetString(ResourceKeys.InspectorPreviewLoadingText));
        return LoadInspectorPreviewCoreAsync(assetTab, details, previewRequestVersion, formatFirstErrorMessage);
    }

    private async Task LoadInspectorPreviewCoreAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        long previewRequestVersion,
        Func<IReadOnlyList<AppError>, string> formatFirstErrorMessage)
    {
        if (!TryCreateAssetPreviewQuery(details, out var previewQuery))
        {
            if (assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
            {
                assetTab.Inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
            }

            return;
        }

        var previewResult = await _workspaceAssetInspectionGateway.GetAssetPreviewAsync(previewQuery);
        if (previewResult.IsCancelled || !assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
        {
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
                    var imageSource = await CreatePreviewImageSourceAsync(preview.BinaryContent);
                    if (!assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
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
                        previewInfoRows,
                        preview.PreviewPayloadFilePath);
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
                    var audioPreviewSource = await CreatePreviewAudioSourceAsync(preview.BinaryContent, preview.ContentType);
                    if (!assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
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
            if (!assetTab.IsPreviewRequestCurrent(details, previewRequestVersion))
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

    private static async Task<BitmapImage?> CreatePreviewImageSourceAsync(byte[]? imagePayload)
    {
        if (imagePayload is not { Length: > 0 })
        {
            return null;
        }

        var bitmapImage = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(imagePayload);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        await bitmapImage.SetSourceAsync(stream);
        return bitmapImage;
    }

    private static async Task<AudioPreviewSource?> CreatePreviewAudioSourceAsync(byte[]? audioPayload, string contentType)
    {
        if (audioPayload is not { Length: > 0 } || string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var stream = new InMemoryRandomAccessStream();

        try
        {
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(audioPayload);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }

            stream.Seek(0);
            var mediaSource = MediaSource.CreateFromStream(stream, contentType.Trim());
            return new AudioPreviewSource(mediaSource, stream);
        }
        catch
        {
            stream.Dispose();
            return null;
        }
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

    private void LogPreviewDisplayFailure(string assetName, string reason)
    {
        _appLogService.AppendWarning(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
            assetName,
            reason));
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

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);

    private sealed record AudioPreviewSource(
        MediaSource MediaSource,
        IRandomAccessStream Stream);
}
