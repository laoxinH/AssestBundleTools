using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AssetsTools.NET;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsTypeTreeJsonBuilder
{
    private const int MaxNodeCount = 50000;
    private const int MaxDepth = 96;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = true
    };

    public static TypeTreeJsonBuildResult Build(AssetTypeValueField rootField)
    {
        ArgumentNullException.ThrowIfNull(rootField);

        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, WriterOptions))
        {
            var context = new TypeTreeJsonWriteContext();
            WriteObjectChildren(writer, rootField.Children, context, depth: 0);
            writer.Flush();

            return new TypeTreeJsonBuildResult(
                Encoding.UTF8.GetString(output.ToArray()),
                context.NodeCount,
                context.IsTruncated);
        }
    }

    private static void WriteFieldValue(
        Utf8JsonWriter writer,
        AssetTypeValueField field,
        TypeTreeJsonWriteContext context,
        int depth)
    {
        if (!context.TryVisitNode() || depth > MaxDepth)
        {
            context.MarkTruncated();
            writer.WriteNullValue();
            return;
        }

        if (IsAssetPointerField(field))
        {
            WritePointerObject(writer, field);
            return;
        }

        if (TryGetArrayChildren(field, out var arrayChildren))
        {
            writer.WriteStartArray();
            foreach (var child in arrayChildren)
            {
                WriteFieldValue(writer, child, context, depth + 1);
            }

            writer.WriteEndArray();
            return;
        }

        if (field.Children is { Count: > 0 } children)
        {
            WriteObjectChildren(writer, children, context, depth + 1);
            return;
        }

        WriteScalarValue(writer, field);
    }

    private static void WriteObjectChildren(
        Utf8JsonWriter writer,
        IReadOnlyList<AssetTypeValueField>? children,
        TypeTreeJsonWriteContext context,
        int depth)
    {
        writer.WriteStartObject();

        if (children is { Count: > 0 })
        {
            var propertyNames = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                var propertyName = ResolveUniquePropertyName(ResolveFieldName(child, index), propertyNames);
                writer.WritePropertyName(propertyName);
                WriteFieldValue(writer, child, context, depth + 1);
            }
        }

        writer.WriteEndObject();
    }

    private static void WritePointerObject(Utf8JsonWriter writer, AssetTypeValueField field)
    {
        writer.WriteStartObject();
        writer.WriteNumber(
            "m_FileID",
            AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(field, "m_FileID")) ?? 0);
        WriteNullableNumber(
            writer,
            "m_PathID",
            AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(field, "m_PathID")));
        writer.WriteEndObject();
    }

    private static void WriteScalarValue(Utf8JsonWriter writer, AssetTypeValueField field)
    {
        if (IsStringType(field.TypeName))
        {
            var stringValue = AssetsToolsFieldReader.ReadString(field);
            if (stringValue is not null)
            {
                writer.WriteStringValue(stringValue);
                return;
            }

            var bytes = AssetsToolsFieldReader.ReadByteArray(field);
            if (bytes is { Length: > 0 })
            {
                writer.WriteStringValue(Convert.ToBase64String(bytes));
                return;
            }
        }

        if (IsBooleanType(field.TypeName))
        {
            var booleanValue = AssetsToolsFieldReader.ReadBoolean(field);
            if (booleanValue.HasValue)
            {
                writer.WriteBooleanValue(booleanValue.Value);
                return;
            }
        }

        if (IsFloatType(field.TypeName))
        {
            var doubleValue = AssetsToolsFieldReader.ReadFloat(field);
            if (doubleValue.HasValue && double.IsFinite(doubleValue.Value))
            {
                writer.WriteNumberValue(doubleValue.Value);
                return;
            }
        }

        if (IsIntegerType(field.TypeName))
        {
            var integerValue = AssetsToolsFieldReader.ReadInt64(field);
            if (integerValue.HasValue)
            {
                writer.WriteNumberValue(integerValue.Value);
                return;
            }
        }

        writer.WriteNullValue();
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string propertyName, long? value)
    {
        if (value.HasValue)
        {
            writer.WriteNumber(propertyName, value.Value);
            return;
        }

        writer.WriteNull(propertyName);
    }

    private static bool TryGetArrayChildren(
        AssetTypeValueField field,
        out IReadOnlyList<AssetTypeValueField> children)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(field, "Array");
        if (arrayField?.Children is { Count: > 0 } arrayChildren)
        {
            children = arrayChildren
                .Where(static child => !IsFieldNamed(child, "size"))
                .ToArray();
            return true;
        }

        children = Array.Empty<AssetTypeValueField>();
        return false;
    }

    private static string ResolveFieldName(AssetTypeValueField field, int index)
    {
        var templateName = field.TemplateField?.Name;
        return string.IsNullOrWhiteSpace(templateName)
            ? string.Format(CultureInfo.InvariantCulture, "#{0}", index)
            : templateName.Trim();
    }

    private static string ResolveUniquePropertyName(
        string propertyName,
        IDictionary<string, int> propertyNames)
    {
        if (!propertyNames.TryGetValue(propertyName, out var count))
        {
            propertyNames[propertyName] = 1;
            return propertyName;
        }

        count++;
        propertyNames[propertyName] = count;
        return string.Create(CultureInfo.InvariantCulture, $"{propertyName}#{count}");
    }

    private static bool IsAssetPointerField(AssetTypeValueField field)
    {
        return field.TypeName?.StartsWith("PPtr<", StringComparison.OrdinalIgnoreCase) == true ||
               (field.Children?.Count == 2 &&
                AssetsToolsFieldReader.TryGetField(field, "m_FileID") is not null &&
                AssetsToolsFieldReader.TryGetField(field, "m_PathID") is not null);
    }

    private static bool IsFieldNamed(AssetTypeValueField field, string expectedName) =>
        string.Equals(field.TemplateField?.Name, expectedName, StringComparison.OrdinalIgnoreCase);

    private static bool IsStringType(string? typeName) =>
        IsType(typeName, "string") ||
        IsType(typeName, "TypelessData");

    private static bool IsBooleanType(string? typeName) =>
        IsType(typeName, "bool");

    private static bool IsFloatType(string? typeName) =>
        IsType(typeName, "float") ||
        IsType(typeName, "double");

    private static bool IsIntegerType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        return typeName.Equals("int", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("uint", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt8", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt8", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt16", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt16", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt32", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt32", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("SInt64", StringComparison.OrdinalIgnoreCase) ||
               typeName.Equals("UInt64", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsType(string? typeName, string expectedTypeName) =>
        string.Equals(typeName, expectedTypeName, StringComparison.OrdinalIgnoreCase);

    private sealed class TypeTreeJsonWriteContext
    {
        public int NodeCount { get; private set; }

        public bool IsTruncated { get; private set; }

        public bool TryVisitNode()
        {
            if (NodeCount >= MaxNodeCount)
            {
                IsTruncated = true;
                return false;
            }

            NodeCount++;
            return true;
        }

        public void MarkTruncated()
        {
            IsTruncated = true;
        }
    }
}

internal sealed record TypeTreeJsonBuildResult(
    string JsonText,
    int NodeCount,
    bool IsTruncated);
