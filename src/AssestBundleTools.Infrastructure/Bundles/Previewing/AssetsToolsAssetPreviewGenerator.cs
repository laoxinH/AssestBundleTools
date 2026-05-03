using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
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
    private const string TextContentType = "text/plain; charset=utf-8";
    private const int MaxTextPreviewCharacters = 24000;

    private readonly IAudioPayloadTranscodingService _audioPayloadTranscodingService;

    public AssetsToolsAssetPreviewGenerator(IAudioPayloadTranscodingService audioPayloadTranscodingService)
    {
        _audioPayloadTranscodingService = audioPayloadTranscodingService ?? throw new ArgumentNullException(nameof(audioPayloadTranscodingService));
    }

    public string GeneratorVersion => "assetstools.preview/12";

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
        catch (Exception exception)
        {
            return Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundlePreviewGenerationFailed(
                    request.ResolvedBundlePath,
                    exception.ToString()));
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
            AssetsToolsSpritePreviewBuilder.TryBuildSpritePreview(assetsManager, serializedFile, baseField, request, out var spritePreview))
        {
            return spritePreview;
        }

        if (classId == AssetClassID.AudioClip)
        {
            return await BuildAudioPreviewAsync(bundleInstance, baseField, request, cancellationToken);
        }

        if (classId == AssetClassID.Mesh &&
            AssetsToolsMeshPreviewBuilder.TryBuildMeshPreview(baseField, request.SizeInBytes, out var meshPreview))
        {
            return meshPreview;
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

        if (!AssetsToolsImagePreviewSupport.TryDecodeTextureImage(serializedFile, baseField, out var textureImage))
        {
            return false;
        }

        return AssetsToolsImagePreviewSupport.TryEncodeTexturePreview(
            textureImage,
            AssetsToolsImagePreviewSupport.BuildTexturePreviewInfoItems(baseField, textureImage, request.SizeInBytes),
            AssetsToolsAssetDisplayMetadataResolver.ResolvePreferredDisplaySizeInBytes(AssetClassID.Texture2D, baseField),
            imageOverlay: null,
            imageFocus: null,
            out previewContent);
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
                AddTextInfo(items, AssetPreviewInfoKey.SpriteTextureRect, AssetsToolsImagePreviewSupport.FormatRect(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "textureRect")));
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
        AssetsToolsImagePreviewSupport.AddTextInfo(items, key, value);
    }

    private static void AddIntegerInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, key, value);
    }

    private static void AddInteger64Info(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        AssetsToolsImagePreviewSupport.AddInteger64Info(items, key, value);
    }

    private static void AddByteSizeInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        long? value)
    {
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, key, value);
    }

    private static void AddDecimalInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        AssetsToolsImagePreviewSupport.AddDecimalInfo(items, key, value);
    }

    private static void AddDurationInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        float? value)
    {
        AssetsToolsImagePreviewSupport.AddDurationInfo(items, key, value);
    }

    private static void AddFrequencyInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        AssetsToolsImagePreviewSupport.AddFrequencyInfo(items, key, value);
    }

    private static void AddBitsInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        int? value)
    {
        AssetsToolsImagePreviewSupport.AddBitsInfo(items, key, value);
    }

    private static void AddBooleanInfo(
        ICollection<AssetPreviewInfoItemDto> items,
        AssetPreviewInfoKey key,
        bool? value)
    {
        AssetsToolsImagePreviewSupport.AddBooleanInfo(items, key, value);
    }

    private static AssetClassID ResolveClassId(int rawTypeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), rawTypeId)
            ? (AssetClassID)rawTypeId
            : 0;
    }
}
