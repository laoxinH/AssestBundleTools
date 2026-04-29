using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Infrastructure.Bundles.Internal;
using AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing;

/// <summary>
/// 基于 AssetsTools.NET 的真实资源预览生成器。
/// </summary>
public sealed class AssetsToolsAssetPreviewGenerator : IAssetPreviewGenerator
{
    private const string PngContentType = "image/png";
    private const string TextContentType = "text/plain; charset=utf-8";
    private const int MaxTextPreviewCharacters = 24000;
    private const string PreviewPixelEncoding = "RGBA8";
    private const string PreviewContainerEncoding = "PNG";

    private readonly IAudioPayloadTranscodingService _audioPayloadTranscodingService;

    public AssetsToolsAssetPreviewGenerator(IAudioPayloadTranscodingService audioPayloadTranscodingService)
    {
        _audioPayloadTranscodingService = audioPayloadTranscodingService ?? throw new ArgumentNullException(nameof(audioPayloadTranscodingService));
    }

    public string GeneratorVersion => "assetstools.preview/8";

    public async Task<Result<AssetPreviewContentDto>> GenerateAsync(
        AssetPreviewBuildRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetPreviewContentDto>.Cancelled();
        }

        if (!File.Exists(request.ResolvedBundlePath))
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundleSourceMissing(request.ResolvedBundlePath));
        }

        var assetsManager = new AssetsManager
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

        try
        {
            var bundleInstance = assetsManager.LoadBundleFile(request.ResolvedBundlePath, unpackIfPacked: true);
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance, cancellationToken);
            var serializedFile = serializedFiles.FirstOrDefault(file =>
                string.Equals(file.name, request.SerializedFileName, StringComparison.OrdinalIgnoreCase));
            if (serializedFile is null)
            {
                return Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundlePreviewSerializedFileLocatorMissing(request.ResolvedBundlePath));
            }

            if (!AssetsToolsFieldReader.TryFindAssetInfoByPathId(serializedFile, request.PathId, out var assetInfo))
            {
                return Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundlePreviewAssetLocatorMissing(request.ResolvedBundlePath));
            }

            var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
            if (baseField is null)
            {
                return Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundlePreviewFieldTreeUnavailable(request.ResolvedBundlePath));
            }

            var classId = ResolveClassId(assetInfo.TypeId);
            var previewContent = await BuildPreviewContentAsync(
                assetsManager,
                bundleInstance,
                serializedFile,
                assetInfo,
                baseField,
                classId,
                request,
                cancellationToken);

            return Result<AssetPreviewContentDto>.Success(previewContent);
        }
        catch (OperationCanceledException)
        {
            return Result<AssetPreviewContentDto>.Cancelled();
        }
        catch (Exception)
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(request.ResolvedBundlePath));
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static IReadOnlyList<AssetsFileInstance> LoadSerializedFiles(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance,
        CancellationToken cancellationToken)
    {
        var serializedFiles = new List<AssetsFileInstance>();
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;

        for (var index = 0; index < directoryInfos.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!bundleInstance.file.IsAssetsFile(index))
            {
                continue;
            }

            serializedFiles.Add(assetsManager.LoadAssetsFileFromBundle(bundleInstance, index, loadDeps: false));
        }

        return serializedFiles;
    }

    private async Task<AssetPreviewContentDto> BuildPreviewContentAsync(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance,
        AssetsFileInstance serializedFile,
        AssetFileInfo assetInfo,
        AssetTypeValueField baseField,
        AssetClassID classId,
        AssetPreviewBuildRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (classId == AssetClassID.Texture2D &&
            TryBuildTexturePreview(serializedFile, baseField, request, out var texturePreview))
        {
            return texturePreview;
        }

        if (classId == AssetClassID.Sprite &&
            TryBuildSpritePreview(assetsManager, serializedFile, baseField, request, out var spritePreview))
        {
            return spritePreview;
        }

        if (classId == AssetClassID.AudioClip)
        {
            return await BuildAudioPreviewAsync(bundleInstance, baseField, request, cancellationToken);
        }

        if (classId == AssetClassID.TextAsset &&
            TryBuildTextPreview(baseField, "m_Script", out var textAssetPreview))
        {
            return textAssetPreview;
        }

        if (classId == AssetClassID.Shader &&
            TryBuildTextPreview(baseField, "m_Script", out var shaderPreview))
        {
            return shaderPreview;
        }

        return BuildSummaryPreview(assetInfo, baseField, classId);
    }

    private async Task<AssetPreviewContentDto> BuildAudioPreviewAsync(
        BundleFileInstance bundleInstance,
        AssetTypeValueField baseField,
        AssetPreviewBuildRequestDto request,
        CancellationToken cancellationToken)
    {
        var previewSource = AssetsToolsAudioClipPreviewBuilder.BuildPreviewSource(bundleInstance, baseField, request);
        if (previewSource.Payload is null)
        {
            return AssetsToolsAudioClipPreviewBuilder.BuildSummaryPreview(
                previewSource,
                new AssetPreviewMessageDto(AssetPreviewMessageKey.AudioPayloadMissing));
        }

        if (previewSource.Container.IsPlayable)
        {
            return AssetsToolsAudioClipPreviewBuilder.BuildDirectPlayablePreview(previewSource);
        }

        if (!AssetsToolsAudioClipPreviewBuilder.CanAttemptExternalTranscode(previewSource))
        {
            return AssetsToolsAudioClipPreviewBuilder.BuildSummaryPreview(
                previewSource,
                AssetsToolsAudioClipPreviewBuilder.BuildUnsupportedContainerMessage(previewSource));
        }

        var transcodeResult = await _audioPayloadTranscodingService.TryTranscodeAsync(
            new AudioPayloadTranscodeRequestDto(
                Payload: previewSource.Payload.Content,
                ContainerKind: previewSource.Container.Kind,
                SourceFileName: AssetsToolsAudioClipPreviewBuilder.ResolveSuggestedTranscodeSourceFileName(previewSource),
                WorkspaceKey: request.WorkspaceKey,
                WorkspaceDataRootPath: request.WorkspaceDataRootPath),
            cancellationToken);
        if (transcodeResult.IsSuccess)
        {
            return AssetsToolsAudioClipPreviewBuilder.BuildTranscodedPreview(previewSource, transcodeResult);
        }

        return AssetsToolsAudioClipPreviewBuilder.BuildSummaryPreview(
            previewSource,
            AssetsToolsAudioClipPreviewBuilder.BuildTranscodeFallbackMessage(previewSource, transcodeResult));
    }

    private static bool TryBuildTexturePreview(
        AssetsFileInstance serializedFile,
        AssetTypeValueField baseField,
        AssetPreviewBuildRequestDto request,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        if (!TryDecodeTextureImage(serializedFile, baseField, out var textureImage))
        {
            return false;
        }

        return TryEncodeTexturePreview(
            textureImage,
            BuildTexturePreviewInfoItems(baseField, textureImage, request.SizeInBytes),
            AssetsToolsAssetDisplayMetadataResolver.ResolvePreferredDisplaySizeInBytes(AssetClassID.Texture2D, baseField),
            imageOverlay: null,
            out previewContent);
    }

    private static bool TryDecodeTextureImage(
        AssetsFileInstance serializedFile,
        AssetTypeValueField baseField,
        out DecodedTextureImage textureImage)
    {
        textureImage = default;

        try
        {
            var textureFile = TextureFile.ReadTextureFile(baseField);
            if (textureFile.m_Width <= 0 || textureFile.m_Height <= 0)
            {
                return false;
            }

            var pictureData = textureFile.FillPictureData(serializedFile);
            if (pictureData is not { Length: > 0 })
            {
                return false;
            }

            var decodedPixels = textureFile.DecodeTextureRaw(pictureData, useBgra: false);
            var expectedLength = checked((long)textureFile.m_Width * textureFile.m_Height * 4L);
            if (decodedPixels is not { Length: > 0 } || decodedPixels.Length < expectedLength || expectedLength > int.MaxValue)
            {
                return false;
            }

            if (decodedPixels.Length != expectedLength)
            {
                Array.Resize(ref decodedPixels, (int)expectedLength);
            }

            textureImage = new DecodedTextureImage(textureFile.m_Width, textureFile.m_Height, decodedPixels);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryBuildSpritePreview(
        AssetsManager assetsManager,
        AssetsFileInstance serializedFile,
        AssetTypeValueField baseField,
        AssetPreviewBuildRequestDto request,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        var texturePointer = AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "texture");
        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(texturePointer, "m_FileID")) ?? -1;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(texturePointer, "m_PathID"));
        if (fileId != 0 || pathId is null)
        {
            return false;
        }

        if (!AssetsToolsFieldReader.TryFindAssetInfoByPathId(serializedFile, pathId.Value, out var textureAssetInfo))
        {
            return false;
        }

        var textureField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, textureAssetInfo);
        if (textureField is null || !TryDecodeTextureImage(serializedFile, textureField, out var textureImage))
        {
            return false;
        }

        _ = TryCropSpriteTexture(baseField, textureImage, out _, out var cropMetadata);

        return TryEncodeTexturePreview(
            textureImage,
            BuildSpritePreviewInfoItems(baseField, textureField, textureImage, pathId.Value, cropMetadata, request.SizeInBytes),
            preferredDisplaySizeInBytes: null,
            imageOverlay: BuildSpritePreviewImageOverlay(textureImage, cropMetadata),
            out previewContent);
    }

    private static bool TryCropSpriteTexture(
        AssetTypeValueField baseField,
        DecodedTextureImage sourceImage,
        out DecodedTextureImage croppedImage,
        out SpriteCropMetadata cropMetadata)
    {
        croppedImage = default;
        cropMetadata = default;

        var textureRectField = AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "textureRect");
        if (textureRectField is null)
        {
            return false;
        }

        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "y"));
        var width = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "width"));
        var height = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(textureRectField, "height"));
        if (!x.HasValue || !y.HasValue || !width.HasValue || !height.HasValue)
        {
            return false;
        }

        var cropX = Math.Max(0, (int)MathF.Floor(x.Value));
        var cropYFromBottom = Math.Max(0, (int)MathF.Floor(y.Value));
        var cropWidth = Math.Max(1, (int)MathF.Round(width.Value));
        var cropHeight = Math.Max(1, (int)MathF.Round(height.Value));

        if (cropX >= sourceImage.Width || cropYFromBottom >= sourceImage.Height)
        {
            return false;
        }

        cropWidth = Math.Min(cropWidth, sourceImage.Width - cropX);
        cropHeight = Math.Min(cropHeight, sourceImage.Height - cropYFromBottom);
        if (cropWidth <= 0 || cropHeight <= 0)
        {
            return false;
        }

        var croppedPixels = new byte[checked(cropWidth * cropHeight * 4)];
        var sourceStride = checked(sourceImage.Width * 4);
        var targetStride = checked(cropWidth * 4);

        for (var row = 0; row < cropHeight; row++)
        {
            var sourceOffset = checked(((cropYFromBottom + row) * sourceStride) + (cropX * 4));
            var targetOffset = row * targetStride;
            Buffer.BlockCopy(sourceImage.RgbaPixels, sourceOffset, croppedPixels, targetOffset, targetStride);
        }

        croppedImage = new DecodedTextureImage(cropWidth, cropHeight, croppedPixels);
        var cropYFromTop = Math.Max(0, sourceImage.Height - (cropYFromBottom + cropHeight));

        cropMetadata = new SpriteCropMetadata(
            PixelX: cropX,
            PixelYFromTop: cropYFromTop,
            PixelWidth: cropWidth,
            PixelHeight: cropHeight);
        return true;
    }

    private static bool TryEncodeTexturePreview(
        DecodedTextureImage textureImage,
        IReadOnlyList<AssetPreviewInfoItemDto> previewInfoItems,
        long? preferredDisplaySizeInBytes,
        AssetPreviewImageOverlayDto? imageOverlay,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        try
        {
            previewContent = new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Image,
                ContentType: PngContentType,
                Content: EncodeRgbaPng(textureImage.Width, textureImage.Height, textureImage.RgbaPixels),
                PreviewInfoItems: previewInfoItems,
                PreferredDisplaySizeInBytes: preferredDisplaySizeInBytes,
                ImageOverlay: imageOverlay);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryBuildTextPreview(
        AssetTypeValueField baseField,
        string fieldName,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        var textField = AssetsToolsFieldReader.TryGetField(baseField, fieldName);
        var textContent = AssetsToolsFieldReader.ReadString(textField);
        if (string.IsNullOrWhiteSpace(textContent))
        {
            textContent = DecodeTextPayload(AssetsToolsFieldReader.ReadByteArray(textField));
        }

        if (string.IsNullOrWhiteSpace(textContent))
        {
            return false;
        }

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Text,
            ContentType: TextContentType,
            Content: Encoding.UTF8.GetBytes(TrimTextPreview(textContent)),
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static string DecodeTextPayload(byte[]? payload)
    {
        if (payload is not { Length: > 0 })
        {
            return string.Empty;
        }

        try
        {
            if (payload.Length >= 2 &&
                payload[0] == 0xFF &&
                payload[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(payload).TrimEnd('\0');
            }

            if (payload.Length >= 2 &&
                payload[0] == 0xFE &&
                payload[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(payload).TrimEnd('\0');
            }

            return Encoding.UTF8.GetString(payload).TrimEnd('\0');
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string TrimTextPreview(string textContent)
    {
        var normalizedText = textContent.Replace("\r\n", "\n").Replace('\r', '\n');
        if (normalizedText.Length <= MaxTextPreviewCharacters)
        {
            return normalizedText;
        }

        return string.Concat(
            normalizedText[..MaxTextPreviewCharacters]);
    }

    private static AssetPreviewContentDto BuildSummaryPreview(
        AssetFileInfo assetInfo,
        AssetTypeValueField baseField,
        AssetClassID classId)
    {
        // 详情页已经统一展示通用身份信息，
        // 摘要预览这里只保留类型专属补充字段，避免和“名称 / 路径 / PathId / 大小”重复。
        var items = new List<AssetPreviewInfoItemDto>();

        switch (classId)
        {
            case AssetClassID.AudioClip:
                AddIntegerInfo(items, AssetPreviewInfoKey.AudioLoadType, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")));
                AddIntegerInfo(items, AssetPreviewInfoKey.AudioChannels, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Channels")));
                AddFrequencyInfo(items, AssetPreviewInfoKey.AudioFrequency, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Frequency")));
                AddBitsInfo(items, AssetPreviewInfoKey.AudioBitsPerSample, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_BitsPerSample")));
                AddDurationInfo(items, AssetPreviewInfoKey.AudioDuration, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_Length")));
                AddIntegerInfo(items, AssetPreviewInfoKey.AudioCompressionFormatId, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_CompressionFormat")));
                AddByteSizeInfo(items, AssetPreviewInfoKey.AudioResourceStreamSize, AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_Resource", "m_Size")));
                break;

            case AssetClassID.Mesh:
                AddIntegerInfo(items, AssetPreviewInfoKey.MeshSubMeshCount, CountArray(baseField, "m_SubMeshes"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MeshBlendShapeCount, CountArray(baseField, "m_Shapes", "channels"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MeshBoneNameHashCount, CountArray(baseField, "m_BoneNameHashes"));
                break;

            case AssetClassID.Material:
                AddIntegerInfo(items, AssetPreviewInfoKey.MaterialTexturePropertyCount, CountArray(baseField, "m_SavedProperties", "m_TexEnvs"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MaterialFloatPropertyCount, CountArray(baseField, "m_SavedProperties", "m_Floats"));
                AddIntegerInfo(items, AssetPreviewInfoKey.MaterialColorPropertyCount, CountArray(baseField, "m_SavedProperties", "m_Colors"));
                break;

            case AssetClassID.GameObject:
                AddIntegerInfo(items, AssetPreviewInfoKey.GameObjectComponentCount, CountArray(baseField, "m_Component"));
                AddIntegerInfo(items, AssetPreviewInfoKey.GameObjectLayer, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Layer")));
                AddBooleanInfo(items, AssetPreviewInfoKey.GameObjectIsActive, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_IsActive")));
                break;

            case AssetClassID.MonoBehaviour:
                AddBooleanInfo(items, AssetPreviewInfoKey.MonoBehaviourEnabled, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_Enabled")));
                AddInteger64Info(items, AssetPreviewInfoKey.MonoBehaviourScriptPathId, AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_Script", "m_PathID")));
                break;

            case AssetClassID.AnimationClip:
                AddDecimalInfo(items, AssetPreviewInfoKey.AnimationClipSampleRate, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_SampleRate")));
                AddBooleanInfo(items, AssetPreviewInfoKey.AnimationClipIsLegacy, AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_Legacy")));
                AddIntegerInfo(items, AssetPreviewInfoKey.AnimationClipEventCount, CountArray(baseField, "m_Events"));
                break;

            case AssetClassID.AnimatorController:
                AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerLayerCount, CountArray(baseField, "m_AnimatorLayers"));
                AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerParameterCount, CountArray(baseField, "m_AnimatorParameters"));
                break;

            case AssetClassID.Font:
                AddDecimalInfo(items, AssetPreviewInfoKey.FontLineSpacing, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_LineSpacing")));
                AddIntegerInfo(items, AssetPreviewInfoKey.FontDefaultSize, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_DefaultSize")));
                AddIntegerInfo(items, AssetPreviewInfoKey.FontAsciiStartOffset, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_AsciiStartOffset")));
                break;

            case AssetClassID.SpriteAtlas:
                AddIntegerInfo(items, AssetPreviewInfoKey.SpriteAtlasPackedSpriteCount, CountArray(baseField, "m_PackedSprites"));
                break;

            case AssetClassID.Sprite:
                AddDecimalInfo(items, AssetPreviewInfoKey.SpritePixelsPerUnit, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_PixelsToUnits")));
                AddInteger64Info(items, AssetPreviewInfoKey.SpriteTexturePathId, AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "texture", "m_PathID")));
                AddTextInfo(items, AssetPreviewInfoKey.SpriteTextureRect, FormatRect(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "textureRect")));
                break;

            case AssetClassID.Texture2D:
                AddIntegerInfo(items, AssetPreviewInfoKey.TextureWidth, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Width")));
                AddIntegerInfo(items, AssetPreviewInfoKey.TextureHeight, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Height")));
                AddIntegerInfo(items, AssetPreviewInfoKey.TextureMipCount, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_MipCount")));
                break;

            default:
                AddIntegerInfo(items, AssetPreviewInfoKey.UnknownTypeId, assetInfo.TypeId);
                break;
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            Content: Array.Empty<byte>(),
            PreviewInfoItems: items,
            PreviewMessage: new AssetPreviewMessageDto(AssetPreviewMessageKey.MetadataOnly),
            PreferredDisplaySizeInBytes: null);
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildTexturePreviewInfoItems(
        AssetTypeValueField textureField,
        DecodedTextureImage textureImage,
        long serializedObjectSizeInBytes)
    {
        var completeImageSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(textureField, "m_CompleteImageSize"));
        var streamSize = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(textureField, "m_StreamData", "size"));
        var streamSizeValue = streamSize.GetValueOrDefault();
        var hasDedicatedStreamSize = streamSizeValue > 0;
        var hasStandaloneCompleteSize = completeImageSize.HasValue &&
                                        completeImageSize.Value > 0 &&
                                        (!hasDedicatedStreamSize || completeImageSize.Value != streamSizeValue);

        var items = new List<AssetPreviewInfoItemDto>();
        AddTextInfo(items, AssetPreviewInfoKey.ImageDimensions, FormatDimensions(textureImage.Width, textureImage.Height));
        AddTextInfo(items, AssetPreviewInfoKey.PreviewPixelEncoding, PreviewPixelEncoding);
        AddTextInfo(items, AssetPreviewInfoKey.PreviewOutputFormat, PreviewContainerEncoding);
        AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AddIntegerInfo(items, AssetPreviewInfoKey.TextureFormatId, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_TextureFormat")));
        AddIntegerInfo(items, AssetPreviewInfoKey.TextureMipCount, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_MipCount")));
        AddByteSizeInfo(items, AssetPreviewInfoKey.TextureDecodedSize, textureImage.RgbaPixels.LongLength);
        AddByteSizeInfo(items, AssetPreviewInfoKey.TextureOriginalDataSize, hasStandaloneCompleteSize ? completeImageSize : null);
        AddByteSizeInfo(items, AssetPreviewInfoKey.TextureStreamDataSize, hasDedicatedStreamSize ? streamSize : null);
        AddTextInfo(items, AssetPreviewInfoKey.TextureStreamDataPath, AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(textureField, "m_StreamData", "path")));
        return items;
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildSpritePreviewInfoItems(
        AssetTypeValueField spriteField,
        AssetTypeValueField textureField,
        DecodedTextureImage textureImage,
        long texturePathId,
        SpriteCropMetadata cropMetadata,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AddTextInfo(items, AssetPreviewInfoKey.ImageDimensions, FormatDimensions(textureImage.Width, textureImage.Height));
        AddTextInfo(items, AssetPreviewInfoKey.PreviewPixelEncoding, PreviewPixelEncoding);
        AddTextInfo(items, AssetPreviewInfoKey.PreviewOutputFormat, PreviewContainerEncoding);
        AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AddInteger64Info(items, AssetPreviewInfoKey.SpriteTexturePathId, texturePathId);
        AddDecimalInfo(items, AssetPreviewInfoKey.SpritePixelsPerUnit, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(spriteField, "m_PixelsToUnits")));
        AddTextInfo(items, AssetPreviewInfoKey.SpriteTextureRect, FormatSpritePreviewRect(cropMetadata, spriteField));
        AddTextInfo(items, AssetPreviewInfoKey.SpriteCropSize, cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0
            ? FormatDimensions(cropMetadata.PixelWidth, cropMetadata.PixelHeight)
            : null);
        AddTextInfo(items, AssetPreviewInfoKey.SpriteCropOrigin, cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0
            ? FormattableString.Invariant($"x={cropMetadata.PixelX}, y={cropMetadata.PixelYFromTop}")
            : null);
        AddIntegerInfo(items, AssetPreviewInfoKey.SpriteSourceTextureFormatId, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_TextureFormat")));
        return items;
    }

    private static AssetPreviewImageOverlayDto? BuildSpritePreviewImageOverlay(
        DecodedTextureImage textureImage,
        SpriteCropMetadata cropMetadata)
    {
        if (cropMetadata.PixelWidth <= 0 || cropMetadata.PixelHeight <= 0)
        {
            return null;
        }

        return new AssetPreviewImageOverlayDto(
            CanvasPixelWidth: textureImage.Width,
            CanvasPixelHeight: textureImage.Height,
            HighlightRegion: new AssetPreviewImageRegionDto(
                X: cropMetadata.PixelX,
                Y: cropMetadata.PixelYFromTop,
                Width: cropMetadata.PixelWidth,
                Height: cropMetadata.PixelHeight));
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private static void AddTextInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Text,
            TextValue: value.Trim()));
    }

    private static void AddIntegerInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Integer,
            Int64Value: value.Value));
    }

    private static void AddInteger64Info(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Integer,
            Int64Value: value.Value));
    }

    private static void AddByteSizeInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.ByteSize,
            Int64Value: value.Value));
    }

    private static void AddDecimalInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Decimal,
            NumberValue: value.Value));
    }

    private static void AddDurationInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.DurationSeconds,
            NumberValue: value.Value));
    }

    private static void AddFrequencyInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.FrequencyHertz,
            Int64Value: value.Value));
    }

    private static void AddBitsInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.BitsPerSample,
            Int64Value: value.Value));
    }

    private static void AddBooleanInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        bool? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        items.Add(new AssetPreviewInfoItemDto(
            Key: key,
            ValueKind: AssetPreviewInfoValueKind.Boolean,
            BooleanValue: value.Value));
    }

    private static string? FormatRect(AssetTypeValueField? rectField)
    {
        if (rectField is null)
        {
            return null;
        }

        var x = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "x"));
        var y = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "y"));
        var width = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "width"));
        var height = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(rectField, "height"));
        if (!x.HasValue || !y.HasValue || !width.HasValue || !height.HasValue)
        {
            return null;
        }

        return FormattableString.Invariant(
            $"x={x.Value:0.###}, y={y.Value:0.###}, w={width.Value:0.###}, h={height.Value:0.###}");
    }

    private static string FormatDimensions(int width, int height) =>
        $"{width.ToString(CultureInfo.InvariantCulture)} x {height.ToString(CultureInfo.InvariantCulture)}";

    private static string? FormatSpritePreviewRect(
        SpriteCropMetadata cropMetadata,
        AssetTypeValueField spriteField)
    {
        if (cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0)
        {
            return FormattableString.Invariant(
                $"x={cropMetadata.PixelX}, y={cropMetadata.PixelYFromTop}, w={cropMetadata.PixelWidth}, h={cropMetadata.PixelHeight}");
        }

        return FormatRect(AssetsToolsFieldReader.TryGetField(spriteField, "m_RD", "textureRect"));
    }

    private static AssetClassID ResolveClassId(int rawTypeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), rawTypeId)
            ? (AssetClassID)rawTypeId
            : 0;
    }

    private static byte[] EncodeRgbaPng(int width, int height, byte[] rgbaPixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(rgbaPixels);

        var expectedPixelByteLength = checked(width * height * 4);
        if (rgbaPixels.Length != expectedPixelByteLength)
        {
            throw new ArgumentException("RGBA payload length does not match texture dimensions.", nameof(rgbaPixels));
        }

        using var filteredImageDataStream = new MemoryStream(capacity: checked(height * ((width * 4) + 1)));
        var rowLength = width * 4;
        for (var row = 0; row < height; row++)
        {
            filteredImageDataStream.WriteByte(0);
            var sourceRow = height - row - 1;
            filteredImageDataStream.Write(rgbaPixels, sourceRow * rowLength, rowLength);
        }

        filteredImageDataStream.Position = 0;

        using var compressedImageDataStream = new MemoryStream();
        using (var zlibStream = new ZLibStream(compressedImageDataStream, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            filteredImageDataStream.CopyTo(zlibStream);
        }

        using var pngStream = new MemoryStream();
        pngStream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WritePngChunk(pngStream, "IHDR", BuildPngHeader(width, height));
        WritePngChunk(pngStream, "IDAT", compressedImageDataStream.ToArray());
        WritePngChunk(pngStream, "IEND", Array.Empty<byte>());
        return pngStream.ToArray();
    }

    private static byte[] BuildPngHeader(int width, int height)
    {
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6;
        header[10] = 0;
        header[11] = 0;
        header[12] = 0;
        return header;
    }

    private static void WritePngChunk(Stream outputStream, string chunkType, byte[] chunkData)
    {
        var chunkTypeBytes = Encoding.ASCII.GetBytes(chunkType);
        var chunkLengthBytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(chunkLengthBytes, chunkData.Length);
        outputStream.Write(chunkLengthBytes);
        outputStream.Write(chunkTypeBytes);
        outputStream.Write(chunkData);

        var crcInput = new byte[chunkTypeBytes.Length + chunkData.Length];
        Buffer.BlockCopy(chunkTypeBytes, 0, crcInput, 0, chunkTypeBytes.Length);
        Buffer.BlockCopy(chunkData, 0, crcInput, chunkTypeBytes.Length, chunkData.Length);

        var crcBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, ComputeCrc32(crcInput));
        outputStream.Write(crcBytes);
    }

    private static uint ComputeCrc32(byte[] data)
    {
        const uint Polynomial = 0xEDB88320u;
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ Polynomial
                    : crc >> 1;
            }
        }

        return ~crc;
    }

    private readonly record struct DecodedTextureImage(int Width, int Height, byte[] RgbaPixels);

    private readonly record struct SpriteCropMetadata(
        int PixelX,
        int PixelYFromTop,
        int PixelWidth,
        int PixelHeight);
}
