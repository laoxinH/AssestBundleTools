using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Assets.Services;

internal static class AssetPreviewCacheEvaluator
{
    public static AssetPreviewCacheEvaluationResult Evaluate(
        BundleIndexCacheEntryDto bundleCacheEntry,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        string previewCacheKey,
        PreviewCachePayloadDto? previewCachePayload,
        string generatorVersion,
        IAudioPayloadTranscodingService audioPayloadTranscodingService)
    {
        ArgumentNullException.ThrowIfNull(bundleCacheEntry);
        ArgumentNullException.ThrowIfNull(assetCacheEntry);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewCacheKey);
        ArgumentNullException.ThrowIfNull(audioPayloadTranscodingService);

        if (previewCachePayload?.Descriptor is null)
        {
            return Miss(AssetPreviewCacheStatusKind.Missing);
        }

        var descriptor = previewCachePayload.Descriptor;
        var sourceMatches = WorkspaceFileFingerprintComparer.Matches(
            descriptor.SourceFingerprint,
            bundleCacheEntry.SourceFingerprint);
        if (!string.Equals(descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(descriptor.BundleCacheKey, bundleCacheEntry.BundleCacheKey, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(descriptor.AssetId, assetCacheEntry.AssetId, StringComparison.Ordinal) ||
            !sourceMatches)
        {
            return Miss(sourceMatches
                ? AssetPreviewCacheStatusKind.IdentityChanged
                : AssetPreviewCacheStatusKind.SourceFingerprintChanged);
        }

        if (!string.Equals(descriptor.GeneratorVersion, generatorVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Miss(AssetPreviewCacheStatusKind.GeneratorVersionChanged);
        }

        var audioTranscoderEvaluation = EvaluateAudioTranscoderCache(
            descriptor,
            assetCacheEntry,
            audioPayloadTranscodingService);
        return audioTranscoderEvaluation.IsHit
            ? new AssetPreviewCacheEvaluationResult(true, previewCachePayload, AssetPreviewCacheStatusKind.Hit, null)
            : audioTranscoderEvaluation;
    }

    private static AssetPreviewCacheEvaluationResult EvaluateAudioTranscoderCache(
        PreviewCacheDescriptorDto descriptor,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        IAudioPayloadTranscodingService audioPayloadTranscodingService)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.AudioTranscoderId))
        {
            var currentDescriptor = audioPayloadTranscodingService.ResolveDescriptor(descriptor.AudioTranscoderId);
            return currentDescriptor is not null &&
                   string.Equals(currentDescriptor.Version, descriptor.AudioTranscoderVersion, StringComparison.OrdinalIgnoreCase)
                ? Hit()
                : Miss(AssetPreviewCacheStatusKind.AudioTranscoderChanged);
        }

        var isAudioSummaryPreview =
            string.Equals(assetCacheEntry.AssetType, AssetType.AudioClip.ToString(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(descriptor.PreviewKind, AssetPreviewKind.Summary.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isAudioSummaryPreview && audioPayloadTranscodingService.GetAvailableDescriptors().Count > 0)
        {
            return Miss(AssetPreviewCacheStatusKind.AudioTranscoderChanged);
        }

        return Hit();
    }

    private static AssetPreviewCacheEvaluationResult Hit() =>
        new(true, null, AssetPreviewCacheStatusKind.Hit, null);

    private static AssetPreviewCacheEvaluationResult Miss(AssetPreviewCacheStatusKind cacheStatus) =>
        new(false, null, cacheStatus, null);
}

internal sealed record AssetPreviewCacheEvaluationResult(
    bool IsHit,
    PreviewCachePayloadDto? Payload,
    AssetPreviewCacheStatusKind CacheStatus,
    LocalizedMessageDto? DetailMessage);
