using System.IO;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static partial class GetAssetPreviewHandlerTests
{
    public static async Task HandleAsync_ShouldReturnCachedPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.txt";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var previewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "cached preview",
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload, resolvedPayloadFilePath);
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Matching preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "Cached preview kind should round-trip.");
        TestAssert.Equal("text/plain; charset=utf-8", preview.ContentType, "Cached preview should preserve content type.");
        TestAssert.Equal("cached preview", preview.TextContent, "Cached preview text payload should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "Text preview should not expose binary payload.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached preview should expose resolved payload path.");
        TestAssert.True(preview.IsFromCache, "Cache hit should mark preview as from cache.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.Hit, preview.Diagnostic.CacheStatus, "Cache hit should be diagnosed as hit.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Cache hit should not fall back to summary.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }

    public static async Task HandleAsync_ShouldRoundTripCachedImageOverlay_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.png";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var imageOverlay = new AssetPreviewImageOverlayDto(
            CanvasPixelWidth: 256,
            CanvasPixelHeight: 128,
            HighlightRegion: new AssetPreviewImageRegionDto(
                X: 24,
                Y: 36,
                Width: 80,
                Height: 48),
            PivotPoint: new AssetPreviewImagePointDto(64, 72),
            NineSliceBorder: new AssetPreviewImageBorderDto(4, 5, 6, 7));
        var imageFocus = new AssetPreviewImageFocusDto(
            ContentType: "image/png",
            BinaryContent: null,
            RelativePayloadPath: "focus.png",
            PixelWidth: 80,
            PixelHeight: 48);
        var imageFocusContent = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D };
        var previewCachePayload = CreateImagePreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            imageOverlay: imageOverlay,
            imageFocus: imageFocus,
            imageFocusContent: imageFocusContent);
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload, resolvedPayloadFilePath);
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Matching image preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Image, preview.Kind, "Cached image preview kind should round-trip.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached image preview should expose resolved payload path.");
        var restoredOverlay = preview.ImageOverlay ?? throw new InvalidOperationException("Expected image overlay.");
        TestAssert.Equal(imageOverlay.CanvasPixelWidth, restoredOverlay.CanvasPixelWidth, "Image overlay canvas width should round-trip.");
        TestAssert.Equal(imageOverlay.CanvasPixelHeight, restoredOverlay.CanvasPixelHeight, "Image overlay canvas height should round-trip.");
        TestAssert.Equal(imageOverlay.HighlightRegion.X, restoredOverlay.HighlightRegion.X, "Image overlay x should round-trip.");
        TestAssert.Equal(imageOverlay.HighlightRegion.Y, restoredOverlay.HighlightRegion.Y, "Image overlay y should round-trip.");
        TestAssert.Equal(imageOverlay.HighlightRegion.Width, restoredOverlay.HighlightRegion.Width, "Image overlay width should round-trip.");
        TestAssert.Equal(imageOverlay.HighlightRegion.Height, restoredOverlay.HighlightRegion.Height, "Image overlay height should round-trip.");
        var restoredPivot = restoredOverlay.PivotPoint ?? throw new InvalidOperationException("Expected image overlay pivot.");
        TestAssert.Equal(imageOverlay.PivotPoint!.X, restoredPivot.X, "Image overlay pivot x should round-trip.");
        TestAssert.Equal(imageOverlay.PivotPoint.Y, restoredPivot.Y, "Image overlay pivot y should round-trip.");
        var restoredBorder = restoredOverlay.NineSliceBorder ?? throw new InvalidOperationException("Expected image overlay border.");
        TestAssert.Equal(imageOverlay.NineSliceBorder!.Left, restoredBorder.Left, "Image overlay border left should round-trip.");
        TestAssert.Equal(imageOverlay.NineSliceBorder.Top, restoredBorder.Top, "Image overlay border top should round-trip.");
        TestAssert.Equal(imageOverlay.NineSliceBorder.Right, restoredBorder.Right, "Image overlay border right should round-trip.");
        TestAssert.Equal(imageOverlay.NineSliceBorder.Bottom, restoredBorder.Bottom, "Image overlay border bottom should round-trip.");
        var restoredFocus = preview.ImageFocus ?? throw new InvalidOperationException("Expected image focus.");
        TestAssert.Equal(imageFocus.ContentType, restoredFocus.ContentType, "Image focus content type should round-trip.");
        TestAssert.Equal(imageFocus.RelativePayloadPath, restoredFocus.RelativePayloadPath, "Image focus payload path should round-trip.");
        TestAssert.Equal(imageFocus.PixelWidth, restoredFocus.PixelWidth, "Image focus width should round-trip.");
        TestAssert.Equal(imageFocus.PixelHeight, restoredFocus.PixelHeight, "Image focus height should round-trip.");
        TestAssert.True(
            restoredFocus.BinaryContent is not null &&
            restoredFocus.BinaryContent.AsSpan().SequenceEqual(imageFocusContent),
            "Image focus binary content should be hydrated from cache payload.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }

    public static async Task HandleAsync_ShouldRegeneratePreview_WhenGeneratorVersionChanges()
    {
        const string resolvedBundlePath = @"C:\Temp\preview.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var stalePreviewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "stale preview",
            generatorVersion: "generator-legacy");
        var previewCacheStore = new TestPreviewCacheStore(stalePreviewCachePayload);
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes("regenerated preview"))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Generator version change should rebuild preview successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "Regenerated preview should preserve preview kind.");
        TestAssert.Equal("regenerated preview", preview.TextContent, "Regenerated preview should expose new text payload.");
        TestAssert.Equal(null, preview.BinaryContent, "Text preview should not expose binary payload.");
        TestAssert.False(preview.IsFromCache, "Regenerated preview should not be marked as cache hit.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.GeneratorVersionChanged, preview.Diagnostic.CacheStatus, "Generator version invalidation should be diagnosed explicitly.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful regeneration should not fall back to summary.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Generator version invalidation should decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Generator version invalidation should regenerate preview once.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Regeneration should rewrite preview cache once.");
        TestAssert.Equal("generator-1", previewCacheStore.LastSavedPayload?.Descriptor.GeneratorVersion, "Saved preview cache should use current generator version.");
        TestAssert.Equal("regenerated preview", previewCacheStore.GetSavedTextContent(), "Saved preview cache should persist regenerated content.");
    }

    public static async Task HandleAsync_ShouldRegeneratePreview_WhenSourceFingerprintChanges()
    {
        const string resolvedBundlePath = @"C:\Temp\preview.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var basePreviewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "stale preview",
            generatorVersion: "generator-1");
        var stalePreviewCachePayload = basePreviewCachePayload with
        {
            Descriptor = basePreviewCachePayload.Descriptor with
            {
                SourceFingerprint = bundleCacheEntry.SourceFingerprint with
                {
                    FileSizeInBytes = bundleCacheEntry.SourceFingerprint.FileSizeInBytes + 1,
                    ContentHash = "hash-legacy"
                }
            }
        };
        var previewCacheStore = new TestPreviewCacheStore(stalePreviewCachePayload);
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes("fresh preview"))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Source fingerprint change should rebuild preview successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal("fresh preview", preview.TextContent, "Regenerated preview should expose new text payload.");
        TestAssert.False(preview.IsFromCache, "Regenerated preview should not be marked as cache hit.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.SourceFingerprintChanged, preview.Diagnostic.CacheStatus, "Source fingerprint invalidation should be diagnosed explicitly.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful regeneration should not fall back to summary.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Source fingerprint invalidation should decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Source fingerprint invalidation should regenerate preview once.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Source fingerprint invalidation should rewrite preview cache once.");
    }

    public static async Task HandleAsync_ShouldRegeneratePreview_WhenPreviewIdentityChanges()
    {
        const string resolvedBundlePath = @"C:\Temp\preview.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var basePreviewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "stale preview",
            generatorVersion: "generator-1");
        var stalePreviewCachePayload = basePreviewCachePayload with
        {
            Descriptor = basePreviewCachePayload.Descriptor with
            {
                AssetId = "asset-legacy"
            }
        };
        var previewCacheStore = new TestPreviewCacheStore(stalePreviewCachePayload);
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes("identity refreshed"))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview identity change should rebuild preview successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal("identity refreshed", preview.TextContent, "Regenerated preview should expose new text payload.");
        TestAssert.False(preview.IsFromCache, "Regenerated preview should not be marked as cache hit.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.IdentityChanged, preview.Diagnostic.CacheStatus, "Identity invalidation should be diagnosed explicitly.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful regeneration should not fall back to summary.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Identity invalidation should decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Identity invalidation should regenerate preview once.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Identity invalidation should rewrite preview cache once.");
    }

    public static async Task HandleAsync_ShouldRegeneratePreview_WhenAudioTranscoderChanges()
    {
        const string resolvedBundlePath = @"C:\Temp\audio.bundle";
        const string transcoderId = "audio.transcoder";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var audioAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.AudioClip.ToString(),
            AssetTypeDisplayName = AssetType.AudioClip.ToString(),
            SerializedFileName = "audio.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [audioAssetEntry]
        };
        var basePreviewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "stale audio preview",
            generatorVersion: "generator-1");
        var stalePreviewCachePayload = basePreviewCachePayload with
        {
            Descriptor = basePreviewCachePayload.Descriptor with
            {
                PreviewKind = AssetPreviewKind.Audio.ToString(),
                ContentType = "audio/wav",
                RelativePayloadPath = Path.Combine(CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, query.AssetId.Value), "payload.wav"),
                AudioTranscoderId = transcoderId,
                AudioTranscoderVersion = "1.0.0"
            },
            Content = [1, 2, 3]
        };
        var previewCacheStore = new TestPreviewCacheStore(stalePreviewCachePayload);
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Audio,
                    ContentType: "audio/wav",
                    Content: [9, 8, 7, 6],
                    AudioTranscoderId: transcoderId,
                    AudioTranscoderVersion: "2.0.0")))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(
            [
                new AudioPayloadTranscoderDescriptorDto(
                    Id: transcoderId,
                    DisplayName: "AudioTranscoder",
                    Version: "2.0.0")
            ]));

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Audio transcoder change should rebuild preview successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Audio, preview.Kind, "Regenerated audio preview should preserve preview kind.");
        TestAssert.Equal("audio/wav", preview.ContentType, "Regenerated audio preview should preserve content type.");
        TestAssert.Equal(4, preview.BinaryContent?.Length ?? 0, "Regenerated audio preview should expose binary payload.");
        TestAssert.False(preview.IsFromCache, "Regenerated preview should not be marked as cache hit.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.AudioTranscoderChanged, preview.Diagnostic.CacheStatus, "Audio transcoder invalidation should be diagnosed explicitly.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful regeneration should not fall back to summary.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Audio transcoder invalidation should decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Audio transcoder invalidation should regenerate preview once.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Audio transcoder invalidation should rewrite preview cache once.");
        TestAssert.Equal("2.0.0", previewCacheStore.LastSavedPayload?.Descriptor.AudioTranscoderVersion, "Saved preview cache should use current transcoder version.");
        TestAssert.Equal(transcoderId, previewCacheStore.LastSavedPayload?.Descriptor.AudioTranscoderId, "Saved preview cache should keep transcoder id.");
    }

    public static async Task HandleAsync_ShouldRegeneratePreview_WhenAudioSummaryCacheCanBeUpgradedWithNewTranscoder()
    {
        const string resolvedBundlePath = @"C:\Temp\audio.bundle";
        const string transcoderId = "audio.transcoder";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var audioAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.AudioClip.ToString(),
            AssetTypeDisplayName = AssetType.AudioClip.ToString(),
            SerializedFileName = "audio.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [audioAssetEntry]
        };

        var basePreviewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "metadata only",
            generatorVersion: "generator-1");
        var stalePreviewCachePayload = basePreviewCachePayload with
        {
            Descriptor = basePreviewCachePayload.Descriptor with
            {
                PreviewKind = AssetPreviewKind.Summary.ToString(),
                ContentType = "text/plain; charset=utf-8",
                AudioTranscoderId = null,
                AudioTranscoderVersion = null
            }
        };
        var previewCacheStore = new TestPreviewCacheStore(stalePreviewCachePayload);
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Audio,
                    ContentType: "audio/wav",
                    Content: [9, 8, 7, 6],
                    AudioTranscoderId: transcoderId,
                    AudioTranscoderVersion: "2.0.0")))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(
            [
                new AudioPayloadTranscoderDescriptorDto(
                    Id: transcoderId,
                    DisplayName: "AudioTranscoder",
                    Version: "2.0.0")
            ]));

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Audio summary cache should be rebuilt once a transcoder becomes available.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Audio, preview.Kind, "Upgraded preview should switch from summary to audio.");
        TestAssert.Equal("audio/wav", preview.ContentType, "Upgraded preview should expose audio content type.");
        TestAssert.Equal(4, preview.BinaryContent?.Length ?? 0, "Upgraded audio preview should expose binary payload.");
        TestAssert.False(preview.IsFromCache, "Upgraded preview should not be marked as cache hit.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.AudioTranscoderChanged, preview.Diagnostic.CacheStatus, "Audio summary upgrade should be diagnosed as transcoder invalidation.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful upgrade should not fall back to summary.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Audio summary upgrade should decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Audio summary upgrade should regenerate preview once.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Audio summary upgrade should rewrite preview cache once.");
        TestAssert.Equal(transcoderId, previewCacheStore.LastSavedPayload?.Descriptor.AudioTranscoderId, "Saved preview cache should keep the resolved transcoder id.");
        TestAssert.Equal("2.0.0", previewCacheStore.LastSavedPayload?.Descriptor.AudioTranscoderVersion, "Saved preview cache should persist the new transcoder version.");
    }

    public static async Task HandleAsync_ShouldReturnFallbackSummary_WhenDecryptionFails()
    {
        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor())
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Failure(
                ApplicationErrors.BundleDecryptionExecutionFailed(AssetBundleDecryptorIds.BuiltInPassThrough)))
        };
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview decryption failure should fall back to a summary preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Summary, preview.Kind, "Fallback preview should use summary kind.");
        TestAssert.Equal(AssetPreviewMessageKey.DecryptionFailed, preview.PreviewMessage?.Key, "Fallback summary should report decryption failed.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.DecryptionFailed, preview.Diagnostic.FallbackReason, "Diagnostic should report decryption failure.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.Missing, preview.Diagnostic.CacheStatus, "Missing preview cache should be reported.");
        TestAssert.Equal("Error.Bundle.Decrypt.ExecutionFailed", preview.Diagnostic.DetailMessage?.MessageToken, "Diagnostic detail should preserve the structured decrypt error token.");
        TestAssert.Equal(
            AssetBundleDecryptorIds.BuiltInPassThrough,
            TestAssert.Single(preview.Diagnostic.DetailMessage?.Arguments ?? Array.Empty<string>()),
            "Decrypt fallback detail should carry the decryptor id.");
        TestAssert.Equal("text/plain; charset=utf-8", preview.ContentType, "Fallback summary should use plain text content type.");
        TestAssert.Equal(null, preview.BinaryContent, "Fallback summary should not include binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Fallback summary should not include decoded text payload.");
        TestAssert.False(preview.IsFromCache, "Fallback summary should not be marked as cache hit.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Decryption failure path should invoke decryptor once.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Decryption failure should stop before preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Fallback summary should not write preview cache.");
    }

    public static async Task HandleAsync_ShouldReturnFallbackSummary_WhenPreviewGenerationFails()
    {
        const string resolvedBundlePath = @"C:\Temp\preview.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: resolvedBundlePath,
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(resolvedBundlePath)))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview generation failure should fall back to a summary preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Summary, preview.Kind, "Fallback preview should use summary kind.");
        TestAssert.Equal(AssetPreviewMessageKey.GenerationFailed, preview.PreviewMessage?.Key, "Fallback summary should report generation failed.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.GenerationFailed, preview.Diagnostic.FallbackReason, "Diagnostic should report generation failure.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.Missing, preview.Diagnostic.CacheStatus, "Missing preview cache should be reported.");
        TestAssert.Equal("Error.Bundle.Preview.GenerationFailed", preview.Diagnostic.DetailMessage?.MessageToken, "Diagnostic detail should preserve the structured generation error token.");
        TestAssert.Equal(
            resolvedBundlePath,
            TestAssert.Single(preview.Diagnostic.DetailMessage?.Arguments ?? Array.Empty<string>()),
            "Generation fallback detail should carry the resolved bundle path.");
        TestAssert.Equal("text/plain; charset=utf-8", preview.ContentType, "Fallback summary should use plain text content type.");
        TestAssert.Equal(null, preview.BinaryContent, "Fallback summary should not include binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Fallback summary should not include decoded text payload.");
        TestAssert.False(preview.IsFromCache, "Fallback summary should not be marked as cache hit.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Generation failure path should still decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Generation failure path should invoke preview generator once.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Fallback summary should not write preview cache.");
    }
}
