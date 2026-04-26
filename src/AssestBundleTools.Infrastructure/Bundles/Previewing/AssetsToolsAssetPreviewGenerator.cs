using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Infrastructure.Bundles.Internal;

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

    public string GeneratorVersion => "assetstools.preview/5";

    public Task<Result<AssetPreviewContentDto>> GenerateAsync(
        AssetPreviewBuildRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetPreviewContentDto>.Cancelled());
        }

        if (!File.Exists(request.ResolvedBundlePath))
        {
            return Task.FromResult(Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundleReadFailed(request.ResolvedBundlePath, "Preview source bundle does not exist.")));
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
                return Task.FromResult(Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundleReadFailed(request.ResolvedBundlePath, "Serialized file locator was not found.")));
            }

            if (!AssetsToolsFieldReader.TryFindAssetInfoByPathId(serializedFile, request.PathId, out var assetInfo))
            {
                return Task.FromResult(Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundleReadFailed(request.ResolvedBundlePath, "Asset locator was not found.")));
            }

            var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile, assetInfo);
            if (baseField is null)
            {
                return Task.FromResult(Result<AssetPreviewContentDto>.Failure(
                    ApplicationErrors.BundleReadFailed(request.ResolvedBundlePath, "Asset field tree could not be loaded.")));
            }

            var classId = ResolveClassId(assetInfo.TypeId);
            var previewContent = BuildPreviewContent(
                assetsManager,
                serializedFile,
                assetInfo,
                baseField,
                classId,
                request,
                cancellationToken);

            return Task.FromResult(Result<AssetPreviewContentDto>.Success(previewContent));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Result<AssetPreviewContentDto>.Cancelled());
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<AssetPreviewContentDto>.Failure(
                ApplicationErrors.BundleReadFailed(request.ResolvedBundlePath, ex.Message)));
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

    private static AssetPreviewContentDto BuildPreviewContent(
        AssetsManager assetsManager,
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
            BuildTexturePreviewInfo(baseField, textureImage, request.SizeInBytes),
            AssetsToolsAssetDisplayMetadataResolver.ResolvePreferredDisplaySizeInBytes(AssetClassID.Texture2D, baseField),
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

        var spriteImage = TryCropSpriteTexture(baseField, textureImage, out var croppedSpriteImage, out var cropMetadata)
            ? croppedSpriteImage
            : textureImage;

        return TryEncodeTexturePreview(
            spriteImage,
            BuildSpritePreviewInfo(baseField, textureField, spriteImage, pathId.Value, cropMetadata, request.SizeInBytes),
            preferredDisplaySizeInBytes: null,
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
        cropMetadata = new SpriteCropMetadata(
            X: x.Value,
            Y: y.Value,
            Width: width.Value,
            Height: height.Value,
            PixelWidth: cropWidth,
            PixelHeight: cropHeight);
        return true;
    }

    private static bool TryEncodeTexturePreview(
        DecodedTextureImage textureImage,
        string? previewInfoText,
        long? preferredDisplaySizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        try
        {
            previewContent = new AssetPreviewContentDto(
                Kind: AssetPreviewKind.Image,
                ContentType: PngContentType,
                Content: EncodeRgbaPng(textureImage.Width, textureImage.Height, textureImage.RgbaPixels),
                PreviewInfoText: previewInfoText,
                PreferredDisplaySizeInBytes: preferredDisplaySizeInBytes);
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
            PreviewInfoText: null,
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
            normalizedText[..MaxTextPreviewCharacters],
            Environment.NewLine,
            Environment.NewLine,
            "... (preview truncated)");
    }

    private static AssetPreviewContentDto BuildSummaryPreview(
        AssetFileInfo assetInfo,
        AssetTypeValueField baseField,
        AssetClassID classId)
    {
        // 详情页已经统一展示通用身份信息，
        // 摘要预览这里只保留类型专属补充字段，避免和“名称 / 路径 / PathId / 大小”重复。
        var lines = new List<string>();

        switch (classId)
        {
            case AssetClassID.AudioClip:
                AddLine(lines, "加载类型", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_LoadType")));
                AddLine(lines, "声道数", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Channels")));
                AddLine(lines, "采样频率", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Frequency")));
                AddLine(lines, "时长", FormatSeconds(AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_Length"))));
                break;

            case AssetClassID.Mesh:
                AddLine(lines, "子网格数", CountArray(baseField, "m_SubMeshes"));
                AddLine(lines, "BlendShape 数", CountArray(baseField, "m_Shapes", "channels"));
                AddLine(lines, "骨骼名称哈希数", CountArray(baseField, "m_BoneNameHashes"));
                break;

            case AssetClassID.Material:
                AddLine(lines, "纹理属性数", CountArray(baseField, "m_SavedProperties", "m_TexEnvs"));
                AddLine(lines, "浮点属性数", CountArray(baseField, "m_SavedProperties", "m_Floats"));
                AddLine(lines, "颜色属性数", CountArray(baseField, "m_SavedProperties", "m_Colors"));
                break;

            case AssetClassID.GameObject:
                AddLine(lines, "组件数", CountArray(baseField, "m_Component"));
                AddLine(lines, "层级", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Layer")));
                AddLine(lines, "启用状态", FormatBoolean(AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_IsActive"))));
                break;

            case AssetClassID.MonoBehaviour:
                AddLine(lines, "启用状态", FormatBoolean(AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_Enabled"))));
                AddLine(lines, "脚本 PathId", AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_Script", "m_PathID")));
                break;

            case AssetClassID.AnimationClip:
                AddLine(lines, "采样率", FormatFloat(AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_SampleRate"))));
                AddLine(lines, "旧版模式", FormatBoolean(AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(baseField, "m_Legacy"))));
                AddLine(lines, "事件数", CountArray(baseField, "m_Events"));
                break;

            case AssetClassID.AnimatorController:
                AddLine(lines, "层数", CountArray(baseField, "m_AnimatorLayers"));
                AddLine(lines, "参数数", CountArray(baseField, "m_AnimatorParameters"));
                break;

            case AssetClassID.Font:
                AddLine(lines, "行间距", FormatFloat(AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_LineSpacing"))));
                AddLine(lines, "默认字号", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_DefaultSize")));
                AddLine(lines, "ASCII 起始偏移", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_AsciiStartOffset")));
                break;

            case AssetClassID.SpriteAtlas:
                AddLine(lines, "打包精灵数", CountArray(baseField, "m_PackedSprites"));
                break;

            case AssetClassID.Sprite:
                AddLine(lines, "每单位像素", FormatFloat(AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(baseField, "m_PixelsToUnits"))));
                AddLine(lines, "纹理 PathId", AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "texture", "m_PathID")));
                AddLine(lines, "纹理矩形", FormatRect(AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "textureRect")));
                break;

            case AssetClassID.Texture2D:
                AddLine(lines, "宽度", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Width")));
                AddLine(lines, "高度", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Height")));
                AddLine(lines, "Mip 数量", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_MipCount")));
                break;

            default:
                AddLine(lines, "类型 ID", assetInfo.TypeId);
                break;
        }

        return new AssetPreviewContentDto(
            Kind: AssetPreviewKind.Summary,
            ContentType: TextContentType,
            Content: Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines)),
            PreviewInfoText: null,
            PreferredDisplaySizeInBytes: null);
    }

    private static string BuildTexturePreviewInfo(
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

        var lines = new List<string>
        {
            $"图像尺寸: {textureImage.Width} x {textureImage.Height}",
            $"预览像素格式: {PreviewPixelEncoding}",
            $"输出格式: {PreviewContainerEncoding}"
        };

        AddLine(lines, "资源记录大小", FormatByteSize(serializedObjectSizeInBytes));
        AddLine(lines, "纹理格式 ID", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_TextureFormat")));
        AddLine(lines, "Mip 数量", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_MipCount")));
        AddLine(lines, "解码后大小", FormatByteSize(textureImage.RgbaPixels.LongLength));
        AddLine(lines, "原始纹理数据大小", hasStandaloneCompleteSize ? FormatByteSize(completeImageSize) : null);
        AddLine(lines, "流数据大小", hasDedicatedStreamSize ? FormatByteSize(streamSize) : null);
        AddLine(lines, "流数据来源", AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(textureField, "m_StreamData", "path")));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildSpritePreviewInfo(
        AssetTypeValueField spriteField,
        AssetTypeValueField textureField,
        DecodedTextureImage spriteImage,
        long texturePathId,
        SpriteCropMetadata cropMetadata,
        long serializedObjectSizeInBytes)
    {
        var lines = new List<string>
        {
            $"精灵尺寸: {spriteImage.Width} x {spriteImage.Height}",
            $"预览像素格式: {PreviewPixelEncoding}",
            $"输出格式: {PreviewContainerEncoding}"
        };

        AddLine(lines, "资源记录大小", FormatByteSize(serializedObjectSizeInBytes));
        AddLine(lines, "纹理 PathId", texturePathId);
        AddLine(lines, "每单位像素", FormatFloat(AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(spriteField, "m_PixelsToUnits"))));
        AddLine(lines, "纹理矩形", FormatRect(AssetsToolsFieldReader.TryGetField(spriteField, "m_RD", "textureRect")));
        AddLine(lines, "裁切尺寸", cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0
            ? $"{cropMetadata.PixelWidth.ToString(CultureInfo.InvariantCulture)} x {cropMetadata.PixelHeight.ToString(CultureInfo.InvariantCulture)}"
            : null);
        AddLine(lines, "裁切起点", cropMetadata.Width > 0 && cropMetadata.Height > 0
            ? FormattableString.Invariant($"x={cropMetadata.X:0.###}, y={cropMetadata.Y:0.###}")
            : null);
        AddLine(lines, "源纹理尺寸", FormatTextureDimensions(textureField));
        AddLine(lines, "源纹理格式 ID", AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_TextureFormat")));
        return string.Join(Environment.NewLine, lines);
    }

    private static int CountArray(AssetTypeValueField baseField, params string[] path)
    {
        var arrayPath = new string[path.Length + 1];
        path.CopyTo(arrayPath, 0);
        arrayPath[^1] = "Array";
        var arrayField = AssetsToolsFieldReader.TryGetField(baseField, arrayPath);
        return AssetsToolsFieldReader.CountChildren(arrayField);
    }

    private static void AddLine(ICollection<string> lines, string label, object? value)
    {
        if (value is null)
        {
            return;
        }

        var text = value switch
        {
            string stringValue when string.IsNullOrWhiteSpace(stringValue) => null,
            string stringValue => stringValue,
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lines.Add($"{label}: {text}");
    }

    private static string? FormatFloat(float? value)
    {
        return value.HasValue
            ? value.Value.ToString("0.###", CultureInfo.InvariantCulture)
            : null;
    }

    private static string? FormatSeconds(float? value)
    {
        return value.HasValue
            ? $"{value.Value.ToString("0.###", CultureInfo.InvariantCulture)} 秒"
            : null;
    }

    private static string? FormatBoolean(bool? value)
    {
        return value.HasValue ? (value.Value ? "是" : "否") : null;
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

    private static string? FormatTextureDimensions(AssetTypeValueField textureField)
    {
        var width = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_Width"));
        var height = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_Height"));
        if (!width.HasValue || !height.HasValue)
        {
            return null;
        }

        return $"{width.Value.ToString(CultureInfo.InvariantCulture)} x {height.Value.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string? FormatByteSize(long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return null;
        }

        var rawValue = value.Value;
        var compactText = rawValue switch
        {
            >= 1024L * 1024L => $"{rawValue / 1024d / 1024d:F1} MB",
            >= 1024L => $"{rawValue / 1024d:F1} KB",
            _ => $"{rawValue.ToString("N0", CultureInfo.InvariantCulture)} B"
        };

        return rawValue >= 1024L
            ? $"{compactText} ({rawValue.ToString("N0", CultureInfo.InvariantCulture)} 字节)"
            : compactText;
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
        float X,
        float Y,
        float Width,
        float Height,
        int PixelWidth,
        int PixelHeight);
}
