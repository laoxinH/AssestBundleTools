using System.Globalization;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsMonoBehaviourFieldScanner
{
    private const int MaxFieldCount = 32;
    private const int MaxReferenceCount = 32;
    private const int MaxFieldGroupCount = 16;
    private const int MaxContainerCount = 24;
    private const int MaxTraversalFieldCount = 2048;

    public static MonoBehaviourFieldScanResult ScanFields(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField monoBehaviourField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var fields = new List<AssetPreviewMonoBehaviourFieldDto>();
        var references = new List<AssetPreviewMonoBehaviourReferenceDto>();
        var scalarFieldCount = 0;
        var referenceCount = 0;
        var arrayFieldCount = 0;
        var visitedFieldCount = 0;
        var fieldGroups = new Dictionary<string, MonoBehaviourFieldGroupBuilder>(StringComparer.OrdinalIgnoreCase);
        var containers = new List<AssetPreviewMonoBehaviourContainerDto>();
        var pendingFields = new Stack<MonoBehaviourFieldReadItem>();

        if (monoBehaviourField.Children is { Count: > 0 } children)
        {
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new MonoBehaviourFieldReadItem(children[index], string.Empty, index));
            }
        }

        while (pendingFields.Count > 0 && visitedFieldCount < MaxTraversalFieldCount)
        {
            var item = pendingFields.Pop();
            visitedFieldCount++;
            var fieldName = ResolveFieldName(item.Field, item.ParentPath, item.Index);
            var fieldPath = string.IsNullOrWhiteSpace(item.ParentPath)
                ? fieldName
                : string.Concat(item.ParentPath, ".", fieldName);
            var rootFieldName = ResolveRootFieldName(fieldPath);
            var fieldGroup = GetOrCreateFieldGroup(fieldGroups, rootFieldName);
            fieldGroup.FieldCount++;

            if (TryReadReference(
                    assetsManager,
                    serializedFiles,
                    currentSerializedFile,
                    item.Field,
                    fieldPath,
                    fieldName,
                    internalSerializedFileLookup,
                    out var reference))
            {
                referenceCount++;
                fieldGroup.ReferenceCount++;
                if (references.Count < MaxReferenceCount)
                {
                    references.Add(reference);
                }

                continue;
            }

            if (TryGetArrayChildren(item.Field, out var arrayChildren))
            {
                arrayFieldCount++;
                fieldGroup.ArrayFieldCount++;
                if (fields.Count < MaxFieldCount)
                {
                    fields.Add(new AssetPreviewMonoBehaviourFieldDto(
                        Path: fieldPath,
                        Name: fieldName,
                        TypeName: item.Field.TypeName,
                        ValueKind: AssetPreviewInfoValueKind.Integer,
                        ChildCount: arrayChildren.Count));
                }

                if (containers.Count < MaxContainerCount)
                {
                    containers.Add(new AssetPreviewMonoBehaviourContainerDto(
                        Path: fieldPath,
                        Name: fieldName,
                        TypeName: item.Field.TypeName,
                        ChildCount: arrayChildren.Count,
                        Depth: CountPathDepth(fieldPath)));
                }
            }
            else if (TryReadScalarField(item.Field, fieldPath, fieldName, out var scalarField))
            {
                scalarFieldCount++;
                fieldGroup.ScalarFieldCount++;
                if (fields.Count < MaxFieldCount)
                {
                    fields.Add(scalarField);
                }
            }

            if (item.Field.Children is not { Count: > 0 } childFields)
            {
                continue;
            }

            for (var index = childFields.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new MonoBehaviourFieldReadItem(childFields[index], fieldPath, index));
            }
        }

        var isTraversalTruncated = pendingFields.Count > 0;
        return new MonoBehaviourFieldScanResult(
            FieldCount: visitedFieldCount,
            ScalarFieldCount: scalarFieldCount,
            ReferenceCount: referenceCount,
            ArrayFieldCount: arrayFieldCount,
            Fields: fields.ToArray(),
            References: references.ToArray(),
            FieldGroups: fieldGroups.Values
                .Select(static group => group.Build())
                .OrderBy(static group => group.IsUnityBuiltin)
                .ThenByDescending(static group => group.FieldCount)
                .ThenBy(static group => group.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxFieldGroupCount)
                .ToArray(),
            Containers: containers
                .OrderByDescending(static container => container.ChildCount)
                .ThenBy(static container => container.Depth)
                .ThenBy(static container => container.Path, StringComparer.OrdinalIgnoreCase)
                .Take(MaxContainerCount)
                .ToArray(),
            IsFieldListTruncated: isTraversalTruncated || scalarFieldCount + arrayFieldCount > fields.Count,
            IsReferenceListTruncated: isTraversalTruncated || referenceCount > references.Count);
    }

    public static bool TryResolveSerializedFileReference(
        AssetsFileInstance currentSerializedFile,
        long fileId,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out AssetsToolsSerializedFileReference targetReference)
    {
        try
        {
            return AssetsToolsBundleDependencyResolver.TryResolveTargetSerializedFileReference(
                currentSerializedFile,
                fileId,
                internalSerializedFileLookup,
                out targetReference);
        }
        catch (OverflowException)
        {
            targetReference = default;
            return false;
        }
    }

    private static bool TryReadReference(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField field,
        string fieldPath,
        string fieldName,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out AssetPreviewMonoBehaviourReferenceDto reference)
    {
        reference = null!;
        if (!IsAssetPointerField(field))
        {
            return false;
        }

        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(field, "m_FileID")) ?? 0;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(field, "m_PathID"));
        if (pathId is null || pathId.Value == 0)
        {
            return false;
        }

        var targetReference = TryResolveSerializedFileReference(
            currentSerializedFile,
            fileId,
            internalSerializedFileLookup,
            out var resolvedReference)
            ? resolvedReference
            : new AssetsToolsSerializedFileReference(
                currentSerializedFile.name,
                ExternalReferencePath: null,
                IsExternal: false);
        var targetAsset = TryResolveAssetSummary(
            assetsManager,
            serializedFiles,
            targetReference.SerializedFileName,
            pathId.Value);

        reference = new AssetPreviewMonoBehaviourReferenceDto(
            Path: fieldPath,
            Name: fieldName,
            FileId: fileId,
            PathId: pathId.Value,
            TypeName: targetAsset?.TypeName,
            TargetName: targetAsset?.Name,
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal);
        return true;
    }

    private static bool TryReadScalarField(
        AssetTypeValueField field,
        string fieldPath,
        string fieldName,
        out AssetPreviewMonoBehaviourFieldDto scalarField)
    {
        scalarField = null!;
        if (field.Children is { Count: > 0 })
        {
            return false;
        }

        if (IsStringType(field.TypeName))
        {
            var textValue = AssetsToolsFieldReader.ReadString(field);
            if (!string.IsNullOrWhiteSpace(textValue))
            {
                scalarField = CreateTextField(field, fieldPath, fieldName, textValue);
                return true;
            }
        }

        if (IsBooleanType(field.TypeName))
        {
            var booleanValue = AssetsToolsFieldReader.ReadBoolean(field);
            if (booleanValue.HasValue)
            {
                scalarField = new AssetPreviewMonoBehaviourFieldDto(
                    fieldPath,
                    fieldName,
                    field.TypeName,
                    AssetPreviewInfoValueKind.Boolean,
                    BooleanValue: booleanValue.Value);
                return true;
            }
        }

        if (IsFloatType(field.TypeName))
        {
            var doubleValue = AssetsToolsFieldReader.ReadFloat(field);
            if (doubleValue.HasValue)
            {
                scalarField = new AssetPreviewMonoBehaviourFieldDto(
                    fieldPath,
                    fieldName,
                    field.TypeName,
                    AssetPreviewInfoValueKind.Decimal,
                    DoubleValue: doubleValue.Value);
                return true;
            }
        }

        if (IsIntegerType(field.TypeName))
        {
            var integerValue = AssetsToolsFieldReader.ReadInt64(field);
            if (integerValue.HasValue)
            {
                scalarField = new AssetPreviewMonoBehaviourFieldDto(
                    fieldPath,
                    fieldName,
                    field.TypeName,
                    AssetPreviewInfoValueKind.Integer,
                    Int64Value: integerValue.Value);
                return true;
            }
        }

        return false;
    }

    private static AssetPreviewMonoBehaviourFieldDto CreateTextField(
        AssetTypeValueField field,
        string fieldPath,
        string fieldName,
        string textValue)
    {
        var value = textValue.Length > 120
            ? string.Concat(textValue.AsSpan(0, 120).ToString(), "...")
            : textValue;
        return new AssetPreviewMonoBehaviourFieldDto(
            fieldPath,
            fieldName,
            field.TypeName,
            AssetPreviewInfoValueKind.Text,
            StringValue: value);
    }

    private static MonoBehaviourResolvedAssetSummary? TryResolveAssetSummary(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        string? serializedFileName,
        long pathId)
    {
        var targetSerializedFile = serializedFiles.FirstOrDefault(file =>
            string.Equals(file.name, serializedFileName, StringComparison.OrdinalIgnoreCase));
        if (targetSerializedFile is null ||
            !AssetsToolsFieldReader.TryFindAssetInfoByPathId(targetSerializedFile, pathId, out var assetInfo))
        {
            return null;
        }

        var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, targetSerializedFile, assetInfo);
        var name = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_Name")) ??
                   AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(baseField, "m_ClassName"));
        return new MonoBehaviourResolvedAssetSummary(
            ResolveTypeName(assetInfo.TypeId),
            name);
    }

    private static bool TryGetArrayChildren(
        AssetTypeValueField field,
        out IReadOnlyList<AssetTypeValueField> children)
    {
        var arrayField = AssetsToolsFieldReader.TryGetField(field, "Array");
        if (arrayField?.Children is { Count: > 0 } arrayChildren)
        {
            children = arrayChildren;
            return true;
        }

        children = Array.Empty<AssetTypeValueField>();
        return false;
    }

    private static string ResolveFieldName(
        AssetTypeValueField field,
        string parentPath,
        int index)
    {
        var templateName = field.TemplateField?.Name;
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            var fieldName = templateName.Trim();
            return string.Equals(fieldName, "data", StringComparison.OrdinalIgnoreCase) &&
                   parentPath.EndsWith(".Array", StringComparison.OrdinalIgnoreCase)
                ? string.Format(CultureInfo.InvariantCulture, "{0}[{1}]", fieldName, index)
                : fieldName;
        }

        return string.Format(CultureInfo.InvariantCulture, "#{0}", index);
    }

    private static MonoBehaviourFieldGroupBuilder GetOrCreateFieldGroup(
        IDictionary<string, MonoBehaviourFieldGroupBuilder> groups,
        string name)
    {
        if (groups.TryGetValue(name, out var group))
        {
            return group;
        }

        group = new MonoBehaviourFieldGroupBuilder(name, IsUnityBuiltinRootFieldName(name));
        groups[name] = group;
        return group;
    }

    private static string ResolveRootFieldName(string fieldPath)
    {
        var separatorIndex = fieldPath.IndexOf('.');
        return separatorIndex > 0
            ? fieldPath[..separatorIndex]
            : fieldPath;
    }

    private static int CountPathDepth(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return 0;
        }

        return fieldPath.Count(static character => character == '.') + 1;
    }

    private static bool IsUnityBuiltinRootFieldName(string name)
    {
        return IsType(name, "m_GameObject") ||
               IsType(name, "m_Enabled") ||
               IsType(name, "m_Script") ||
               IsType(name, "m_Name") ||
               IsType(name, "m_EditorHideFlags") ||
               IsType(name, "m_CorrespondingSourceObject") ||
               IsType(name, "m_PrefabInstance") ||
               IsType(name, "m_PrefabAsset");
    }

    private static string? ResolveTypeName(int? rawTypeId)
    {
        if (!rawTypeId.HasValue)
        {
            return null;
        }

        return Enum.IsDefined(typeof(AssetClassID), rawTypeId.Value)
            ? ((AssetClassID)rawTypeId.Value).ToString()
            : null;
    }

    private static bool IsAssetPointerField(AssetTypeValueField field)
    {
        return field.TypeName.StartsWith("PPtr<", StringComparison.OrdinalIgnoreCase) ||
               (field.Children?.Count == 2 &&
                AssetsToolsFieldReader.TryGetField(field, "m_FileID") is not null &&
                AssetsToolsFieldReader.TryGetField(field, "m_PathID") is not null);
    }

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

    private sealed record MonoBehaviourFieldReadItem(
        AssetTypeValueField Field,
        string ParentPath,
        int Index);

    private sealed record MonoBehaviourResolvedAssetSummary(string? TypeName, string? Name);

    private sealed class MonoBehaviourFieldGroupBuilder
    {
        public MonoBehaviourFieldGroupBuilder(string name, bool isUnityBuiltin)
        {
            Name = name;
            IsUnityBuiltin = isUnityBuiltin;
        }

        public string Name { get; }

        public bool IsUnityBuiltin { get; }

        public int FieldCount { get; set; }

        public int ScalarFieldCount { get; set; }

        public int ReferenceCount { get; set; }

        public int ArrayFieldCount { get; set; }

        public AssetPreviewMonoBehaviourFieldGroupDto Build() =>
            new(
                Name,
                FieldCount,
                ScalarFieldCount,
                ReferenceCount,
                ArrayFieldCount,
                IsUnityBuiltin);
    }
}

internal sealed record MonoBehaviourFieldScanResult(
    int FieldCount,
    int ScalarFieldCount,
    int ReferenceCount,
    int ArrayFieldCount,
    IReadOnlyList<AssetPreviewMonoBehaviourFieldDto> Fields,
    IReadOnlyList<AssetPreviewMonoBehaviourReferenceDto> References,
    IReadOnlyList<AssetPreviewMonoBehaviourFieldGroupDto> FieldGroups,
    IReadOnlyList<AssetPreviewMonoBehaviourContainerDto> Containers,
    bool IsFieldListTruncated,
    bool IsReferenceListTruncated);
