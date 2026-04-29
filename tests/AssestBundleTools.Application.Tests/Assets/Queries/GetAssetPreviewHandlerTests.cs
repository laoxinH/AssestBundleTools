using System.IO;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Assets.Queries;

internal static class GetAssetPreviewHandlerTests
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

    private static GetAssetPreviewQuery CreateQuery()
    {
        return new GetAssetPreviewQuery(
            WorkspaceId: new WorkspaceId("workspace-session-1"),
            WorkspaceKey: "workspace-key-1",
            WorkspaceDataRootPath: @"D:\WorkspaceData",
            BundleId: new BundleId("bundle.001"),
            AssetId: new AssetId("asset.001"));
    }

    private static BundleIndexCacheEntryDto CreateBundleCacheEntry(
        string workspaceKey,
        string bundleId,
        string assetId)
    {
        return new BundleIndexCacheEntryDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: "bundle-cache-001",
            BundleId: bundleId,
            DisplayName: "characters",
            LogicalPath: "characters.ab",
            SourceFingerprint: new WorkspaceFileFingerprintDto(
                SourcePath: @"C:\Bundles\characters.ab",
                FileSizeInBytes: 1024,
                LastWriteTimeUtc: new DateTimeOffset(2026, 4, 28, 14, 0, 0, TimeSpan.Zero),
                ContentHash: "hash-characters"),
            CachedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 5, 0, TimeSpan.Zero),
            ParserVersion: "parser-1",
            ToolVersion: "tool-1",
            Dependencies: Array.Empty<string>(),
            Assets:
            [
                new BundleIndexAssetCacheEntryDto(
                    AssetId: assetId,
                    AssetName: "HeroPortrait",
                    AssetType: AssetType.Texture2D.ToString(),
                    AssetPath: "Assets/HeroPortrait.png",
                    ContainerPath: null,
                    SizeInBytes: 256,
                    PreferredDisplaySizeInBytes: 256,
                    HasPreview: true,
                    CanExtract: true,
                    CanReplace: true,
                    SerializedFileName: "characters.assets",
                    PathId: 12345,
                    AssetTypeDisplayName: "Texture2D")
            ],
            DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
            DecryptorVersion: "1.0.0");
    }

    private static AssetBundleDecryptorDescriptorDto CreatePassThroughDecryptorDescriptor()
    {
        return new AssetBundleDecryptorDescriptorDto(
            AssetBundleDecryptorIds.BuiltInPassThrough,
            "PassThrough",
            "1.0.0",
            AssetBundleDecryptorKind.BuiltIn,
            SupportsAutoDetection: false,
            IsFallback: true,
            Description: "fallback");
    }

    private static PreviewCachePayloadDto CreateTextPreviewPayload(
        BundleIndexCacheEntryDto bundleCacheEntry,
        string assetId,
        string textContent,
        string generatorVersion)
    {
        var previewCacheKey = CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, assetId);
        return new PreviewCachePayloadDto(
            new PreviewCacheDescriptorDto(
                WorkspaceKey: bundleCacheEntry.WorkspaceKey,
                PreviewCacheKey: previewCacheKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                AssetId: assetId,
                PreviewKind: AssetPreviewKind.Text.ToString(),
                ContentType: "text/plain; charset=utf-8",
                RelativePayloadPath: Path.Combine(previewCacheKey, "payload.txt"),
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                GeneratedAtUtc: new DateTimeOffset(2026, 4, 28, 14, 10, 0, TimeSpan.Zero),
                GeneratorVersion: generatorVersion),
            Encoding.UTF8.GetBytes(textContent));
    }

    private static string CreatePreviewCacheKey(string bundleCacheKey, string assetId)
    {
        var normalizedKey = $"{bundleCacheKey.ToLowerInvariant()}::{assetId.ToLowerInvariant()}";
        var bytes = Encoding.UTF8.GetBytes(normalizedKey);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) => throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => true;

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => true;

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) => true;

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            bundleTree = null!;
            throw new NotSupportedException();
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            out AssetListResultDto? assetListResult)
        {
            assetListResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            assetDetails = null;
            throw new NotSupportedException();
        }

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = null;
            throw new NotSupportedException();
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            extractionResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
        {
            candidates = null!;
            throw new NotSupportedException();
        }

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            throw new NotSupportedException();
        }

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            throw new NotSupportedException();
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            undoResult = null;
            throw new NotSupportedException();
        }

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext)
        {
            paneContext = null;
            throw new NotSupportedException();
        }
    }

    private sealed class TestWorkspaceDataStoreFactory : IWorkspaceDataStoreFactory
    {
        private readonly IBundleIndexCacheStore _bundleIndexCacheStore;
        private readonly IPreviewCacheStore _previewCacheStore;

        public TestWorkspaceDataStoreFactory(
            IBundleIndexCacheStore bundleIndexCacheStore,
            IPreviewCacheStore previewCacheStore)
        {
            _bundleIndexCacheStore = bundleIndexCacheStore;
            _previewCacheStore = previewCacheStore;
        }

        public IBundleIndexCacheStore CreateBundleIndexCacheStore(string? rootDirectoryPath = null) => _bundleIndexCacheStore;

        public IPreviewCacheStore CreatePreviewCacheStore(string? rootDirectoryPath = null) => _previewCacheStore;

        public IWorkspaceChangeStore CreateWorkspaceChangeStore(string? rootDirectoryPath = null) => throw new NotSupportedException();

        public string GetDefaultRootDirectoryPath() => @"D:\WorkspaceData";
    }

    private sealed class TestBundleIndexCacheStore : IBundleIndexCacheStore
    {
        private readonly IReadOnlyList<BundleIndexCacheEntryDto> _entries;

        public TestBundleIndexCacheStore(IReadOnlyList<BundleIndexCacheEntryDto> entries)
        {
            _entries = entries;
        }

        public BundleIndexCacheEntryDto? Load(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public IReadOnlyList<BundleIndexCacheEntryDto> LoadAll(string workspaceKey) => _entries;

        public void Save(BundleIndexCacheEntryDto cacheEntry) => throw new NotSupportedException();

        public void Delete(string workspaceKey, string bundleCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();
    }

    private sealed class TestPreviewCacheStore : IPreviewCacheStore
    {
        private PreviewCachePayloadDto? _payload;
        private string? _resolvedPayloadFilePath;

        public TestPreviewCacheStore(
            PreviewCachePayloadDto? payload = null,
            string? resolvedPayloadFilePath = null)
        {
            _payload = payload;
            _resolvedPayloadFilePath = resolvedPayloadFilePath;
        }

        public int SaveCallCount { get; private set; }

        public PreviewCachePayloadDto? LastSavedPayload { get; private set; }

        public PreviewCachePayloadDto? Load(string workspaceKey, string previewCacheKey)
        {
            return _payload is not null &&
                   string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(_payload.Descriptor.PreviewCacheKey, previewCacheKey, StringComparison.OrdinalIgnoreCase)
                ? _payload
                : null;
        }

        public IReadOnlyList<PreviewCacheDescriptorDto> LoadDescriptors(string workspaceKey)
        {
            return _payload is not null &&
                   string.Equals(_payload.Descriptor.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase)
                ? [_payload.Descriptor]
                : Array.Empty<PreviewCacheDescriptorDto>();
        }

        public string? ResolvePayloadFilePath(PreviewCacheDescriptorDto descriptor)
        {
            return _payload is not null &&
                   string.Equals(_payload.Descriptor.PreviewCacheKey, descriptor.PreviewCacheKey, StringComparison.OrdinalIgnoreCase)
                ? _resolvedPayloadFilePath
                : null;
        }

        public void Save(PreviewCachePayloadDto previewCachePayload)
        {
            SaveCallCount++;
            LastSavedPayload = previewCachePayload;
            _payload = previewCachePayload;
            _resolvedPayloadFilePath ??= Path.Combine(@"D:\WorkspaceData\preview-cache", previewCachePayload.Descriptor.RelativePayloadPath);
        }

        public void Delete(string workspaceKey, string previewCacheKey) => throw new NotSupportedException();

        public void DeleteWorkspace(string workspaceKey) => throw new NotSupportedException();

        public string? GetSavedTextContent()
        {
            return LastSavedPayload is null
                ? null
                : Encoding.UTF8.GetString(LastSavedPayload.Content);
        }
    }

    private sealed class TestAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
    {
        private readonly IAssetBundleDecryptor _decryptor;

        public TestAssetBundleDecryptorRegistry(IAssetBundleDecryptor decryptor)
        {
            _decryptor = decryptor;
        }

        public IReadOnlyList<IAssetBundleDecryptor> GetAll() => [_decryptor];

        public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
        {
            decryptor = _decryptor;
            return string.Equals(_decryptor.Descriptor.Id, decryptorId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class TestAssetBundleDecryptor : IAssetBundleDecryptor
    {
        public TestAssetBundleDecryptor(AssetBundleDecryptorDescriptorDto descriptor)
        {
            Descriptor = descriptor;
        }

        public AssetBundleDecryptorDescriptorDto Descriptor { get; }

        public Func<AssetBundleDecryptionRequestDto, Task<Result<AssetBundleDecryptionResultDto>>>? DecryptResultFactory { get; init; }

        public int DecryptCallCount { get; private set; }

        public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
            AssetBundleDecryptorProbeRequestDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
            AssetBundleDecryptionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            DecryptCallCount++;

            if (DecryptResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return DecryptResultFactory.Invoke(request);
        }
    }

    private sealed class TestAssetPreviewGenerator : IAssetPreviewGenerator
    {
        public string GeneratorVersion => "generator-1";

        public Func<AssetPreviewBuildRequestDto, Task<Result<AssetPreviewContentDto>>>? GenerateResultFactory { get; init; }

        public int GenerateCallCount { get; private set; }

        public Task<Result<AssetPreviewContentDto>> GenerateAsync(
            AssetPreviewBuildRequestDto request,
            CancellationToken cancellationToken = default)
        {
            GenerateCallCount++;

            if (GenerateResultFactory is null)
            {
                throw new NotSupportedException();
            }

            return GenerateResultFactory.Invoke(request);
        }
    }

    private sealed class TestAudioPayloadTranscodingService : IAudioPayloadTranscodingService
    {
        private readonly IReadOnlyList<AudioPayloadTranscoderDescriptorDto> _descriptors;

        public TestAudioPayloadTranscodingService(
            IReadOnlyList<AudioPayloadTranscoderDescriptorDto>? descriptors = null)
        {
            _descriptors = descriptors ?? Array.Empty<AudioPayloadTranscoderDescriptorDto>();
        }

        public IReadOnlyList<AudioPayloadTranscoderDescriptorDto> GetAvailableDescriptors() => _descriptors;

        public AudioPayloadTranscoderDescriptorDto? ResolveDescriptor(string transcoderId)
        {
            return _descriptors.FirstOrDefault(descriptor =>
                string.Equals(descriptor.Id, transcoderId, StringComparison.OrdinalIgnoreCase));
        }

        public Task<AudioPayloadTranscodeResultDto> TryTranscodeAsync(
            AudioPayloadTranscodeRequestDto request,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
