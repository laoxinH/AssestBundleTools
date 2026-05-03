using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

internal sealed class PreviewDiagnosticDisplayTextResolver
{
    private readonly ILocalizationService _localizationService;

    public PreviewDiagnosticDisplayTextResolver(ILocalizationService localizationService)
    {
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public string GetCacheStatusText(AssetPreviewCacheStatusKind cacheStatus)
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

    public string GetFallbackReasonText(AssetPreviewFallbackReasonKind fallbackReason)
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
}
