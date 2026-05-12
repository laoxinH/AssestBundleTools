using System.Globalization;
using System.Text;
using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 将 MonoBehaviour 结构化预览投影为可落盘的检查格式。
/// </summary>
internal static class MonoBehaviourAssetExportPayloadBuilder
{
    public static byte[]? BuildPayload(
        AssetPreviewDto preview,
        AssetDetailsDto targetDetails,
        string suggestedOutputPath,
        Func<byte[]?> buildStructuredJsonPayload)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(targetDetails);
        ArgumentNullException.ThrowIfNull(buildStructuredJsonPayload);

        return ResolveExportFormat(suggestedOutputPath) switch
        {
            MonoBehaviourExportFormat.TypeTreeJson => TryBuildTypeTreeJson(preview.MonoBehaviour) ?? buildStructuredJsonPayload(),
            MonoBehaviourExportFormat.SummaryJson => buildStructuredJsonPayload(),
            MonoBehaviourExportFormat.Yaml => TryBuildYaml(preview.MonoBehaviour, targetDetails),
            MonoBehaviourExportFormat.FieldText => TryBuildFieldText(preview.MonoBehaviour, targetDetails),
            _ => TryBuildTypeTreeJson(preview.MonoBehaviour) ?? buildStructuredJsonPayload()
        };
    }

    private static byte[]? TryBuildTypeTreeJson(AssetPreviewMonoBehaviourDto? monoBehaviour)
    {
        return string.IsNullOrWhiteSpace(monoBehaviour?.TypeTreeJsonText)
            ? null
            : Encoding.UTF8.GetBytes(monoBehaviour.TypeTreeJsonText);
    }

    private static byte[]? TryBuildYaml(
        AssetPreviewMonoBehaviourDto? monoBehaviour,
        AssetDetailsDto targetDetails)
    {
        if (monoBehaviour is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("%YAML 1.1");
        builder.AppendLine("---");
        AppendYamlScalar(builder, 0, "schemaVersion", monoBehaviour.SchemaVersion);
        AppendYamlString(builder, 0, "sourceAssetId", targetDetails.AssetId.Value);
        AppendYamlScalar(builder, 0, "sourcePathId", targetDetails.PathId);
        AppendYamlString(builder, 0, "sourceName", targetDetails.Name);
        AppendYamlString(builder, 0, "name", monoBehaviour.Name);
        AppendYamlScalar(builder, 0, "enabled", monoBehaviour.IsEnabled);

        AppendScriptYaml(builder, monoBehaviour.Script);

        builder.AppendLine("summary:");
        AppendYamlScalar(builder, 1, "fieldCount", monoBehaviour.FieldCount);
        AppendYamlScalar(builder, 1, "scalarFieldCount", monoBehaviour.ScalarFieldCount);
        AppendYamlScalar(builder, 1, "referenceCount", monoBehaviour.ReferenceCount);
        AppendYamlScalar(builder, 1, "arrayFieldCount", monoBehaviour.ArrayFieldCount);
        AppendYamlScalar(builder, 1, "fieldsTruncated", monoBehaviour.IsFieldListTruncated);
        AppendYamlScalar(builder, 1, "referencesTruncated", monoBehaviour.IsReferenceListTruncated);

        AppendFieldsYaml(builder, monoBehaviour.Fields);
        AppendReferencesYaml(builder, monoBehaviour.References);
        AppendFieldGroupsYaml(builder, monoBehaviour.FieldGroups);
        AppendContainersYaml(builder, monoBehaviour.Containers);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[]? TryBuildFieldText(
        AssetPreviewMonoBehaviourDto? monoBehaviour,
        AssetDetailsDto targetDetails)
    {
        if (monoBehaviour is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.AppendLine("# AssestBundleTools MonoBehaviour field export");
        builder.AppendLine($"# source-asset-id: {targetDetails.AssetId.Value}");
        builder.AppendLine($"# source-path-id: {targetDetails.PathId}");
        builder.AppendLine($"# source-name: {targetDetails.Name}");
        if (monoBehaviour.IsFieldListTruncated)
        {
            builder.AppendLine("# warning: field list was truncated by the preview projection.");
        }

        if (monoBehaviour.IsReferenceListTruncated)
        {
            builder.AppendLine("# warning: reference list was truncated by the preview projection.");
        }

        builder.AppendLine();
        builder.AppendLine("[Object]");
        builder.AppendLine($"name: {FormatTextValue(monoBehaviour.Name)}");
        builder.AppendLine($"enabled: {FormatTextValue(monoBehaviour.IsEnabled)}");
        builder.AppendLine($"fieldCount: {monoBehaviour.FieldCount}");
        builder.AppendLine($"scalarFieldCount: {monoBehaviour.ScalarFieldCount}");
        builder.AppendLine($"referenceCount: {monoBehaviour.ReferenceCount}");
        builder.AppendLine($"arrayFieldCount: {monoBehaviour.ArrayFieldCount}");

        builder.AppendLine();
        builder.AppendLine("[Script]");
        AppendScriptText(builder, monoBehaviour.Script);

        builder.AppendLine();
        builder.AppendLine("[Fields]");
        if (monoBehaviour.Fields.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var field in monoBehaviour.Fields)
            {
                builder.Append(field.Path);
                builder.Append(" (");
                builder.Append(field.TypeName ?? "unknown");
                builder.Append(") = ");
                builder.AppendLine(FormatFieldValue(field));
            }
        }

        builder.AppendLine();
        builder.AppendLine("[References]");
        if (monoBehaviour.References.Count == 0)
        {
            builder.AppendLine("(none)");
        }
        else
        {
            foreach (var reference in monoBehaviour.References)
            {
                builder.Append(reference.Path);
                builder.Append(" -> ");
                builder.Append(reference.TypeName ?? "unknown");
                if (!string.IsNullOrWhiteSpace(reference.TargetName))
                {
                    builder.Append(' ');
                    builder.Append(reference.TargetName);
                }

                builder.Append(" (fileId=");
                builder.Append(reference.FileId.ToString(CultureInfo.InvariantCulture));
                builder.Append(", pathId=");
                builder.Append(reference.PathId?.ToString(CultureInfo.InvariantCulture) ?? "null");
                builder.AppendLine(")");
            }
        }

        AppendGroupsText(builder, monoBehaviour.FieldGroups);
        AppendContainersText(builder, monoBehaviour.Containers);

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static void AppendScriptYaml(StringBuilder builder, AssetPreviewMonoBehaviourScriptDto? script)
    {
        if (script is null)
        {
            builder.AppendLine("script: null");
            return;
        }

        builder.AppendLine("script:");
        AppendYamlScalar(builder, 1, "fileId", script.FileId);
        AppendYamlScalar(builder, 1, "pathId", script.PathId);
        AppendYamlString(builder, 1, "name", script.Name);
        AppendYamlString(builder, 1, "className", script.ClassName);
        AppendYamlString(builder, 1, "namespace", script.Namespace);
        AppendYamlString(builder, 1, "assemblyName", script.AssemblyName);
        AppendYamlString(builder, 1, "serializedFileName", script.SerializedFileName);
        AppendYamlString(builder, 1, "externalReferencePath", script.ExternalReferencePath);
        AppendYamlScalar(builder, 1, "isExternal", script.IsExternal);
    }

    private static void AppendFieldsYaml(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMonoBehaviourFieldDto> fields)
    {
        if (fields.Count == 0)
        {
            builder.AppendLine("fields: []");
            return;
        }

        builder.AppendLine("fields:");
        foreach (var field in fields)
        {
            AppendYamlListItemHeader(builder, 1);
            AppendYamlString(builder, 2, "path", field.Path);
            AppendYamlString(builder, 2, "name", field.Name);
            AppendYamlString(builder, 2, "typeName", field.TypeName);
            AppendYamlString(builder, 2, "valueKind", field.ValueKind.ToString());
            AppendYamlString(builder, 2, "value", FormatFieldValue(field));
            AppendYamlScalar(builder, 2, "childCount", field.ChildCount);
        }
    }

    private static void AppendReferencesYaml(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMonoBehaviourReferenceDto> references)
    {
        if (references.Count == 0)
        {
            builder.AppendLine("references: []");
            return;
        }

        builder.AppendLine("references:");
        foreach (var reference in references)
        {
            AppendYamlListItemHeader(builder, 1);
            AppendYamlString(builder, 2, "path", reference.Path);
            AppendYamlString(builder, 2, "name", reference.Name);
            AppendYamlScalar(builder, 2, "fileId", reference.FileId);
            AppendYamlScalar(builder, 2, "pathId", reference.PathId);
            AppendYamlString(builder, 2, "typeName", reference.TypeName);
            AppendYamlString(builder, 2, "targetName", reference.TargetName);
            AppendYamlString(builder, 2, "serializedFileName", reference.SerializedFileName);
            AppendYamlString(builder, 2, "externalReferencePath", reference.ExternalReferencePath);
            AppendYamlScalar(builder, 2, "isExternal", reference.IsExternal);
        }
    }

    private static void AppendFieldGroupsYaml(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMonoBehaviourFieldGroupDto>? groups)
    {
        if (groups is not { Count: > 0 })
        {
            builder.AppendLine("fieldGroups: []");
            return;
        }

        builder.AppendLine("fieldGroups:");
        foreach (var group in groups)
        {
            AppendYamlListItemHeader(builder, 1);
            AppendYamlString(builder, 2, "name", group.Name);
            AppendYamlScalar(builder, 2, "fieldCount", group.FieldCount);
            AppendYamlScalar(builder, 2, "scalarFieldCount", group.ScalarFieldCount);
            AppendYamlScalar(builder, 2, "referenceCount", group.ReferenceCount);
            AppendYamlScalar(builder, 2, "arrayFieldCount", group.ArrayFieldCount);
            AppendYamlScalar(builder, 2, "isUnityBuiltin", group.IsUnityBuiltin);
        }
    }

    private static void AppendContainersYaml(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMonoBehaviourContainerDto>? containers)
    {
        if (containers is not { Count: > 0 })
        {
            builder.AppendLine("containers: []");
            return;
        }

        builder.AppendLine("containers:");
        foreach (var container in containers)
        {
            AppendYamlListItemHeader(builder, 1);
            AppendYamlString(builder, 2, "path", container.Path);
            AppendYamlString(builder, 2, "name", container.Name);
            AppendYamlString(builder, 2, "typeName", container.TypeName);
            AppendYamlScalar(builder, 2, "childCount", container.ChildCount);
            AppendYamlScalar(builder, 2, "depth", container.Depth);
        }
    }

    private static void AppendScriptText(StringBuilder builder, AssetPreviewMonoBehaviourScriptDto? script)
    {
        if (script is null)
        {
            builder.AppendLine("(missing)");
            return;
        }

        builder.AppendLine($"fileId: {script.FileId}");
        builder.AppendLine($"pathId: {FormatTextValue(script.PathId)}");
        builder.AppendLine($"name: {FormatTextValue(script.Name)}");
        builder.AppendLine($"className: {FormatTextValue(script.ClassName)}");
        builder.AppendLine($"namespace: {FormatTextValue(script.Namespace)}");
        builder.AppendLine($"assemblyName: {FormatTextValue(script.AssemblyName)}");
        builder.AppendLine($"serializedFileName: {FormatTextValue(script.SerializedFileName)}");
        builder.AppendLine($"externalReferencePath: {FormatTextValue(script.ExternalReferencePath)}");
        builder.AppendLine($"isExternal: {script.IsExternal}");
    }

    private static void AppendGroupsText(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMonoBehaviourFieldGroupDto>? groups)
    {
        builder.AppendLine();
        builder.AppendLine("[Field Groups]");
        if (groups is not { Count: > 0 })
        {
            builder.AppendLine("(none)");
            return;
        }

        foreach (var group in groups)
        {
            builder.AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{group.Name}: fields={group.FieldCount}, scalars={group.ScalarFieldCount}, references={group.ReferenceCount}, arrays={group.ArrayFieldCount}, unityBuiltin={group.IsUnityBuiltin}"));
        }
    }

    private static void AppendContainersText(
        StringBuilder builder,
        IReadOnlyList<AssetPreviewMonoBehaviourContainerDto>? containers)
    {
        builder.AppendLine();
        builder.AppendLine("[Containers]");
        if (containers is not { Count: > 0 })
        {
            builder.AppendLine("(none)");
            return;
        }

        foreach (var container in containers)
        {
            builder.AppendLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{container.Path} ({container.TypeName ?? "unknown"}): childCount={container.ChildCount}, depth={container.Depth}"));
        }
    }

    private static void AppendYamlString(
        StringBuilder builder,
        int indentLevel,
        string key,
        string? value)
    {
        AppendIndent(builder, indentLevel);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(value is null ? "null" : QuoteYamlString(value));
    }

    private static void AppendYamlScalar(
        StringBuilder builder,
        int indentLevel,
        string key,
        int value)
    {
        AppendYamlNumber<int>(builder, indentLevel, key, value);
    }

    private static void AppendYamlScalar(
        StringBuilder builder,
        int indentLevel,
        string key,
        int? value)
    {
        AppendYamlNumber<int>(builder, indentLevel, key, value);
    }

    private static void AppendYamlScalar(
        StringBuilder builder,
        int indentLevel,
        string key,
        long value)
    {
        AppendYamlNumber<long>(builder, indentLevel, key, value);
    }

    private static void AppendYamlScalar(
        StringBuilder builder,
        int indentLevel,
        string key,
        long? value)
    {
        AppendYamlNumber<long>(builder, indentLevel, key, value);
    }

    private static void AppendYamlNumber<T>(
        StringBuilder builder,
        int indentLevel,
        string key,
        T? value)
        where T : struct, IFormattable
    {
        AppendIndent(builder, indentLevel);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(value.HasValue ? value.Value.ToString(null, CultureInfo.InvariantCulture) : "null");
    }

    private static void AppendYamlScalar(
        StringBuilder builder,
        int indentLevel,
        string key,
        bool? value)
    {
        AppendIndent(builder, indentLevel);
        builder.Append(key);
        builder.Append(": ");
        builder.AppendLine(value.HasValue ? value.Value.ToString().ToLowerInvariant() : "null");
    }

    private static void AppendYamlListItemHeader(StringBuilder builder, int indentLevel)
    {
        AppendIndent(builder, indentLevel);
        builder.AppendLine("-");
    }

    private static void AppendIndent(StringBuilder builder, int indentLevel)
    {
        builder.Append(' ', indentLevel * 2);
    }

    private static string QuoteYamlString(string value) =>
        string.Concat("\"", value.Replace("\\", "\\\\").Replace("\"", "\\\""), "\"");

    private static string FormatFieldValue(AssetPreviewMonoBehaviourFieldDto field) =>
        field.ValueKind switch
        {
            AssetPreviewInfoValueKind.Text => FormatTextValue(field.StringValue),
            AssetPreviewInfoValueKind.Boolean => FormatTextValue(field.BooleanValue),
            AssetPreviewInfoValueKind.Decimal => field.DoubleValue?.ToString("0.######", CultureInfo.InvariantCulture) ?? "null",
            AssetPreviewInfoValueKind.Integer or AssetPreviewInfoValueKind.ByteSize => field.Int64Value?.ToString(CultureInfo.InvariantCulture) ??
                field.ChildCount?.ToString(CultureInfo.InvariantCulture) ??
                "null",
            _ => field.StringValue ?? field.Int64Value?.ToString(CultureInfo.InvariantCulture) ?? "null"
        };

    private static string FormatTextValue(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "null"
            : value;

    private static string FormatTextValue(bool? value) =>
        value.HasValue ? value.Value.ToString().ToLowerInvariant() : "null";

    private static string FormatTextValue(long? value) =>
        value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";

    private static MonoBehaviourExportFormat ResolveExportFormat(string suggestedOutputPath)
    {
        var fileName = Path.GetFileName(suggestedOutputPath);
        if (fileName.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return MonoBehaviourExportFormat.Yaml;
        }

        if (fileName.EndsWith(".fields", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".fields.txt", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return MonoBehaviourExportFormat.FieldText;
        }

        if (fileName.EndsWith(".mono-behaviour.json", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".summary.json", StringComparison.OrdinalIgnoreCase))
        {
            return MonoBehaviourExportFormat.SummaryJson;
        }

        return MonoBehaviourExportFormat.TypeTreeJson;
    }

    private enum MonoBehaviourExportFormat
    {
        TypeTreeJson,
        SummaryJson,
        Yaml,
        FieldText
    }
}
