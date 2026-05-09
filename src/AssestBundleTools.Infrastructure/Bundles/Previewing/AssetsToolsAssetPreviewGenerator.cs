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
    private const string BinaryContentType = "application/octet-stream";

    private readonly IAudioPayloadTranscodingService _audioPayloadTranscodingService;

    public AssetsToolsAssetPreviewGenerator(IAudioPayloadTranscodingService audioPayloadTranscodingService)
    {
        _audioPayloadTranscodingService = audioPayloadTranscodingService ?? throw new ArgumentNullException(nameof(audioPayloadTranscodingService));
    }

    public string GeneratorVersion => "assetstools.preview/23";

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

            var internalSerializedFileLookup = AssetsToolsBundleDependencyResolver.BuildInternalSerializedFileLookup(serializedFiles);
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
                serializedFiles,
                internalSerializedFileLookup,
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
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
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

        if (classId == AssetClassID.Material &&
            AssetsToolsMaterialPreviewBuilder.TryBuildMaterialPreview(
                assetsManager,
                serializedFiles,
                serializedFile,
                baseField,
                internalSerializedFileLookup,
                request.SizeInBytes,
                out var materialPreview))
        {
            return materialPreview;
        }

        if (classId == AssetClassID.AnimationClip &&
            AssetsToolsAnimationClipPreviewBuilder.TryBuildAnimationClipPreview(
                baseField,
                request.SizeInBytes,
                out var animationClipPreview))
        {
            return animationClipPreview;
        }

        if (classId == AssetClassID.Font &&
            AssetsToolsFontPreviewBuilder.TryBuildFontPreview(
                assetsManager,
                serializedFiles,
                serializedFile,
                baseField,
                internalSerializedFileLookup,
                request.SizeInBytes,
                out var fontPreview))
        {
            return fontPreview;
        }

        if (classId == AssetClassID.AnimatorController &&
            AssetsToolsAnimatorControllerPreviewBuilder.TryBuildAnimatorControllerPreview(
                assetsManager,
                serializedFiles,
                serializedFile,
                baseField,
                internalSerializedFileLookup,
                request.SizeInBytes,
                out var animatorControllerPreview))
        {
            return animatorControllerPreview;
        }

        if (classId == AssetClassID.GameObject &&
            AssetsToolsGameObjectPreviewBuilder.TryBuildGameObjectPreview(
                assetsManager,
                serializedFiles,
                serializedFile,
                baseField,
                internalSerializedFileLookup,
                request.SizeInBytes,
                out var gameObjectPreview))
        {
            return gameObjectPreview;
        }

        if (classId == AssetClassID.MonoBehaviour &&
            AssetsToolsMonoBehaviourPreviewBuilder.TryBuildMonoBehaviourPreview(
                assetsManager,
                serializedFiles,
                serializedFile,
                baseField,
                internalSerializedFileLookup,
                request.SizeInBytes,
                out var monoBehaviourPreview))
        {
            return monoBehaviourPreview;
        }

        if (classId == AssetClassID.TextAsset &&
            IsSpineBinarySkeletonRequest(request) &&
            TryBuildBinaryTextAssetPreview(baseField, request.SizeInBytes, out var binaryTextAssetPreview))
        {
            return binaryTextAssetPreview;
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

        return AssetsToolsSummaryPreviewBuilder.BuildSummaryPreview(assetInfo, baseField, classId);
    }

    private static bool IsSpineBinarySkeletonRequest(AssetPreviewBuildRequestDto request)
    {
        var path = string.IsNullOrWhiteSpace(request.AssetPath)
            ? request.AssetName
            : request.AssetPath;
        return !string.IsNullOrWhiteSpace(path) &&
               (path.Trim().EndsWith(".skel", StringComparison.OrdinalIgnoreCase) ||
                path.Trim().EndsWith(".skel.bytes", StringComparison.OrdinalIgnoreCase));
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
            Content: Encoding.UTF8.GetBytes(NormalizeTextContent(textContent)),
            PreviewInfoItems: Array.Empty<AssetPreviewInfoItemDto>(),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static bool TryBuildBinaryTextAssetPreview(
        AssetTypeValueField baseField,
        long? sizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        var textField = AssetsToolsFieldReader.TryGetField(baseField, "m_Script");
        var payload = AssetsToolsFieldReader.ReadByteArray(textField);
        if (payload is not { Length: > 0 })
        {
            return false;
        }

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Binary,
            ContentType: BinaryContentType,
            Content: payload,
            PreviewInfoItems:
            [
                new AssetPreviewInfoItemDto(
                    AssetPreviewInfoKey.SerializedObjectSize,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: payload.Length)
            ],
            PreferredDisplaySizeInBytes: sizeInBytes ?? payload.Length);
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

    private static string NormalizeTextContent(string textContent) =>
        textContent.Replace("\r\n", "\n").Replace('\r', '\n');

    private static AssetClassID ResolveClassId(int rawTypeId)
    {
        return Enum.IsDefined(typeof(AssetClassID), rawTypeId)
            ? (AssetClassID)rawTypeId
            : 0;
    }
}
