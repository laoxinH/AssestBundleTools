using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

/// <summary>
/// 呈现文本、摘要和诊断等基础预览状态。
/// </summary>
internal sealed class AssetPreviewBasicPresenter
{
    private readonly ILocalizationService _localizationService;
    private readonly AssetPreviewDisplayFormatter _assetPreviewDisplayFormatter;
    private readonly PreviewDiagnosticDisplayTextResolver _previewDiagnosticDisplayTextResolver;

    public AssetPreviewBasicPresenter(
        ILocalizationService localizationService,
        AssetPreviewDisplayFormatter assetPreviewDisplayFormatter)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _assetPreviewDisplayFormatter = assetPreviewDisplayFormatter ?? throw new ArgumentNullException(nameof(assetPreviewDisplayFormatter));
        _previewDiagnosticDisplayTextResolver = new PreviewDiagnosticDisplayTextResolver(_localizationService);
    }

    public void ApplyUnavailablePreview(
        InspectorPanelViewModel inspector,
        AssetPreviewDiagnosticDto diagnostic)
    {
        inspector.ShowPreviewMessage(_localizationService.GetRequiredString(ResourceKeys.InspectorPreviewUnavailableText));
        ApplyPreviewDiagnostic(inspector, diagnostic);
    }

    public void ApplyTextPreview(
        InspectorPanelViewModel inspector,
        AssetPreviewDto preview,
        IReadOnlyList<InspectorInfoRowViewModel> previewInfoRows)
    {
        if (string.IsNullOrWhiteSpace(preview.TextContent))
        {
            ApplyUnavailablePreview(inspector, preview.Diagnostic);
            return;
        }

        var safeDisplayText = AssetPreviewTextDisplayPolicy.CreateDisplay(preview.TextContent);
        inspector.ApplyTextPreview(
            safeDisplayText.TextContent,
            previewInfoRows,
            preview.PreviewPayloadFilePath);
        ApplyPreviewDiagnostic(inspector, preview.Diagnostic);
    }

    public void ApplySummaryPreview(
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

    public void ApplyPreviewDiagnostic(
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
}
