using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using Microsoft.UI.Xaml.Media;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 负责把应用层预览 DTO 呈现到检查器，保持预览请求编排器不继续承担类型分发细节。
/// </summary>
internal sealed class AssetPreviewResultPresenter
{
    private readonly MeshTextureOverlayPreviewLoader _meshTextureOverlayPreviewLoader;
    private readonly MaterialTexturePreviewLoader _materialTexturePreviewLoader;
    private readonly IAppLogService _appLogService;
    private readonly ILocalizationService _localizationService;
    private readonly LocalizedValueFormatter _localizedValueFormatter;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;
    private readonly MaterialPreviewDisplayFormatter _materialPreviewDisplayFormatter;
    private readonly AnimationClipPreviewDisplayFormatter _animationClipPreviewDisplayFormatter;
    private readonly FontPreviewDisplayFormatter _fontPreviewDisplayFormatter;
    private readonly GameObjectPreviewDisplayFormatter _gameObjectPreviewDisplayFormatter;
    private readonly MonoBehaviourPreviewDisplayFormatter _monoBehaviourPreviewDisplayFormatter;
    private readonly AnimatorControllerPreviewDisplayFormatter _animatorControllerPreviewDisplayFormatter;
    private readonly PreviewDiagnosticDisplayTextResolver _previewDiagnosticDisplayTextResolver;

    public AssetPreviewResultPresenter(
        MeshTextureOverlayPreviewLoader meshTextureOverlayPreviewLoader,
        MaterialTexturePreviewLoader materialTexturePreviewLoader,
        IAppLogService appLogService,
        ILocalizationService localizationService)
    {
        _meshTextureOverlayPreviewLoader = meshTextureOverlayPreviewLoader ?? throw new ArgumentNullException(nameof(meshTextureOverlayPreviewLoader));
        _materialTexturePreviewLoader = materialTexturePreviewLoader ?? throw new ArgumentNullException(nameof(materialTexturePreviewLoader));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizedValueFormatter = new LocalizedValueFormatter(_localizationService);
        _assetPreviewDisplayFormatter = new AssetPreviewDisplayFormatter(_localizationService);
        _materialPreviewDisplayFormatter = new MaterialPreviewDisplayFormatter(_localizationService);
        _animationClipPreviewDisplayFormatter = new AnimationClipPreviewDisplayFormatter(_localizationService);
        _fontPreviewDisplayFormatter = new FontPreviewDisplayFormatter(_localizationService);
        _gameObjectPreviewDisplayFormatter = new GameObjectPreviewDisplayFormatter(_localizationService);
        _monoBehaviourPreviewDisplayFormatter = new MonoBehaviourPreviewDisplayFormatter(_localizationService);
        _animatorControllerPreviewDisplayFormatter = new AnimatorControllerPreviewDisplayFormatter(_localizationService);
        _previewDiagnosticDisplayTextResolver = new PreviewDiagnosticDisplayTextResolver(_localizationService);
    }

    public async Task<bool> TryApplyAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        GetAssetPreviewQuery previewQuery,
        AssetPreviewRequest previewRequest,
        AssetPreviewDto preview,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(previewQuery);
        ArgumentNullException.ThrowIfNull(previewRequest);
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(isPreviewRequestCurrentAndRelevant);

        var previewInfoRows = _assetPreviewDisplayFormatter.BuildPreviewInfoRows(preview.PreviewInfoItems);
        if (preview.PreferredDisplaySizeInBytes is { } preferredDisplaySizeInBytes && preferredDisplaySizeInBytes > 0)
        {
            assetTab.Inspector.OverrideSizeText(_localizedValueFormatter.FormatCompactByteSize(preferredDisplaySizeInBytes));
        }

