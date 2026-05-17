using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Internal;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Assets.Queries;

/// <summary>
/// 处理单个资源预览查询请求。
/// </summary>
/// <remarks>
/// 该用例负责把“索引缓存定位 + 预览缓存命中/写回 + 真实生成器调用”组织成稳定流程，
/// 让 Presentation 层只消费统一的预览读模型。
/// </remarks>
public sealed class GetAssetPreviewHandler : IQueryHandler<GetAssetPreviewQuery, Result<AssetPreviewDto>>
{
    private const string TextContentType = "text/plain; charset=utf-8";

    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly IAssetBundleDecryptorRegistry _assetBundleDecryptorRegistry;
    private readonly IAssetPreviewGenerator _assetPreviewGenerator;
    private readonly IAudioPayloadTranscodingService _audioPayloadTranscodingService;
    private readonly IBundleArtifactSessionInvalidator _bundleArtifactSessionInvalidator;
    private readonly IBundleArtifactCachePrunePolicy _bundleArtifactCachePrunePolicy;

    public GetAssetPreviewHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        IAssetBundleDecryptorRegistry assetBundleDecryptorRegistry,
        IAssetPreviewGenerator assetPreviewGenerator,
        IAudioPayloadTranscodingService audioPayloadTranscodingService,
        IBundleArtifactSessionInvalidator? bundleArtifactSessionInvalidator = null,
        IBundleArtifactCachePrunePolicy? bundleArtifactCachePrunePolicy = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry ?? throw new ArgumentNullException(nameof(assetBundleDecryptorRegistry));
        _assetPreviewGenerator = assetPreviewGenerator ?? throw new ArgumentNullException(nameof(assetPreviewGenerator));
        _audioPayloadTranscodingService = audioPayloadTranscodingService ?? throw new ArgumentNullException(nameof(audioPayloadTranscodingService));
        _bundleArtifactSessionInvalidator = bundleArtifactSessionInvalidator ?? NoOpBundleArtifactSessionInvalidator.Instance;
        _bundleArtifactCachePrunePolicy = bundleArtifactCachePrunePolicy ?? NoOpBundleArtifactCachePrunePolicy.Instance;
    }

    public async Task<Result<AssetPreviewDto>> HandleAsync(
        GetAssetPreviewQuery query,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetPreviewDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceId.Value))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.WorkspaceIdRequired());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceKey))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.WorkspaceCatalogKeyRequired());
        }

        if (string.IsNullOrWhiteSpace(query.WorkspaceDataRootPath))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.WorkspaceOpenDataRootPathRequired());
        }

        if (string.IsNullOrWhiteSpace(query.BundleId.Value))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.BundleIdRequired());
        }

        if (string.IsNullOrWhiteSpace(query.AssetId.Value))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetIdRequired());
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(query.WorkspaceId))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.WorkspaceNotFound(query.WorkspaceId));
        }

        if (!_workspaceSessionRegistry.ContainsBundle(query.WorkspaceId, query.BundleId))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.BundleNotFound(query.BundleId));
        }

        if (!_workspaceSessionRegistry.ContainsAsset(query.WorkspaceId, query.BundleId, query.AssetId))
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId));
        }

        var bundleArtifactStore = _workspaceDataStoreFactory.CreateBundleArtifactStore(query.WorkspaceDataRootPath);
        var bundleIndexCacheStore = _workspaceDataStoreFactory.CreateBundleIndexCacheStore(query.WorkspaceDataRootPath);
        var previewCacheStore = _workspaceDataStoreFactory.CreatePreviewCacheStore(query.WorkspaceDataRootPath);
        var bundleCacheEntry = bundleIndexCacheStore.LoadAll(query.WorkspaceKey)
            .FirstOrDefault(entry => string.Equals(entry.BundleId, query.BundleId.Value, StringComparison.Ordinal));
        if (bundleCacheEntry is null)
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.BundleNotFound(query.BundleId));
        }

        var assetCacheEntry = bundleCacheEntry.Assets
            .FirstOrDefault(entry => string.Equals(entry.AssetId, query.AssetId.Value, StringComparison.Ordinal));
        if (assetCacheEntry is null)
        {
            return Result<AssetPreviewDto>.Failure(ApplicationErrors.AssetNotFound(query.AssetId));
        }

        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetCacheEntry.AssetId);
        var cachedPreviewPayload = previewCacheStore.Load(query.WorkspaceKey, previewCacheKey);
        var cacheEvaluation = EvaluatePreviewCache(
            bundleCacheEntry,
            assetCacheEntry,
            previewCacheKey,
            cachedPreviewPayload);
        if (cacheEvaluation.IsHit && cacheEvaluation.Payload is not null)
        {
            return Result<AssetPreviewDto>.Success(AssetPreviewDtoMapper.MapPreviewDto(
                query,
                cacheEvaluation.Payload.Descriptor,
                cacheEvaluation.Payload.Content,
                cacheEvaluation.Payload.ImageFocusContent,
                previewPayloadFilePath: previewCacheStore.ResolvePayloadFilePath(cacheEvaluation.Payload.Descriptor),
                isFromCache: true,
                diagnostic: new AssetPreviewDiagnosticDto(
                    CacheStatus: AssetPreviewCacheStatusKind.Hit,
                    FallbackReason: AssetPreviewFallbackReasonKind.None)));
        }

        if (string.IsNullOrWhiteSpace(assetCacheEntry.SerializedFileName))
        {
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                AssetPreviewMessageKey.MissingLocator,
                new AssetPreviewDiagnosticDto(
                    CacheStatus: cacheEvaluation.CacheStatus,
                    FallbackReason: AssetPreviewFallbackReasonKind.MissingLocator,
                    DetailMessage: null)));
        }

        var decryptorResult = WorkspaceAssetBundleDecryptorResolver.Resolve(
            _assetBundleDecryptorRegistry,
            bundleCacheEntry.DecryptorId);
        if (!decryptorResult.IsSuccess || decryptorResult.Value is null)
        {
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                AssetPreviewMessageKey.DecryptorResolveFailed,
                new AssetPreviewDiagnosticDto(
                    CacheStatus: cacheEvaluation.CacheStatus,
                    FallbackReason: AssetPreviewFallbackReasonKind.DecryptorResolveFailed,
                    DetailMessage: AppErrorLocalizedMessageMapper.CreateFromFirstError(decryptorResult.Errors))));
        }

        var artifactResult = await WorkspaceBundleArtifactResolver.ResolveAsync(
            bundleArtifactStore,
            decryptorResult.Value,
            query.WorkspaceKey,
            query.WorkspaceDataRootPath,
            bundleCacheEntry.BundleCacheKey,
            bundleCacheEntry.SourceFingerprint.SourcePath,
            bundleCacheEntry.SourceFingerprint,
            DateTimeOffset.UtcNow,
            cancellationToken,
            _bundleArtifactSessionInvalidator,
            _bundleArtifactCachePrunePolicy);
        if (artifactResult.IsCancelled)
        {
            return Result<AssetPreviewDto>.Cancelled();
        }

        if (!artifactResult.IsSuccess || artifactResult.Value is null)
        {
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                AssetPreviewMessageKey.DecryptionFailed,
                new AssetPreviewDiagnosticDto(
                    CacheStatus: cacheEvaluation.CacheStatus,
                    FallbackReason: AssetPreviewFallbackReasonKind.DecryptionFailed,
                    DetailMessage: AppErrorLocalizedMessageMapper.CreateFromFirstError(artifactResult.Errors))));
        }

        var buildRequest = new AssetPreviewBuildRequestDto(
            ResolvedBundlePath: artifactResult.Value.ResolvedBundlePath,
            WorkspaceKey: query.WorkspaceKey,
            WorkspaceDataRootPath: query.WorkspaceDataRootPath,
            AssetId: assetCacheEntry.AssetId,
            AssetName: assetCacheEntry.AssetName,
            AssetPath: assetCacheEntry.AssetPath,
            ContainerPath: assetCacheEntry.ContainerPath,
            AssetType: ParseAssetType(assetCacheEntry.AssetType),
            AssetTypeDisplayName: ResolveAssetTypeDisplayName(assetCacheEntry),
            SizeInBytes: assetCacheEntry.SizeInBytes,
            SerializedFileName: assetCacheEntry.SerializedFileName,
            PathId: assetCacheEntry.PathId,
            BundleSessionCacheKey: artifactResult.Value.BundleSessionCacheKey);

        var previewContentResult = await _assetPreviewGenerator.GenerateAsync(buildRequest, cancellationToken);
        if (previewContentResult.IsCancelled)
        {
            return Result<AssetPreviewDto>.Cancelled();
        }

        if (!previewContentResult.IsSuccess || previewContentResult.Value is null)
        {
            var fallback = AssetPreviewGenerationFallbackResolver.Resolve(previewContentResult.Errors);
            return Result<AssetPreviewDto>.Success(BuildFallbackSummaryPreview(
                query,
                fallback.MessageKey,
                new AssetPreviewDiagnosticDto(
                    cacheEvaluation.CacheStatus,
                    fallback.FallbackReason,
                    AppErrorLocalizedMessageMapper.CreateFromFirstError(previewContentResult.Errors))));
        }

        var generatedAtUtc = DateTimeOffset.UtcNow;
        var previewContent = previewContentResult.Value;
        var descriptor = new PreviewCacheDescriptorDto(
            WorkspaceKey: query.WorkspaceKey,
            PreviewCacheKey: previewCacheKey,
            BundleCacheKey: bundleCacheEntry.BundleCacheKey,
            AssetId: assetCacheEntry.AssetId,
            PreviewKind: previewContent.Kind.ToString(),
            ContentType: previewContent.ContentType,
            RelativePayloadPath: ResolvePayloadFileName(previewCacheKey, previewContent),
            SourceFingerprint: bundleCacheEntry.SourceFingerprint,
            GeneratedAtUtc: generatedAtUtc,
            GeneratorVersion: _assetPreviewGenerator.GeneratorVersion,
            PreviewInfoItems: previewContent.PreviewInfoItems,
            PreviewMessage: previewContent.PreviewMessage,
            PreferredDisplaySizeInBytes: previewContent.PreferredDisplaySizeInBytes,
            AudioTranscoderId: previewContent.AudioTranscoderId,
            AudioTranscoderVersion: previewContent.AudioTranscoderVersion,
            ImageOverlay: previewContent.ImageOverlay,
            ImageFocus: CreateCachedImageFocus(previewContent, previewCacheKey));
        previewCacheStore.Save(new PreviewCachePayloadDto(
            descriptor,
            previewContent.Content,
            previewContent.ImageFocus?.BinaryContent));

        return Result<AssetPreviewDto>.Success(AssetPreviewDtoMapper.MapPreviewDto(
            query,
            descriptor,
            previewContent.Content,
            previewPayloadFilePath: previewCacheStore.ResolvePayloadFilePath(descriptor),
            imageFocusContent: previewContent.ImageFocus?.BinaryContent,
            isFromCache: false,
            diagnostic: new AssetPreviewDiagnosticDto(
                CacheStatus: cacheEvaluation.CacheStatus,
                FallbackReason: AssetPreviewFallbackReasonKind.None,
                DetailMessage: cacheEvaluation.DetailMessage)));
    }

    private PreviewCacheEvaluationResult EvaluatePreviewCache(
        BundleIndexCacheEntryDto bundleCacheEntry,
        BundleIndexAssetCacheEntryDto assetCacheEntry,
        string previewCacheKey,
        PreviewCachePayloadDto? previewCachePayload)
    {
        if (previewCachePayload?.Descriptor is null)
        {
            return new PreviewCacheEvaluationResult(
                IsHit: false,
                Payload: null,
                CacheStatus: AssetPreviewCacheStatusKind.Missing,
                DetailMessage: null);
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
            var cacheStatus = sourceMatches
                ? AssetPreviewCacheStatusKind.IdentityChanged
                : AssetPreviewCacheStatusKind.SourceFingerprintChanged;
            return new PreviewCacheEvaluationResult(
                IsHit: false,
                Payload: null,
                CacheStatus: cacheStatus,
                DetailMessage: null);
        }

        if (!string.Equals(descriptor.GeneratorVersion, _assetPreviewGenerator.GeneratorVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new PreviewCacheEvaluationResult(
                IsHit: false,
                Payload: null,
                CacheStatus: AssetPreviewCacheStatusKind.GeneratorVersionChanged,
                DetailMessage: null);
        }

        var audioTranscoderEvaluation = EvaluateAudioTranscoderCache(descriptor, assetCacheEntry);
        if (!audioTranscoderEvaluation.IsHit)
        {
            return audioTranscoderEvaluation;
        }

        // 预览缓存和资源包索引缓存一样，属于“缓存命中识别”的一部分。
        // 这里只在资源身份、来源指纹和生成器版本都仍然一致时才复用旧预览，
        // 避免资源包更新后继续误用过期缩略图或文本摘要。
        return new PreviewCacheEvaluationResult(
            IsHit: true,
            Payload: previewCachePayload,
            CacheStatus: AssetPreviewCacheStatusKind.Hit,
            DetailMessage: null);
    }

    private PreviewCacheEvaluationResult EvaluateAudioTranscoderCache(
        PreviewCacheDescriptorDto descriptor,
        BundleIndexAssetCacheEntryDto assetCacheEntry)
    {
        if (!string.IsNullOrWhiteSpace(descriptor.AudioTranscoderId))
        {
            var currentDescriptor = _audioPayloadTranscodingService.ResolveDescriptor(descriptor.AudioTranscoderId);
            if (currentDescriptor is null)
            {
                return new PreviewCacheEvaluationResult(
                    IsHit: false,
                    Payload: null,
                    CacheStatus: AssetPreviewCacheStatusKind.AudioTranscoderChanged,
                    DetailMessage: null);
            }

            if (!string.Equals(currentDescriptor.Version, descriptor.AudioTranscoderVersion, StringComparison.OrdinalIgnoreCase))
            {
                return new PreviewCacheEvaluationResult(
                    IsHit: false,
                    Payload: null,
                    CacheStatus: AssetPreviewCacheStatusKind.AudioTranscoderChanged,
                    DetailMessage: null);
            }

            return new PreviewCacheEvaluationResult(
                IsHit: true,
                Payload: null,
                CacheStatus: AssetPreviewCacheStatusKind.Hit,
                DetailMessage: null);
        }

        // 音频摘要若是在“无转码器”环境下生成，后续安装转码器后应允许自动重建，
        // 这样旧摘要才能升级为真实可播放预览，而不会长期卡在第一次回退结果上。
        var isAudioSummaryPreview =
            string.Equals(assetCacheEntry.AssetType, AssetType.AudioClip.ToString(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(descriptor.PreviewKind, AssetPreviewKind.Summary.ToString(), StringComparison.OrdinalIgnoreCase);
        if (isAudioSummaryPreview && _audioPayloadTranscodingService.GetAvailableDescriptors().Count > 0)
        {
            return new PreviewCacheEvaluationResult(
                IsHit: false,
                Payload: null,
                CacheStatus: AssetPreviewCacheStatusKind.AudioTranscoderChanged,
                DetailMessage: null);
        }

        return new PreviewCacheEvaluationResult(
            IsHit: true,
            Payload: null,
            CacheStatus: AssetPreviewCacheStatusKind.Hit,
            DetailMessage: null);
    }

    private static AssetPreviewDto BuildFallbackSummaryPreview(
        GetAssetPreviewQuery query,
        AssetPreviewMessageKey messageKey,
        AssetPreviewDiagnosticDto diagnostic)
    {
        return new AssetPreviewDto(
            WorkspaceId: query.WorkspaceId,
            BundleId: query.BundleId,
            AssetId: query.AssetId,
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            BinaryContent: null,
            TextContent: null,
            PreviewPayloadFilePath: null,
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreviewMessage: new AssetPreviewMessageDto(messageKey),
            PreferredDisplaySizeInBytes: null,
            IsFromCache: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Diagnostic: diagnostic,
            ImageOverlay: null,
            ImageFocus: null);
    }

    private static string CreatePreviewCacheKey(string bundleCacheKey, string assetId)
    {
        var normalizedKey = $"{bundleCacheKey.ToLowerInvariant()}::{assetId.ToLowerInvariant()}";
        var bytes = Encoding.UTF8.GetBytes(normalizedKey);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string ResolvePayloadFileName(string previewCacheKey, AssetPreviewContentDto previewContent)
    {
        var fileName = previewContent.Kind switch
        {
            AssetPreviewKind.Image => "payload.png",
            AssetPreviewKind.Audio => $"payload{ResolveAudioPayloadExtension(previewContent.ContentType)}",
            AssetPreviewKind.Mesh => "payload.mesh.json",
            AssetPreviewKind.Material => "payload.material.json",
            AssetPreviewKind.AnimationClip => "payload.animation-clip.json",
            AssetPreviewKind.Font => "payload.font.json",
            AssetPreviewKind.GameObject => "payload.game-object.json",
            AssetPreviewKind.MonoBehaviour => "payload.mono-behaviour.json",
            AssetPreviewKind.AnimatorController => "payload.animator-controller.json",
            AssetPreviewKind.Binary => "payload.bin",
            _ => "payload.txt"
        };

        return Path.Combine(previewCacheKey, fileName);
    }

    private static AssetPreviewImageFocusDto? CreateCachedImageFocus(
        AssetPreviewContentDto previewContent,
        string previewCacheKey)
    {
        var imageFocus = previewContent.ImageFocus;
        if (imageFocus?.BinaryContent is not { Length: > 0 })
        {
            return null;
        }

        return imageFocus with
        {
            BinaryContent = null,
            RelativePayloadPath = Path.Combine(previewCacheKey, "focus.png")
        };
    }

    private static string ResolveAudioPayloadExtension(string contentType)
    {
        return contentType.Trim().ToLowerInvariant() switch
        {
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            "audio/ogg" => ".ogg",
            "audio/mpeg" => ".mp3",
            "audio/flac" => ".flac",
            "audio/mp4" => ".m4a",
            "audio/aac" => ".aac",
            _ => ".bin"
        };
    }

    private static AssetType ParseAssetType(string assetType)
    {
        return Enum.TryParse<AssetType>(assetType, ignoreCase: true, out var parsedAssetType)
            ? parsedAssetType
            : AssetType.Unknown;
    }

    private static string ResolveAssetTypeDisplayName(BundleIndexAssetCacheEntryDto assetCacheEntry)
    {
        if (!string.IsNullOrWhiteSpace(assetCacheEntry.AssetTypeDisplayName))
        {
            return assetCacheEntry.AssetTypeDisplayName.Trim();
        }

        return string.IsNullOrWhiteSpace(assetCacheEntry.AssetType)
            ? AssetType.Unknown.ToString()
            : assetCacheEntry.AssetType.Trim();
    }

    private sealed record PreviewCacheEvaluationResult(
        bool IsHit,
        PreviewCachePayloadDto? Payload,
        AssetPreviewCacheStatusKind CacheStatus,
        LocalizedMessageDto? DetailMessage);
}
