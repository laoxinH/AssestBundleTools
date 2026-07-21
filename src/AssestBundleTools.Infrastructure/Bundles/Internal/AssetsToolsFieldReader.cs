using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 封装 AssetsTools.NET 读取字段时常用的安全访问逻辑。
/// </summary>
internal static class AssetsToolsFieldReader
{
    public static AssetTypeValueField? TryGetBaseField(
        AssetsManager assetsManager,
        AssetsFileInstance serializedFile,
        AssetFileInfo assetInfo)
    {
        try
        {
            return assetsManager.GetBaseField(serializedFile, assetInfo, AssetReadFlags.None);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static AssetTypeValueField? TryGetField(AssetTypeValueField? field, params string[] path)
    {
        var current = field;
        foreach (var segment in path)
        {
            if (current is null || current.IsDummy)
            {
                return null;
            }

            try
            {
                current = current[segment];
            }
            catch (Exception)
            {
                return null;
            }
        }

        return current is null || current.IsDummy
            ? null
            : current;
    }

    public static string? ReadString(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            var value = field.AsString;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static byte[]? ReadByteArray(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            var value = field.AsByteArray;
            return value is { Length: > 0 } ? value : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static int? ReadInt32(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.AsInt;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static long? ReadByteArrayLength(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            var value = field.AsByteArray;
            return value is { Length: > 0 } ? value.LongLength : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static uint? ReadUInt32(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.AsUInt;
        }
        catch (Exception)
        {
            try
            {
                var value = field.AsInt;
                return value < 0 ? null : (uint)value;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static byte? ReadByte(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.AsByte;
        }
        catch (Exception)
        {
            try
            {
                var value = field.AsInt;
                return value is < byte.MinValue or > byte.MaxValue
                    ? null
                    : (byte)value;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static long? ReadInt64(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.AsLong;
        }
        catch (Exception)
        {
            try
            {
                return field.AsInt;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static float? ReadFloat(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.AsFloat;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool? ReadBoolean(AssetTypeValueField? field)
    {
        if (field is null)
        {
            return null;
        }

        try
        {
            return field.AsBool;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static int CountChildren(AssetTypeValueField? field)
    {
        return field?.Children?.Count ?? 0;
    }

    public static bool TryFindAssetInfoByPathId(
        AssetsFileInstance serializedFile,
        long pathId,
        out AssetFileInfo assetInfo)
    {
        foreach (var candidate in serializedFile.file.AssetInfos)
        {
            if (candidate.PathId == pathId)
            {
                assetInfo = candidate;
                return true;
            }
        }

        assetInfo = default!;
        return false;
    }
}
