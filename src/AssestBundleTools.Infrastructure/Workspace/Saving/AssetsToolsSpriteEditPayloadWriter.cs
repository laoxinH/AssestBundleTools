using AssetsTools.NET;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal static class AssetsToolsSpriteEditPayloadWriter
{
    public static Result<object> Apply(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem)
    {
        if (writeItem.SpriteEditGeometry is null || writeItem.AssetType != AssetType.Sprite)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.UnsupportedSpriteEdit"));
        }

        var rectFields = ResolveSpriteRectFields(baseField);
        if (rectFields.Count == 0)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.SpriteRectUnavailable"));
        }

        var projection = SpriteEditGeometryWriterProjection.Create(
            writeItem.SpriteEditGeometry,
            useNormalizedPivot: UsesNormalizedSpritePivot(AssetsToolsFieldReader.TryGetField(baseField, "m_Pivot")));
        foreach (var rectField in rectFields)
        {
            SetSpriteRectField(rectField, projection);
        }

        var pivotField = AssetsToolsFieldReader.TryGetField(baseField, "m_Pivot");
        if (pivotField is not null)
        {
            SetSpritePivotField(pivotField, projection);
        }

        return Result<object>.Success(new object());
    }

    private static IReadOnlyList<AssetTypeValueField> ResolveSpriteRectFields(AssetTypeValueField baseField)
    {
        var fields = new List<AssetTypeValueField>();
        var renderDataRectField = AssetsToolsFieldReader.TryGetField(baseField, "m_RD", "textureRect");
        if (renderDataRectField is not null)
        {
            fields.Add(renderDataRectField);
        }

        var spriteRectField = AssetsToolsFieldReader.TryGetField(baseField, "m_Rect");
        if (spriteRectField is not null &&
            !fields.Any(field => ReferenceEquals(field, spriteRectField)))
        {
            fields.Add(spriteRectField);
        }

        return fields;
    }

    private static void SetSpriteRectField(
        AssetTypeValueField rectField,
        SpriteEditGeometryWriterProjectionDto projection)
    {
        SetFloat(rectField, "x", projection.RectX);
        SetFloat(rectField, "y", projection.RectY);
        SetFloat(rectField, "width", projection.RectWidth);
        SetFloat(rectField, "height", projection.RectHeight);
    }

    private static void SetSpritePivotField(
        AssetTypeValueField pivotField,
        SpriteEditGeometryWriterProjectionDto projection)
    {
        SetFloat(pivotField, "x", projection.PivotX);
        SetFloat(pivotField, "y", projection.PivotY);
    }

    private static void SetFloat(
        AssetTypeValueField parent,
        string fieldName,
        double value)
    {
        var field = AssetsToolsFieldReader.TryGetField(parent, fieldName);
        if (field is not null)
        {
            field.AsFloat = (float)value;
        }
    }

    private static bool UsesNormalizedSpritePivot(AssetTypeValueField? pivotField)
    {
        var pivotX = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(pivotField, "x"));
        var pivotY = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(pivotField, "y"));
        return pivotX is >= 0f and <= 1f &&
               pivotY is >= 0f and <= 1f;
    }
}
