using System.IO;
using System.Text;
using System.Text.Json;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
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

    public static async Task HandleAsync_ShouldUseFileReplacementPreview_ForTexture2D()
    {
        const string replacementFilePath = @"C:\Imports\hero-replacement.png";
        var replacementBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x10 };

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Image,
                ContentType: "image/png",
                Content: replacementBytes,
                PreferredDisplaySizeInBytes: replacementBytes.Length)));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    fileReplacementSourceFileName: "hero-replacement.png",
                    fileReplacementSourcePath: replacementFilePath)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Texture2D file replacement should provide the effective preview payload.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(query.BundleId, preview.BundleId, "Effective preview should keep the target bundle identity.");
        TestAssert.Equal(query.AssetId, preview.AssetId, "Effective preview should keep the target asset identity.");
        TestAssert.Equal(AssetPreviewKind.Image, preview.Kind, "Texture2D replacement preview should be an image.");
        TestAssert.Equal("image/png", preview.ContentType, "Replacement preview should preserve provider content type.");
        TestAssert.True(
            preview.BinaryContent is not null &&
            preview.BinaryContent.AsSpan().SequenceEqual(replacementBytes),
            "Replacement preview should expose the file source bytes.");
        TestAssert.Equal(replacementFilePath, preview.PreviewPayloadFilePath, "Replacement preview should expose the selected source path.");
        TestAssert.False(preview.IsFromCache, "Replacement preview should not be reported as a cached original preview.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful replacement preview should not fall back.");
        TestAssert.Equal(1, replacementPreviewSourceProvider.LoadFilePreviewCallCount, "Replacement source provider should be used once.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Effective file preview should not decrypt the original bundle.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Effective file preview should not generate original preview content.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Effective file preview should not rewrite original preview cache.");
    }

    public static async Task HandleAsync_ShouldUsePreviewSourceOverride_ForTextAsset()
    {
        const string overrideFilePath = @"C:\Imports\hero.atlas";
        const string atlasText = "hero.png\nsize: 256,128\nhead\n  xy: 0, 0\n  size: 32, 32\n";

        var query = CreateQuery() with
        {
            PreviewSourceOverride = new AssetPreviewSourceOverrideDto(
                new AssestBundleTools.Domain.Bundles.BundleId("bundle.001"),
                new AssestBundleTools.Domain.Assets.AssetId("asset.001"),
                overrideFilePath)
        };
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0] with
                {
                    AssetType = AssetType.TextAsset.ToString(),
                    AssetTypeDisplayName = AssetType.TextAsset.ToString(),
                    AssetPath = "Assets/Hero/Hero.atlas",
                    AssetName = "Hero.atlas"
                }
            ]
        };
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Text,
                ContentType: "text/plain; charset=utf-8",
                Content: Encoding.UTF8.GetBytes(atlasText))));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview source overrides should provide draft TextAsset preview content.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(query.BundleId, preview.BundleId, "Override preview should keep the target bundle identity.");
        TestAssert.Equal(query.AssetId, preview.AssetId, "Override preview should keep the target asset identity.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "TextAsset overrides should expose text preview content.");
        TestAssert.Equal(atlasText, preview.FullTextContent, "Override text should be available to structured text readers.");
        TestAssert.Equal(
            AssetPreviewFullContentReferenceKind.FileReplacement,
            preview.FullContentReference?.Kind,
            "Override text should expose a file-replacement full content reference.");
        TestAssert.Equal(
            overrideFilePath,
            preview.FullContentReference?.SourcePath,
            "Override full content reference should carry the draft source path.");
        TestAssert.Equal(
            Encoding.UTF8.GetByteCount(atlasText),
            preview.FullContentReference?.ContentLength,
            "Override full content reference should expose the replacement text payload length.");
        TestAssert.Equal(overrideFilePath, preview.PreviewPayloadFilePath, "Override preview should expose the draft source path.");
        TestAssert.Equal(1, replacementPreviewSourceProvider.LoadFilePreviewCallCount, "Override preview should read exactly one external file source.");
        TestAssert.Equal(AssetType.TextAsset, replacementPreviewSourceProvider.LastTargetDetails?.AssetType, "Override target details should preserve the indexed asset type.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Override preview should not decrypt the original bundle.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Override preview should not generate original preview content.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Override preview should not write the original preview cache.");
    }

    public static async Task HandleAsync_ShouldUseFileReplacementPreview_ForTextAsset()
    {
        const string replacementFilePath = @"C:\Imports\hero.atlas";
        const string atlasText = "hero.png\nsize: 512,256\n";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0] with
                {
                    AssetType = AssetType.TextAsset.ToString(),
                    AssetTypeDisplayName = AssetType.TextAsset.ToString(),
                    AssetPath = "Assets/Hero/Hero.atlas",
                    AssetName = "Hero.atlas"
                }
            ]
        };
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Text,
                ContentType: "text/plain; charset=utf-8",
                Content: Encoding.UTF8.GetBytes(atlasText))));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    assetType: AssetType.TextAsset,
                    fileReplacementSourceFileName: "hero.atlas",
                    fileReplacementSourcePath: replacementFilePath)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "TextAsset file replacement should provide the effective preview payload.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "TextAsset replacement preview should be text.");
        TestAssert.Equal(atlasText, preview.FullTextContent, "TextAsset replacement preview should expose full text.");
        TestAssert.Equal(
            AssetPreviewFullContentReferenceKind.FileReplacement,
            preview.FullContentReference?.Kind,
            "TextAsset replacement preview should expose a file-replacement full content reference.");
        TestAssert.Equal(
            replacementFilePath,
            preview.FullContentReference?.SourcePath,
            "TextAsset replacement full content reference should carry the selected source path.");
        TestAssert.Equal(
            Encoding.UTF8.GetByteCount(atlasText),
            preview.FullContentReference?.ContentLength,
            "TextAsset replacement full content reference should expose the replacement text payload length.");
        TestAssert.Equal(replacementFilePath, preview.PreviewPayloadFilePath, "TextAsset replacement preview should expose the selected source path.");
        TestAssert.Equal(1, replacementPreviewSourceProvider.LoadFilePreviewCallCount, "TextAsset replacement preview should load one file source.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "TextAsset file preview should not decrypt the original bundle.");
        TestAssert.Equal(0, generator.GenerateCallCount, "TextAsset file preview should not generate original preview content.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "TextAsset file preview should not rewrite original preview cache.");
    }

    public static async Task HandleAsync_ShouldUseAppliedTextEditDraftPreview_ForTextAsset()
    {
        const string originalText = "hero.png\nsize: 256,128\n";
        const string editedText = "hero.png\nsize: 512,256\nhead\n  xy: 1, 2\n";
        const string draftId = "draft-text-1";
        const string draftMode = "text-content";
        const int draftRevision = 2;
        var appliedAtUtc = new DateTimeOffset(2026, 7, 5, 9, 0, 0, TimeSpan.Zero);

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0] with
                {
                    AssetType = AssetType.TextAsset.ToString(),
                    AssetTypeDisplayName = AssetType.TextAsset.ToString(),
                    AssetPath = "Assets/Hero/Hero.atlas",
                    AssetName = "Hero.atlas"
                }
            ]
        };
        var originalPreviewPayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            originalText,
            generatorVersion: "generator-1");
        var draftPreviewPayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            editedText,
            generatorVersion: "edit-draft-text-v1");
        var previewCacheStore = new TestPreviewCacheStore(originalPreviewPayload);
        var draftTextStore = new TestAssetEditDraftTextStore(draftPreviewPayload);
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    name: "Hero.atlas",
                    assetType: AssetType.TextAsset,
                    textEditDraftId: draftId,
                    textEditDraftMode: draftMode,
                    textEditAppliedAtUtc: appliedAtUtc,
                    textEditDraftRevision: draftRevision)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore,
                assetEditDraftTextStore: draftTextStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Applied TextAsset edit draft should provide the current effective preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Text, preview.Kind, "Applied text edit preview should be text.");
        TestAssert.Equal(editedText, preview.FullTextContent, "Applied text edit preview should expose the edited draft text.");
        TestAssert.Equal(
            AssetPreviewFullContentReferenceKind.EditDraft,
            preview.FullContentReference?.Kind,
            "Applied text edit preview should expose an edit-draft full content reference.");
        TestAssert.Equal(draftId, preview.FullContentReference?.DraftId, "Edit-draft reference should carry the applied draft id.");
        TestAssert.Equal(draftMode, preview.FullContentReference?.DraftMode, "Edit-draft reference should carry the applied draft mode.");
        TestAssert.Equal(draftRevision, preview.FullContentReference?.DraftRevision, "Edit-draft reference should carry the applied draft revision.");
        TestAssert.Equal(appliedAtUtc, preview.GeneratedAtUtc, "Applied text edit preview should use the applied timestamp.");
        TestAssert.Equal(1, draftTextStore.LoadTextChunkCallCount, "Applied text edit preview should read the draft text store.");
        TestAssert.Equal(draftRevision, draftTextStore.LastRevision, "Applied text edit preview should read the revision selected by operation replay.");
        TestAssert.Equal(0, previewCacheStore.LoadCallCount, "Applied text edit preview should not read stale original preview cache.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Applied text edit preview should not decrypt the original bundle.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Applied text edit preview should not generate original preview content.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Applied text edit preview should not rewrite original preview cache.");
    }

    public static async Task HandleAsync_ShouldUseFileReplacementPreview_ForAudioClip()
    {
        const string replacementFilePath = @"C:\Imports\hero-voice.wav";
        var replacementBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x57, 0x41, 0x56, 0x45 };

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0] with
                {
                    AssetType = AssetType.AudioClip.ToString(),
                    AssetTypeDisplayName = AssetType.AudioClip.ToString(),
                    AssetPath = "Assets/Audio/HeroVoice",
                    AssetName = "HeroVoice"
                }
            ]
        };
        var previewCachePayload = CreateTextPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            "old audio summary",
            generatorVersion: "generator-1");
        var previewCacheStore = new TestPreviewCacheStore(previewCachePayload);
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator();
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Audio,
                ContentType: "audio/wav",
                Content: replacementBytes,
                PreferredDisplaySizeInBytes: replacementBytes.Length)));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    name: "HeroVoice",
                    assetType: AssetType.AudioClip,
                    fileReplacementSourceFileName: "hero-voice.wav",
                    fileReplacementSourcePath: replacementFilePath)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "AudioClip file replacement should provide the effective preview payload.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(query.BundleId, preview.BundleId, "Effective audio preview should keep the target bundle identity.");
        TestAssert.Equal(query.AssetId, preview.AssetId, "Effective audio preview should keep the target asset identity.");
        TestAssert.Equal(AssetPreviewKind.Audio, preview.Kind, "AudioClip replacement preview should be audio.");
        TestAssert.Equal("audio/wav", preview.ContentType, "Audio replacement preview should preserve provider content type.");
        TestAssert.True(
            preview.BinaryContent is not null &&
            preview.BinaryContent.AsSpan().SequenceEqual(replacementBytes),
            "Audio replacement preview should expose the replacement file bytes.");
        TestAssert.Equal(replacementFilePath, preview.PreviewPayloadFilePath, "Audio replacement preview should expose the selected source path.");
        TestAssert.False(preview.IsFromCache, "Audio replacement preview should not be reported as a cached original preview.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful audio replacement preview should not fall back.");
        TestAssert.Equal(1, replacementPreviewSourceProvider.LoadFilePreviewCallCount, "Audio replacement preview should load one file source.");
        TestAssert.Equal(AssetType.AudioClip, replacementPreviewSourceProvider.LastTargetDetails?.AssetType, "Audio replacement target details should preserve the indexed asset type.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Audio replacement preview should not decrypt the original bundle.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Audio replacement preview should not generate original preview content.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Audio replacement preview should not rewrite original preview cache.");
    }

    public static async Task HandleAsync_ShouldUseWorkspaceReplacementPreview_WithTargetIdentity()
    {
        const string resolvedBundlePath = @"C:\Temp\characters.bundle";
        var sourceAssetId = new AssetId("asset.002");
        var sourceBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x22 };

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets =
            [
                bundleCacheEntry.Assets[0],
                bundleCacheEntry.Assets[0] with
                {
                    AssetId = sourceAssetId.Value,
                    AssetName = "HeroPortraitSource",
                    AssetPath = "Assets/HeroPortraitSource.png",
                    PathId = 54321
                }
            ]
        };
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
            GenerateResultFactory = request => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Image,
                    ContentType: "image/png",
                    Content: sourceBytes,
                    PreferredDisplaySizeInBytes: sourceBytes.Length)))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    workspaceReplacementSourceBundleId: query.BundleId,
                    workspaceReplacementSourceAssetId: sourceAssetId,
                    workspaceReplacementSourceBundleDisplayName: "characters",
                    workspaceReplacementSourceAssetName: "HeroPortraitSource")
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Workspace replacement should resolve the source asset preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(query.BundleId, preview.BundleId, "Workspace replacement preview should keep the target bundle identity.");
        TestAssert.Equal(query.AssetId, preview.AssetId, "Workspace replacement preview should keep the target asset identity.");
        TestAssert.Equal(AssetPreviewKind.Image, preview.Kind, "Workspace replacement preview should expose the source preview kind.");
        TestAssert.True(
            preview.BinaryContent is not null &&
            preview.BinaryContent.AsSpan().SequenceEqual(sourceBytes),
            "Workspace replacement preview should expose source preview bytes.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Workspace source preview should generate source content once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Workspace source preview should call the generator once.");
        TestAssert.Equal(sourceAssetId.Value, generator.LastRequest?.AssetId, "Preview generator should be asked for the replacement source asset.");
    }

    public static async Task HandleAsync_ShouldUseAssociatedTextureReplacementPreview_ForSprite()
    {
        const string replacementFilePath = @"C:\Imports\hero-texture.png";
        var spriteAssetId = new AssetId("asset.sprite");
        var textureAssetId = new AssetId("asset.texture");
        var replacementBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x33 };
        var originalSpriteBytes = Encoding.UTF8.GetBytes("original sprite");
        var query = CreateQuery() with
        {
            AssetId = spriteAssetId
        };
        var spriteOverlay = new AssetPreviewImageOverlayDto(
            CanvasPixelWidth: 256,
            CanvasPixelHeight: 128,
            HighlightRegion: new AssetPreviewImageRegionDto(16, 24, 64, 32),
            PivotPoint: new AssetPreviewImagePointDto(48, 40),
            NineSliceBorder: null);
        var spriteEntry = CreateAssetCacheEntry(query.AssetId.Value) with
        {
            AssetName = "HeroSprite",
            AssetType = AssetType.Sprite.ToString(),
            AssetTypeDisplayName = AssetType.Sprite.ToString(),
            AssetPath = "Assets/HeroSprite.sprite",
            PathId = 10
        };
        var textureEntry = CreateAssetCacheEntry(textureAssetId.Value) with
        {
            AssetName = "HeroTexture",
            AssetType = AssetType.Texture2D.ToString(),
            AssetTypeDisplayName = AssetType.Texture2D.ToString(),
            AssetPath = "Assets/HeroTexture.png",
            PathId = 20
        };
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value) with
        {
            Assets = [spriteEntry, textureEntry]
        };
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ => Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(
                new AssetBundleDecryptionResultDto(
                    Decryptor: decryptorDescriptor,
                    ResolvedBundlePath: @"C:\Temp\sprites.bundle",
                    ProducedDerivedFile: false,
                    BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                    DiagnosticMessage: null)))
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = request =>
            {
                if (string.Equals(request.AssetId, spriteAssetId.Value, StringComparison.Ordinal))
                {
                    return Task.FromResult(Result<AssetPreviewContentDto>.Success(
                        new AssetPreviewContentDto(
                            Kind: AssetPreviewKind.Image,
                            ContentType: "image/png",
                            Content: originalSpriteBytes,
                            ImageOverlay: spriteOverlay)));
                }

                throw new InvalidOperationException("Texture file replacement should be loaded from the replacement source provider.");
            }
        };
        var replacementPreviewSourceProvider = new TestReplacementPreviewSourceProvider(
            Result<AssetPreviewContentDto>.Success(new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Image,
                ContentType: "image/png",
                Content: replacementBytes,
                PreferredDisplaySizeInBytes: replacementBytes.Length)));
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(
            [
                CreateAssetDetails(
                    query,
                    assetType: AssetType.Sprite,
                    dependencies:
                    [
                        new AssetDependencyDto(
                            textureAssetId,
                            query.BundleId,
                            "HeroTexture",
                            AssetType.Texture2D,
                            PathId: 20,
                            AssetTypeDisplayName: AssetType.Texture2D.ToString())
                    ]),
                CreateAssetDetails(
                    query with { AssetId = textureAssetId },
                    fileReplacementSourceFileName: "hero-texture.png",
                    fileReplacementSourcePath: replacementFilePath)
            ]),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService(),
            replacementPreviewSourceProvider: replacementPreviewSourceProvider);

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Sprite preview should use the effective associated Texture2D payload.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(query.BundleId, preview.BundleId, "Sprite preview should keep Sprite bundle identity.");
        TestAssert.Equal(spriteAssetId, preview.AssetId, "Sprite preview should keep Sprite asset identity.");
        TestAssert.Equal(AssetPreviewKind.Image, preview.Kind, "Sprite associated texture projection should remain an image preview.");
        TestAssert.True(
            preview.BinaryContent is not null &&
            preview.BinaryContent.AsSpan().SequenceEqual(replacementBytes),
            "Sprite preview should expose the associated Texture2D replacement bytes.");
        TestAssert.Equal(spriteOverlay, preview.ImageOverlay, "Sprite overlay should be preserved over the replacement texture payload.");
        TestAssert.Equal(1, replacementPreviewSourceProvider.LoadFilePreviewCallCount, "Associated Texture2D replacement preview should load once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Only the original Sprite preview should be generated for overlay metadata.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Generating the Sprite overlay should decrypt the original bundle once.");
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

    public static async Task HandleAsync_ShouldReuseBundleArtifactManifest_WhenPreviewCacheMisses()
    {
        const string artifactBundlePath = @"D:\WorkspaceData\cache\bundles\workspace-key-1\bundle-cache-001\normalized.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var bundleArtifactStore = new TestBundleArtifactStore(
        [
            new BundleArtifactManifestDto(
                WorkspaceKey: query.WorkspaceKey,
                BundleCacheKey: bundleCacheEntry.BundleCacheKey,
                ResolvedBundlePath: artifactBundlePath,
                IsMaterialized: true,
                SourceFingerprint: bundleCacheEntry.SourceFingerprint,
                BundleFingerprint: new WorkspaceFileFingerprintDto(
                    SourcePath: artifactBundlePath,
                    FileSizeInBytes: 4096,
                    LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero),
                    ContentHash: null),
                DecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough,
                DecryptorVersion: "1.0.0",
                NormalizerVersion: "bundle-artifact/1",
                CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero))
        ]);
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptor = new TestAssetBundleDecryptor(CreatePassThroughDecryptorDescriptor());
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = request => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes($"artifact path: {request.ResolvedBundlePath}"))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore,
                bundleArtifactStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var result = await handler.HandleAsync(query);

        TestAssert.True(result.IsSuccess, "Preview cache miss should generate preview from reusable bundle artifact.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal($"artifact path: {artifactBundlePath}", preview.TextContent, "Preview generator should receive the cached artifact path.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Reusable bundle artifact should avoid invoking decryptor.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Preview content should still be generated on preview cache miss.");
        TestAssert.Equal(artifactBundlePath, generator.LastRequest?.ResolvedBundlePath, "Generator request should use artifact path.");
        TestAssert.True(
            generator.LastRequest?.BundleSessionCacheKey?.Contains(bundleCacheEntry.BundleCacheKey, StringComparison.OrdinalIgnoreCase) == true,
            "Generator request should carry the artifact identity for loaded-session reuse.");
        TestAssert.Equal(0, bundleArtifactStore.SaveCallCount, "Reusable bundle artifact should not rewrite manifest.");
    }

    public static async Task HandleAsync_ShouldSingleFlightBundleArtifactGeneration_WhenConcurrentPreviewCacheMisses()
    {
        const string resolvedBundlePath = @"C:\Temp\characters-normalized.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var bundleArtifactStore = new TestBundleArtifactStore();
        var previewCacheStore = new TestPreviewCacheStore();
        var decryptorDescriptor = CreatePassThroughDecryptorDescriptor();
        var decryptStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueDecrypt = new TaskCompletionSource<Result<AssetBundleDecryptionResultDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var decryptor = new TestAssetBundleDecryptor(decryptorDescriptor)
        {
            DecryptResultFactory = _ =>
            {
                decryptStarted.TrySetResult();
                return continueDecrypt.Task;
            }
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = request => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Text,
                    ContentType: "text/plain; charset=utf-8",
                    Content: Encoding.UTF8.GetBytes($"generated from {request.ResolvedBundlePath}"))))
        };
        var handler = new GetAssetPreviewHandler(
            new TestWorkspaceSessionRegistry(),
            new TestWorkspaceDataStoreFactory(
                new TestBundleIndexCacheStore([bundleCacheEntry]),
                previewCacheStore,
                bundleArtifactStore),
            new TestAssetBundleDecryptorRegistry(decryptor),
            generator,
            new TestAudioPayloadTranscodingService());

        var firstTask = handler.HandleAsync(query);
        await decryptStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var secondTask = handler.HandleAsync(query);
        await Task.Delay(100);
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Concurrent artifact requests for the same bundle should share the in-flight generation.");

        continueDecrypt.SetResult(Result<AssetBundleDecryptionResultDto>.Success(
            new AssetBundleDecryptionResultDto(
                Decryptor: decryptorDescriptor,
                ResolvedBundlePath: resolvedBundlePath,
                ProducedDerivedFile: false,
                BundleFingerprint: bundleCacheEntry.SourceFingerprint,
                DiagnosticMessage: null)));

        var firstResult = await firstTask;
        var secondResult = await secondTask;

        TestAssert.True(firstResult.IsSuccess, "First concurrent preview should complete successfully.");
        TestAssert.True(secondResult.IsSuccess, "Second concurrent preview should complete successfully.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Artifact generation should still invoke decryptor once after both requests finish.");
        TestAssert.Equal(1, bundleArtifactStore.SaveCallCount, "Shared artifact generation should save one manifest.");
        TestAssert.Equal(resolvedBundlePath, generator.LastRequest?.ResolvedBundlePath, "Preview generator should receive the shared artifact path.");
        TestAssert.True(
            generator.LastRequest?.BundleSessionCacheKey?.Contains("bundle-artifact", StringComparison.OrdinalIgnoreCase) == true,
            "Preview generation should carry a standardized artifact cache identity.");
    }

    public static async Task HandleAsync_ShouldHydrateCachedMeshGeometry_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.mesh.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var meshAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Mesh.ToString(),
            AssetTypeDisplayName = AssetType.Mesh.ToString(),
            SerializedFileName = "mesh.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [meshAssetEntry]
        };
        var meshGeometry = CreateMeshGeometry();
        var previewCachePayload = CreateMeshPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            meshGeometry);
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

        TestAssert.True(result.IsSuccess, "Matching mesh preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Mesh, preview.Kind, "Cached mesh preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "Mesh preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Mesh preview JSON should be hydrated into structured geometry.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached mesh preview should expose resolved payload path.");
        var restoredGeometry = preview.MeshGeometry ?? throw new InvalidOperationException("Expected mesh geometry.");
        TestAssert.Equal(meshGeometry.SourceVertexCount, restoredGeometry.SourceVertexCount, "Mesh vertex count should round-trip.");
        TestAssert.Equal(meshGeometry.SourceIndexCount, restoredGeometry.SourceIndexCount, "Mesh index count should round-trip.");
        TestAssert.Equal(3, restoredGeometry.Edges.Count, "Mesh edges should be hydrated from cached JSON.");
        TestAssert.Equal(1, restoredGeometry.Triangles?.Count ?? 0, "Mesh triangles should be hydrated from cached JSON.");
        TestAssert.Equal(1, restoredGeometry.SubMeshes.Count, "Mesh submesh summary should be hydrated from cached JSON.");
        TestAssert.Equal(3, restoredGeometry.Uv0?.Count ?? 0, "Mesh UV0 coordinates should be hydrated from cached JSON.");
        TestAssert.Equal(2, restoredGeometry.Channels?.Count ?? 0, "Mesh channel metadata should be hydrated from cached JSON.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }

    public static async Task HandleAsync_ShouldHydrateCachedMaterialPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.material.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var materialAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Material.ToString(),
            AssetTypeDisplayName = AssetType.Material.ToString(),
            SerializedFileName = "materials.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [materialAssetEntry]
        };
        var material = CreateMaterialPreview();
        var previewCachePayload = CreateMaterialPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            material);
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

        TestAssert.True(result.IsSuccess, "Matching material preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Material, preview.Kind, "Cached material preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "Material preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Material preview JSON should be hydrated into structured material data.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached material preview should expose resolved payload path.");
        var restoredMaterial = preview.Material ?? throw new InvalidOperationException("Expected material preview.");
        TestAssert.Equal("Particles/Additive", restoredMaterial.Shader?.Name, "Material shader name should round-trip.");
        TestAssert.Equal(1, restoredMaterial.TextureProperties.Count, "Texture slots should be hydrated from cached JSON.");
        TestAssert.Equal("_MainTex", restoredMaterial.TextureProperties[0].Name, "Texture property name should round-trip.");
        TestAssert.True(restoredMaterial.TextureProperties[0].IsExternal, "External texture reference should round-trip.");
        TestAssert.Equal(1, restoredMaterial.ColorProperties.Count, "Color properties should be hydrated from cached JSON.");
        TestAssert.Equal("_Color", restoredMaterial.ColorProperties[0].Name, "Color property name should round-trip.");
        TestAssert.Equal(1, restoredMaterial.FloatProperties.Count, "Float properties should be hydrated from cached JSON.");
        TestAssert.Equal(1, restoredMaterial.IntegerProperties.Count, "Integer properties should be hydrated from cached JSON.");
        TestAssert.Equal(0, decryptor.DecryptCallCount, "Cache hit should not invoke decryptor.");
        TestAssert.Equal(0, generator.GenerateCallCount, "Cache hit should not invoke preview generator.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Cache hit should not rewrite preview cache.");
    }

    public static async Task HandleAsync_ShouldHydrateCachedAnimationClipPreview_WhenPreviewCacheStillMatches()
    {
        const string resolvedPayloadFilePath = @"D:\WorkspaceData\preview-cache\bundle-cache-001\asset-001.animation-clip.json";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var animationClipAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Unknown.ToString(),
            AssetTypeDisplayName = "AnimationClip",
            SerializedFileName = "animations.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [animationClipAssetEntry]
        };
        var animationClip = CreateAnimationClipPreview();
        var previewCachePayload = CreateAnimationClipPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-1",
            animationClip);
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

        TestAssert.True(result.IsSuccess, "Matching animation clip preview cache should return cached preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.AnimationClip, preview.Kind, "Cached animation clip preview kind should round-trip.");
        TestAssert.Equal(null, preview.BinaryContent, "AnimationClip preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "AnimationClip preview JSON should be hydrated into structured animation data.");
        TestAssert.Equal(resolvedPayloadFilePath, preview.PreviewPayloadFilePath, "Cached animation clip preview should expose resolved payload path.");
        var restoredAnimationClip = preview.AnimationClip ?? throw new InvalidOperationException("Expected animation clip preview.");
        TestAssert.Equal(30d, restoredAnimationClip.SampleRate, "AnimationClip sample rate should round-trip.");
        TestAssert.Equal(1.5d, restoredAnimationClip.Duration, "AnimationClip duration should round-trip.");
        TestAssert.Equal(45L, restoredAnimationClip.EstimatedFrameCount, "AnimationClip estimated frame count should round-trip.");
        TestAssert.Equal(8, restoredAnimationClip.FloatCurveCount, "AnimationClip curve counts should round-trip.");
        TestAssert.Equal(1, restoredAnimationClip.Events.Count, "Animation events should be hydrated from cached JSON.");
        TestAssert.Equal("OnStep", restoredAnimationClip.Events[0].FunctionName, "Animation event function should round-trip.");
        TestAssert.Equal(3, restoredAnimationClip.CurveBindingCount, "Animation curve binding count should round-trip.");
        TestAssert.Equal(3, restoredAnimationClip.CurveBindings?.Count, "Animation curve binding rows should be hydrated from cached JSON.");
        TestAssert.Equal(AssetPreviewAnimationCurveBindingKind.Float, restoredAnimationClip.CurveBindings![0].Kind, "Animation curve binding kind should round-trip.");
        TestAssert.Equal("Root/Hips", restoredAnimationClip.CurveBindings[0].Path, "Animation curve binding path should round-trip.");
        TestAssert.Equal(4, restoredAnimationClip.CurveBindings[0].KeyframeCount, "Animation curve binding keyframe count should round-trip.");
        TestAssert.Equal(AssetPreviewAnimationCurveBindingKind.Generic, restoredAnimationClip.CurveBindings[2].Kind, "Generic animation binding kind should round-trip.");
        TestAssert.Equal<int?>(null, restoredAnimationClip.CurveBindings[2].KeyframeCount, "Packed generic binding keyframe count should stay unknown.");
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

    public static async Task HandleAsync_ShouldRegenerateMeshPreview_WhenGeneratorVersionChanges()
    {
        const string resolvedBundlePath = @"C:\Temp\mesh.bundle";

        var query = CreateQuery();
        var bundleCacheEntry = CreateBundleCacheEntry(query.WorkspaceKey, query.BundleId.Value, query.AssetId.Value);
        var meshAssetEntry = bundleCacheEntry.Assets[0] with
        {
            AssetType = AssetType.Mesh.ToString(),
            AssetTypeDisplayName = AssetType.Mesh.ToString(),
            SerializedFileName = "mesh.assets"
        };
        bundleCacheEntry = bundleCacheEntry with
        {
            Assets = [meshAssetEntry]
        };
        var stalePreviewCachePayload = CreateMeshPreviewPayload(
            bundleCacheEntry,
            query.AssetId.Value,
            generatorVersion: "generator-legacy",
            CreateMeshGeometry());
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
        var regeneratedGeometry = CreateMeshGeometry() with
        {
            SourceVertexCount = 4,
            IsGeometryTruncated = true
        };
        var generator = new TestAssetPreviewGenerator
        {
            GenerateResultFactory = _ => Task.FromResult(Result<AssetPreviewContentDto>.Success(
                new AssetPreviewContentDto(
                    Kind: AssetPreviewKind.Mesh,
                    ContentType: "application/vnd.assestbundletools.mesh-preview+json",
                    Content: JsonSerializer.SerializeToUtf8Bytes(
                        regeneratedGeometry,
                        TestMeshPreviewJsonSerializerOptions),
                    PreviewInfoItems:
                    [
                        new AssetPreviewInfoItemDto(
                            AssetPreviewInfoKey.MeshVertexCount,
                            AssetPreviewInfoValueKind.Integer,
                            Int64Value: regeneratedGeometry.SourceVertexCount)
                    ])))
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

        TestAssert.True(result.IsSuccess, "Generator version change should rebuild mesh preview successfully.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Mesh, preview.Kind, "Regenerated preview should preserve mesh kind.");
        TestAssert.Equal(null, preview.BinaryContent, "Mesh preview should not expose binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Mesh preview JSON should be hydrated into structured geometry.");
        TestAssert.Equal(4, preview.MeshGeometry?.SourceVertexCount, "Regenerated mesh geometry should be exposed.");
        TestAssert.True(preview.MeshGeometry?.IsGeometryTruncated == true, "Regenerated mesh geometry should preserve truncation state.");
        TestAssert.Equal(3, preview.MeshGeometry?.Uv0?.Count ?? 0, "Regenerated mesh geometry should preserve UV0 data.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.GeneratorVersionChanged, preview.Diagnostic.CacheStatus, "Generator version invalidation should be diagnosed explicitly.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.None, preview.Diagnostic.FallbackReason, "Successful regeneration should not fall back to summary.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Generator version invalidation should decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Generator version invalidation should regenerate preview once.");
        TestAssert.Equal(1, previewCacheStore.SaveCallCount, "Regeneration should rewrite preview cache once.");
        TestAssert.Equal(
            Path.Combine(CreatePreviewCacheKey(bundleCacheEntry.BundleCacheKey, query.AssetId.Value), "payload.mesh.json"),
            previewCacheStore.LastSavedPayload?.Descriptor.RelativePayloadPath,
            "Saved mesh preview should use the mesh JSON payload file name.");
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

    public static async Task HandleAsync_ShouldReturnOutOfMemoryFallbackSummary_WhenPreviewGenerationRunsOutOfMemory()
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
                ApplicationErrors.BundlePreviewOutOfMemory(resolvedBundlePath, "allocation failed")))
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

        TestAssert.True(result.IsSuccess, "Out-of-memory preview generation should fall back to a summary preview.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(AssetPreviewKind.Summary, preview.Kind, "Fallback preview should use summary kind.");
        TestAssert.Equal(AssetPreviewMessageKey.OutOfMemory, preview.PreviewMessage?.Key, "Fallback summary should report memory pressure.");
        TestAssert.Equal(AssetPreviewFallbackReasonKind.OutOfMemory, preview.Diagnostic.FallbackReason, "Diagnostic should report preview memory pressure.");
        TestAssert.Equal(AssetPreviewCacheStatusKind.Missing, preview.Diagnostic.CacheStatus, "Missing preview cache should be reported.");
        TestAssert.Equal("Error.Bundle.Preview.OutOfMemory", preview.Diagnostic.DetailMessage?.MessageToken, "Diagnostic detail should preserve the structured memory error token.");
        TestAssert.Equal(
            "allocation failed",
            TestAssert.Single(preview.Diagnostic.DetailMessage?.Arguments ?? Array.Empty<string>()),
            "Out-of-memory fallback detail should carry the diagnostic detail.");
        TestAssert.Equal("text/plain; charset=utf-8", preview.ContentType, "Fallback summary should use plain text content type.");
        TestAssert.Equal(null, preview.BinaryContent, "Fallback summary should not include binary payload.");
        TestAssert.Equal(null, preview.TextContent, "Fallback summary should not include decoded text payload.");
        TestAssert.False(preview.IsFromCache, "Fallback summary should not be marked as cache hit.");
        TestAssert.Equal(1, decryptor.DecryptCallCount, "Generation failure path should still decrypt once.");
        TestAssert.Equal(1, generator.GenerateCallCount, "Generation failure path should invoke preview generator once.");
        TestAssert.Equal(0, previewCacheStore.SaveCallCount, "Fallback summary should not write preview cache.");
    }
}
