using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsSpritePreviewBuilder
{
    public static bool TryBuildSpritePreview(
        AssetsManager assetsManager,
        AssetsFileInstance serializedFile,
        AssetTypeValueField spriteField,
        AssetPreviewBuildRequestDto request,
        out AssetPreviewContentDto previewContent)
    {
        previewContent = null!;

        var texturePointer = AssetsToolsFieldReader.TryGetField(spriteField, "m_RD", "texture");
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
        if (textureField is null ||
            !AssetsToolsImagePreviewSupport.TryDecodeTextureImage(serializedFile, textureField, out var textureImage))
        {
            return false;
        }

        _ = TryCropSpriteTexture(spriteField, textureImage, out var croppedImage, out var cropMetadata);
        var spriteMetadata = BuildSpriteMetadata(spriteField, cropMetadata);

        return AssetsToolsImagePreviewSupport.TryEncodeTexturePreview(
            textureImage,
            BuildPreviewInfoItems(spriteField, textureField, textureImage, pathId.Value, cropMetadata, spriteMetadata, request.SizeInBytes),
            preferredDisplaySizeInBytes: null,
            imageOverlay: BuildImageOverlay(textureImage, cropMetadata, spriteMetadata),
            imageFocus: BuildImageFocus(croppedImage),
            out previewContent);
    }

    private static bool TryCropSpriteTexture(
        AssetTypeValueField spriteField,
        AssetsToolsDecodedTextureImage sourceImage,
        out AssetsToolsDecodedTextureImage croppedImage,
        out SpriteCropMetadata cropMetadata)
    {
        croppedImage = default;
        cropMetadata = default;

        var textureRectField = AssetsToolsFieldReader.TryGetField(spriteField, "m_RD", "textureRect");
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

        croppedImage = new AssetsToolsDecodedTextureImage(cropWidth, cropHeight, croppedPixels);
        var cropYFromTop = Math.Max(0, sourceImage.Height - (cropYFromBottom + cropHeight));

        cropMetadata = new SpriteCropMetadata(
            PixelX: cropX,
            PixelYFromTop: cropYFromTop,
            PixelWidth: cropWidth,
            PixelHeight: cropHeight);
        return true;
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetTypeValueField spriteField,
        AssetTypeValueField textureField,
        AssetsToolsDecodedTextureImage textureImage,
        long texturePathId,
        SpriteCropMetadata cropMetadata,
        SpritePreviewMetadata spriteMetadata,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.ImageDimensions, AssetsToolsImagePreviewSupport.FormatDimensions(textureImage.Width, textureImage.Height));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.PreviewPixelEncoding, AssetsToolsImagePreviewSupport.PixelEncoding);
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.PreviewOutputFormat, AssetsToolsImagePreviewSupport.ContainerEncoding);
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddInteger64Info(items, AssetPreviewInfoKey.SpriteTexturePathId, texturePathId);
        AssetsToolsImagePreviewSupport.AddDecimalInfo(items, AssetPreviewInfoKey.SpritePixelsPerUnit, AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(spriteField, "m_PixelsToUnits")));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.SpriteTextureRect, FormatSpritePreviewRect(cropMetadata, spriteField));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.SpriteCropSize, cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0
            ? AssetsToolsImagePreviewSupport.FormatDimensions(cropMetadata.PixelWidth, cropMetadata.PixelHeight)
            : null);
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.SpriteCropOrigin, cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0
            ? FormattableString.Invariant($"x={cropMetadata.PixelX}, y={cropMetadata.PixelYFromTop}")
            : null);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.SpriteSourceTextureFormatId, AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(textureField, "m_TextureFormat")));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.SpritePivot, FormatPoint(spriteMetadata.PivotPoint));
        AssetsToolsImagePreviewSupport.AddTextInfo(items, AssetPreviewInfoKey.SpriteBorder, FormatBorder(spriteMetadata.NineSliceBorder));
        return items;
    }

    private static AssetPreviewImageOverlayDto? BuildImageOverlay(
        AssetsToolsDecodedTextureImage textureImage,
        SpriteCropMetadata cropMetadata,
        SpritePreviewMetadata spriteMetadata)
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
                Height: cropMetadata.PixelHeight),
            PivotPoint: spriteMetadata.PivotPoint,
            NineSliceBorder: spriteMetadata.NineSliceBorder);
    }

    private static AssetPreviewImageFocusDto? BuildImageFocus(AssetsToolsDecodedTextureImage croppedImage)
    {
        if (croppedImage.Width <= 0 ||
            croppedImage.Height <= 0 ||
            croppedImage.RgbaPixels is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            return new AssetPreviewImageFocusDto(
                ContentType: AssetsToolsImagePreviewSupport.PngContentType,
                BinaryContent: AssetsToolsImagePreviewSupport.EncodeRgbaPng(croppedImage.Width, croppedImage.Height, croppedImage.RgbaPixels),
                RelativePayloadPath: null,
                PixelWidth: croppedImage.Width,
                PixelHeight: croppedImage.Height);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static SpritePreviewMetadata BuildSpriteMetadata(
        AssetTypeValueField spriteField,
        SpriteCropMetadata cropMetadata)
    {
        return new SpritePreviewMetadata(
            PivotPoint: TryReadSpritePivot(spriteField, cropMetadata),
            NineSliceBorder: TryReadSpriteBorder(spriteField, cropMetadata));
    }

    private static AssetPreviewImagePointDto? TryReadSpritePivot(
        AssetTypeValueField spriteField,
        SpriteCropMetadata cropMetadata)
    {
        if (cropMetadata.PixelWidth <= 0 || cropMetadata.PixelHeight <= 0)
        {
            return null;
        }

        var pivotField = AssetsToolsFieldReader.TryGetField(spriteField, "m_Pivot");
        var pivotX = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(pivotField, "x"));
        var pivotY = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(pivotField, "y"));
        if (!pivotX.HasValue || !pivotY.HasValue)
        {
            return null;
        }

        var normalizedPivotX = NormalizePivotCoordinate(pivotX.Value, cropMetadata.PixelWidth);
        var normalizedPivotY = NormalizePivotCoordinate(pivotY.Value, cropMetadata.PixelHeight);
        if (!normalizedPivotX.HasValue || !normalizedPivotY.HasValue)
        {
            return null;
        }

        return new AssetPreviewImagePointDto(
            X: cropMetadata.PixelX + normalizedPivotX.Value,
            Y: cropMetadata.PixelYFromTop + (cropMetadata.PixelHeight - normalizedPivotY.Value));
    }

    private static AssetPreviewImageBorderDto? TryReadSpriteBorder(
        AssetTypeValueField spriteField,
        SpriteCropMetadata cropMetadata)
    {
        if (cropMetadata.PixelWidth <= 0 || cropMetadata.PixelHeight <= 0)
        {
            return null;
        }

        var borderField = AssetsToolsFieldReader.TryGetField(spriteField, "m_Border");
        var left = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(borderField, "x"));
        var bottom = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(borderField, "y"));
        var right = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(borderField, "z"));
        var top = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(borderField, "w"));
        if (!left.HasValue || !bottom.HasValue || !right.HasValue || !top.HasValue)
        {
            return null;
        }

        var normalizedLeft = Math.Clamp(left.Value, 0f, cropMetadata.PixelWidth);
        var normalizedRight = Math.Clamp(right.Value, 0f, cropMetadata.PixelWidth);
        var normalizedTop = Math.Clamp(top.Value, 0f, cropMetadata.PixelHeight);
        var normalizedBottom = Math.Clamp(bottom.Value, 0f, cropMetadata.PixelHeight);
        if (normalizedLeft <= 0f &&
            normalizedRight <= 0f &&
            normalizedTop <= 0f &&
            normalizedBottom <= 0f)
        {
            return null;
        }

        return new AssetPreviewImageBorderDto(
            Left: normalizedLeft,
            Top: normalizedTop,
            Right: normalizedRight,
            Bottom: normalizedBottom);
    }

    private static double? NormalizePivotCoordinate(float value, int pixelSize)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return null;
        }

        var normalizedValue = value is >= 0f and <= 1f
            ? value * pixelSize
            : value;
        return Math.Clamp(normalizedValue, 0d, pixelSize);
    }

    private static string? FormatPoint(AssetPreviewImagePointDto? point)
    {
        return point is null
            ? null
            : FormattableString.Invariant($"x={point.X:0.###}, y={point.Y:0.###}");
    }

    private static string? FormatBorder(AssetPreviewImageBorderDto? border)
    {
        return border is null
            ? null
            : FormattableString.Invariant(
                $"left={border.Left:0.###}, top={border.Top:0.###}, right={border.Right:0.###}, bottom={border.Bottom:0.###}");
    }

    private static string? FormatSpritePreviewRect(
        SpriteCropMetadata cropMetadata,
        AssetTypeValueField spriteField)
    {
        if (cropMetadata.PixelWidth > 0 && cropMetadata.PixelHeight > 0)
        {
            return FormattableString.Invariant(
                $"x={cropMetadata.PixelX}, y={cropMetadata.PixelYFromTop}, w={cropMetadata.PixelWidth}, h={cropMetadata.PixelHeight}");
        }

        return AssetsToolsImagePreviewSupport.FormatRect(AssetsToolsFieldReader.TryGetField(spriteField, "m_RD", "textureRect"));
    }

    private readonly record struct SpriteCropMetadata(
        int PixelX,
        int PixelYFromTop,
        int PixelWidth,
        int PixelHeight);

    private readonly record struct SpritePreviewMetadata(
        AssetPreviewImagePointDto? PivotPoint,
        AssetPreviewImageBorderDto? NineSliceBorder);
}