        switch (preview.Kind)
        {
            case AssetPreviewKind.Image:
                return await TryApplyImagePreviewAsync(assetTab, details, previewRequest, preview, previewInfoRows);
            case AssetPreviewKind.Text:
                ApplyTextPreview(assetTab.Inspector, preview, previewInfoRows);
                return true;
            case AssetPreviewKind.Audio:
                return await TryApplyAudioPreviewAsync(assetTab, details, previewRequest, preview, previewInfoRows);
            case AssetPreviewKind.Mesh:
                return await TryApplyMeshPreviewAsync(
                    assetTab,
                    details,
                    previewQuery,
                    previewRequest,
                    preview,
                    previewInfoRows,
                    priority,
                    isPreviewRequestCurrentAndRelevant);
            case AssetPreviewKind.Material:
                return await TryApplyMaterialPreviewAsync(
                    assetTab,
                    details,
                    previewQuery,
                    previewRequest,
                    preview,
                    previewInfoRows,
                    priority,
                    isPreviewRequestCurrentAndRelevant);
            case AssetPreviewKind.AnimationClip:
                ApplyAnimationClipPreview(assetTab.Inspector, details, preview, previewInfoRows);
                return true;
            case AssetPreviewKind.Font:
                ApplyFontPreview(assetTab.Inspector, details, preview, previewInfoRows);
                return true;
            case AssetPreviewKind.GameObject:
                ApplyGameObjectPreview(assetTab.Inspector, details, preview, previewInfoRows);
                return true;
            case AssetPreviewKind.MonoBehaviour:
                ApplyMonoBehaviourPreview(assetTab.Inspector, details, preview, previewInfoRows);
                return true;
            case AssetPreviewKind.AnimatorController:
                ApplyAnimatorControllerPreview(assetTab.Inspector, details, preview, previewInfoRows);
                return true;
            case AssetPreviewKind.Summary:
                ApplySummaryPreview(assetTab.Inspector, preview, previewInfoRows);
                return true;
            default:
                ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
                return true;
        }
    }

    public void ApplyUnavailablePreview(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ShowPreviewMessage(GetString(ResourceKeys.InspectorPreviewUnavailableText));
        ApplyPreviewDiagnostic(inspector, diagnostic);
    }

    private async Task<bool> TryApplyImagePreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        var imageSource = await AssetPreviewMediaSourceFactory.CreateImageSourceAsync(preview.BinaryContent);
        var spriteFocusPreviewDisplayState = await CreateSpriteFocusPreviewDisplayStateAsync(preview);
        if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
        {
            return false;
        }

        if (imageSource is null)
        {
            LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewImageDecodeFailed));
            ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
            return true;
        }

        assetTab.Inspector.ApplyImagePreview(
            imageSource,
            imageSource.PixelWidth,
            imageSource.PixelHeight,
            previewInfoRows,
            preview.PreviewPayloadFilePath,
            _assetPreviewDisplayFormatter.BuildSpritePreviewOverlayDisplayState(preview),
            spriteFocusPreviewDisplayState,
            _assetPreviewDisplayFormatter.BuildImagePreviewInfoDisplayState(preview.PreviewInfoItems));
        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
        return true;
    }

    private void ApplyTextPreview(
        InspectorPanelViewModel inspector,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (string.IsNullOrWhiteSpace(preview.TextContent))
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplyTextPreview(
            preview.TextContent,
            previewInfoRows,
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private async Task<bool> TryApplyAudioPreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        AssetPreviewRequest previewRequest,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        var audioPreviewSource = await AssetPreviewMediaSourceFactory.CreateAudioSourceAsync(preview.BinaryContent, preview.ContentType);
        if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
        {
            audioPreviewSource?.Stream.Dispose();
            return false;
        }

        if (audioPreviewSource is null)
        {
            LogPreviewDisplayFailure(details.Name, GetString(ResourceKeys.BundleExplorerPreviewAudioDecodeFailed));
            ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
            return true;
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
        return true;
    }

    private async Task<bool> TryApplyMeshPreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        GetAssetPreviewQuery previewQuery,
        AssetPreviewRequest previewRequest,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        if (preview.MeshGeometry is null)
        {
            ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
            return true;
        }

        var textureOverlayContent = await _meshTextureOverlayPreviewLoader.LoadAsync(
            details,
            previewQuery,
            previewRequest.CancellationToken,
            priority,
            isPreviewRequestCurrentAndRelevant);
        if (!isPreviewRequestCurrentAndRelevant())
        {
            return false;
        }

        assetTab.Inspector.ApplyMeshPreview(
            preview.MeshGeometry,
            previewInfoRows,
            _assetPreviewDisplayFormatter.BuildMeshPreviewDisplayState(preview, details),
            preview.PreviewPayloadFilePath,
            textureOverlayContent);
        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
        return true;
    }

    private async Task<bool> TryApplyMaterialPreviewAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        GetAssetPreviewQuery previewQuery,
        AssetPreviewRequest previewRequest,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        if (preview.Material is null)
        {
            ApplyUnavailablePreview(assetTab.Inspector, preview.Diagnostic);
            return true;
        }

        var textureThumbnailSources = await LoadMaterialTextureThumbnailSourcesAsync(
            assetTab,
            details,
            previewQuery,
            previewRequest,
            preview.Material,
            priority,
            isPreviewRequestCurrentAndRelevant);
        if (!isPreviewRequestCurrentAndRelevant())
        {
            return false;
        }

        assetTab.Inspector.ApplyMaterialPreview(
            preview.Material,
            previewInfoRows,
            _materialPreviewDisplayFormatter.BuildDisplayState(preview.Material, details, textureThumbnailSources),
            preview.PreviewPayloadFilePath,
            textureThumbnailSources);
        ApplyPreviewDiagnostic(assetTab.Inspector, preview.Diagnostic);
        return true;
    }

    private async Task<IReadOnlyDictionary<string, ImageSource>> LoadMaterialTextureThumbnailSourcesAsync(
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        GetAssetPreviewQuery previewQuery,
        AssetPreviewRequest previewRequest,
        AssetPreviewMaterialDto material,
        AssetPreviewLoadPriority priority,
        Func<bool> isPreviewRequestCurrentAndRelevant)
    {
        var thumbnailPayloads = await _materialTexturePreviewLoader.LoadAsync(
            details,
            material,
            previewQuery,
            previewRequest.CancellationToken,
            priority,
            isPreviewRequestCurrentAndRelevant);
        if (thumbnailPayloads.Count == 0 ||
            !assetTab.IsPreviewRequestCurrent(details, previewRequest))
        {
            return new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        }

        var thumbnailSources = new Dictionary<string, ImageSource>(StringComparer.Ordinal);
        foreach (var (assetId, payload) in thumbnailPayloads)
        {
            if (!assetTab.IsPreviewRequestCurrent(details, previewRequest))
            {
                return new Dictionary<string, ImageSource>(StringComparer.Ordinal);
            }

            var imageSource = await TryCreateThumbnailSourceAsync(payload);
            if (imageSource is not null)
            {
                thumbnailSources[assetId] = imageSource;
            }
        }

        return thumbnailSources;
    }

    private static async Task<ImageSource?> TryCreateThumbnailSourceAsync(byte[] imagePayload)
    {
        try
        {
            return await AssetPreviewMediaSourceFactory.CreateImageSourceAsync(imagePayload);
        }
        catch
        {
            return null;
        }
    }

    private void ApplyAnimationClipPreview(
        InspectorPanelViewModel inspector,
        AssetDetailsDto details,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (preview.AnimationClip is null)
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplyAnimationClipPreview(
            preview.AnimationClip,
            previewInfoRows,
            _animationClipPreviewDisplayFormatter.BuildDisplayState(preview.AnimationClip, details.Name),
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private void ApplyFontPreview(
        InspectorPanelViewModel inspector,
        AssetDetailsDto details,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (preview.Font is null)
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplyFontPreview(
            preview.Font,
            previewInfoRows,
            _fontPreviewDisplayFormatter.BuildDisplayState(preview.Font, details.Name),
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private void ApplyGameObjectPreview(
        InspectorPanelViewModel inspector,
        AssetDetailsDto details,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (preview.GameObject is null)
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplyGameObjectPreview(
            preview.GameObject,
            previewInfoRows,
            _gameObjectPreviewDisplayFormatter.BuildDisplayState(preview.GameObject, details),
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private void ApplyMonoBehaviourPreview(
        InspectorPanelViewModel inspector,
        AssetDetailsDto details,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (preview.MonoBehaviour is null)
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplyMonoBehaviourPreview(
            preview.MonoBehaviour,
            previewInfoRows,
            _monoBehaviourPreviewDisplayFormatter.BuildDisplayState(preview.MonoBehaviour, details),
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private void ApplyAnimatorControllerPreview(
        InspectorPanelViewModel inspector,
        AssetDetailsDto details,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (preview.AnimatorController is null)
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplyAnimatorControllerPreview(
            preview.AnimatorController,
            previewInfoRows,
            _animatorControllerPreviewDisplayFormatter.BuildDisplayState(preview.AnimatorController, details),
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private void ApplySummaryPreview(
        InspectorPanelViewModel inspector,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        var summaryText = !string.IsNullOrWhiteSpace(preview.TextContent)
            ? preview.TextContent
            : _assetPreviewDisplayFormatter.BuildPreviewMessageText(preview.PreviewMessage);

        if (string.IsNullOrWhiteSpace(summaryText))
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        inspector.ApplySummaryPreview(
            summaryText,
            previewInfoRows,
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    private void ApplyPreviewDiagnostic(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ApplyPreviewDiagnostic(
            cacheStatusText: _previewDiagnosticDisplayTextResolver.GetCacheStatusText(diagnostic.CacheStatus),
            fallbackReasonText: _previewDiagnosticDisplayTextResolver.GetFallbackReasonText(diagnostic.FallbackReason),
            diagnosticDetailText: LocalizedMessageDisplayFormatter.Format(
                diagnostic.DetailMessage,
                _localizationService));
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
                imageFocus.PixelHeight),
            imageFocus.PixelWidth,
            imageFocus.PixelHeight);
    }

    private void LogPreviewDisplayFailure(string assetName, string reason)
    {
        _appLogService.AppendWarning(_localizationService.GetFormattedString(
            ResourceKeys.BundleExplorerLogAssetPreviewFailedFormat,
            assetName,
            reason));
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
