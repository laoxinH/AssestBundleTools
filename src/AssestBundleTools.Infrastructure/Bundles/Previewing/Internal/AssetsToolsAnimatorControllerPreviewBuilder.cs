using System.Globalization;
using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsAnimatorControllerPreviewBuilder
{
    private const string AnimatorControllerPreviewContentType = "application/vnd.assestbundletools.animator-controller-preview+json";
    private const int AnimatorControllerPreviewSchemaVersion = 1;
    private const int MaxLayerCount = 24;
    private const int MaxParameterCount = 48;
    private const int MaxReferenceCount = 48;
    private const int MaxContainerCount = 32;
    private const int MaxTraversalFieldCount = 4096;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildAnimatorControllerPreview(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField animatorControllerField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(assetsManager);
        ArgumentNullException.ThrowIfNull(serializedFiles);
        ArgumentNullException.ThrowIfNull(currentSerializedFile);
        ArgumentNullException.ThrowIfNull(animatorControllerField);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        var layerArray = AssetsToolsFieldReader.TryGetField(animatorControllerField, "m_AnimatorLayers", "Array");
        var parameterArray = AssetsToolsFieldReader.TryGetField(animatorControllerField, "m_AnimatorParameters", "Array");
        var fieldScan = ScanFields(
            assetsManager,
            serializedFiles,
            currentSerializedFile,
            animatorControllerField,
            internalSerializedFileLookup);
        var animatorController = new AssetPreviewAnimatorControllerDto(
            SchemaVersion: AnimatorControllerPreviewSchemaVersion,
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(animatorControllerField, "m_Name")),
            LayerCount: AssetsToolsFieldReader.CountChildren(layerArray),
            ParameterCount: AssetsToolsFieldReader.CountChildren(parameterArray),
            StateMachineCount: fieldScan.StateMachineCount,
            StateCount: fieldScan.StateCount,
            TransitionCount: fieldScan.TransitionCount + fieldScan.AnyStateTransitionCount,
            AnyStateTransitionCount: fieldScan.AnyStateTransitionCount,
            ReferenceCount: fieldScan.ReferenceCount,
            Layers: ReadLayers(layerArray),
            Parameters: ReadParameters(parameterArray),
            References: fieldScan.References,
            Containers: fieldScan.Containers,
            IsLayerListTruncated: AssetsToolsFieldReader.CountChildren(layerArray) > MaxLayerCount,
            IsParameterListTruncated: AssetsToolsFieldReader.CountChildren(parameterArray) > MaxParameterCount,
            IsReferenceListTruncated: fieldScan.IsReferenceListTruncated,
            IsContainerListTruncated: fieldScan.IsContainerListTruncated);

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.AnimatorController,
            ContentType: AnimatorControllerPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(animatorController, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(animatorController, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static IReadOnlyList<AssetPreviewAnimatorControllerLayerDto> ReadLayers(
        AssetTypeValueField? layerArray)
    {
        if (layerArray?.Children is not { Count: > 0 } children)
        {
            return Array.Empty<AssetPreviewAnimatorControllerLayerDto>();
        }

        var layers = new List<AssetPreviewAnimatorControllerLayerDto>(Math.Min(children.Count, MaxLayerCount));
        for (var index = 0; index < children.Count && layers.Count < MaxLayerCount; index++)
        {
            var layerField = children[index];
            var stateMachineField = AssetsToolsFieldReader.TryGetField(layerField, "m_StateMachine");
            var avatarMaskField = AssetsToolsFieldReader.TryGetField(layerField, "m_AvatarMask");
            layers.Add(new AssetPreviewAnimatorControllerLayerDto(
                Index: index,
                Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(layerField, "m_Name")),
                DefaultWeight: AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(layerField, "m_DefaultWeight")),
                BlendingMode: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(layerField, "m_BlendingMode")),
                SyncedLayerIndex: AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(layerField, "m_SyncedLayerIndex")),
                IkPass: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(layerField, "m_IKPass")),
                StateMachinePathId: ReadPathId(stateMachineField),
                AvatarMaskPathId: ReadPathId(avatarMaskField),
                StateCount: CountKnownContainers(layerField, AssetPreviewAnimatorControllerContainerKind.State),
                TransitionCount: CountKnownContainers(layerField, AssetPreviewAnimatorControllerContainerKind.Transition)));
        }

        return layers;
    }

    private static IReadOnlyList<AssetPreviewAnimatorControllerParameterDto> ReadParameters(
        AssetTypeValueField? parameterArray)
    {
        if (parameterArray?.Children is not { Count: > 0 } children)
        {
            return Array.Empty<AssetPreviewAnimatorControllerParameterDto>();
        }

        var parameters = new List<AssetPreviewAnimatorControllerParameterDto>(Math.Min(children.Count, MaxParameterCount));
        for (var index = 0; index < children.Count && parameters.Count < MaxParameterCount; index++)
        {
            var parameterField = children[index];
            var parameterType = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(parameterField, "m_Type"));
            parameters.Add(new AssetPreviewAnimatorControllerParameterDto(
                Index: index,
                Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(parameterField, "m_Name")),
                Type: parameterType,
                TypeName: ResolveParameterTypeName(parameterType),
                DefaultFloat: AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(parameterField, "m_DefaultFloat")),
                DefaultInt: AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(parameterField, "m_DefaultInt")),
                DefaultBool: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(parameterField, "m_DefaultBool"))));
        }

        return parameters;
    }

    private static AnimatorControllerFieldScanResult ScanFields(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField animatorControllerField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var references = new List<AssetPreviewAnimatorControllerReferenceDto>();
        var containers = new List<AssetPreviewAnimatorControllerContainerDto>();
        var referenceCount = 0;
        var stateMachineCount = 0;
        var stateCount = 0;
        var transitionCount = 0;
        var anyStateTransitionCount = 0;
        var visitedFieldCount = 0;
        var pendingFields = new Stack<AnimatorControllerFieldReadItem>();

        if (animatorControllerField.Children is { Count: > 0 } children)
        {
            for (var index = children.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new AnimatorControllerFieldReadItem(children[index], string.Empty, index));
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
                if (references.Count < MaxReferenceCount)
                {
                    references.Add(reference);
                }
            }

            if (TryGetArrayChildren(item.Field, out var arrayChildren))
            {
                var containerKind = ResolveContainerKind(fieldPath, fieldName);
                switch (containerKind)
                {
                    case AssetPreviewAnimatorControllerContainerKind.StateMachine:
                        stateMachineCount += arrayChildren.Count;
                        break;
                    case AssetPreviewAnimatorControllerContainerKind.State:
                        stateCount += arrayChildren.Count;
                        break;
                    case AssetPreviewAnimatorControllerContainerKind.Transition:
                        transitionCount += arrayChildren.Count;
                        break;
                    case AssetPreviewAnimatorControllerContainerKind.AnyStateTransition:
                        anyStateTransitionCount += arrayChildren.Count;
                        break;
                }

                if (containerKind != AssetPreviewAnimatorControllerContainerKind.Unknown &&
                    containers.Count < MaxContainerCount)
                {
                    containers.Add(new AssetPreviewAnimatorControllerContainerDto(
                        Path: fieldPath,
                        Name: fieldName,
                        Kind: containerKind,
                        TypeName: item.Field.TypeName,
                        ChildCount: arrayChildren.Count,
                        Depth: CountPathDepth(fieldPath)));
                }
            }

            if (item.Field.Children is not { Count: > 0 } childFields)
            {
                continue;
            }

            for (var index = childFields.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new AnimatorControllerFieldReadItem(childFields[index], fieldPath, index));
            }
        }

        var isTraversalTruncated = pendingFields.Count > 0;
        return new AnimatorControllerFieldScanResult(
            StateMachineCount: stateMachineCount,
            StateCount: stateCount,
            TransitionCount: transitionCount,
            AnyStateTransitionCount: anyStateTransitionCount,
            ReferenceCount: referenceCount,
            References: references
                .DistinctBy(static reference => $"{reference.Path}|{reference.FileId}|{reference.PathId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty}")
                .ToArray(),
            Containers: containers
                .OrderBy(static container => container.Depth)
                .ThenBy(static container => container.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            IsReferenceListTruncated: isTraversalTruncated || referenceCount > references.Count,
            IsContainerListTruncated: isTraversalTruncated || containers.Count >= MaxContainerCount);
    }

    private static bool TryReadReference(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField field,
        string fieldPath,
        string fieldName,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out AssetPreviewAnimatorControllerReferenceDto reference)
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

        var targetReference = AssetsToolsMonoBehaviourFieldScanner.TryResolveSerializedFileReference(
            currentSerializedFile,
            fileId,
            internalSerializedFileLookup,
            out var resolvedReference)
            ? resolvedReference
            : new AssetsToolsSerializedFileReference(
                currentSerializedFile.name,
                ExternalReferencePath: null,
                IsExternal: false);
        var resolvedAsset = TryResolveAssetSummary(
            assetsManager,
            serializedFiles,
            targetReference.SerializedFileName,
            pathId.Value);

        reference = new AssetPreviewAnimatorControllerReferenceDto(
            Path: fieldPath,
            Name: fieldName,
            FileId: fileId,
            PathId: pathId.Value,
            TypeName: resolvedAsset?.TypeName,
            TargetName: resolvedAsset?.Name,
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal);
        return true;
    }

    private static AnimatorControllerResolvedAssetSummary? TryResolveAssetSummary(
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
        return new AnimatorControllerResolvedAssetSummary(
            ResolveTypeName(assetInfo.TypeId),
            name);
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetPreviewAnimatorControllerDto animatorController,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerLayerCount, animatorController.LayerCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerParameterCount, animatorController.ParameterCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerStateCount, animatorController.StateCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerTransitionCount, animatorController.TransitionCount);
        AssetsToolsImagePreviewSupport.AddIntegerInfo(items, AssetPreviewInfoKey.AnimatorControllerReferenceCount, animatorController.ReferenceCount);
        return items;
    }

    private static int CountKnownContainers(
        AssetTypeValueField field,
        AssetPreviewAnimatorControllerContainerKind kind)
    {
        var total = 0;
        var pendingFields = new Stack<AnimatorControllerFieldReadItem>();
        pendingFields.Push(new AnimatorControllerFieldReadItem(field, string.Empty, 0));

        while (pendingFields.Count > 0)
        {
            var item = pendingFields.Pop();
            var fieldName = ResolveFieldName(item.Field, item.ParentPath, item.Index);
            var fieldPath = string.IsNullOrWhiteSpace(item.ParentPath)
                ? fieldName
                : string.Concat(item.ParentPath, ".", fieldName);

            if (TryGetArrayChildren(item.Field, out var arrayChildren) &&
                ResolveContainerKind(fieldPath, fieldName) == kind)
            {
                total += arrayChildren.Count;
            }

            if (item.Field.Children is not { Count: > 0 } childFields)
            {
                continue;
            }

            for (var index = childFields.Count - 1; index >= 0; index--)
            {
                pendingFields.Push(new AnimatorControllerFieldReadItem(childFields[index], fieldPath, index));
            }
        }

        return total;
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

    private static AssetPreviewAnimatorControllerContainerKind ResolveContainerKind(
        string fieldPath,
        string fieldName)
    {
        if (IsNamedField(fieldName, "m_AnimatorLayers"))
        {
            return AssetPreviewAnimatorControllerContainerKind.Layer;
        }

        if (IsNamedField(fieldName, "m_AnimatorParameters"))
        {
            return AssetPreviewAnimatorControllerContainerKind.Parameter;
        }

        if (IsNamedField(fieldName, "m_StateMachineArray"))
        {
            return AssetPreviewAnimatorControllerContainerKind.StateMachine;
        }

        if (IsNamedField(fieldName, "m_StateArray") ||
            IsNamedField(fieldName, "m_StateConstantArray") ||
            IsNamedField(fieldName, "m_SelectorStateConstantArray"))
        {
            return AssetPreviewAnimatorControllerContainerKind.State;
        }

        if (IsNamedField(fieldName, "m_AnyStateTransitionArray"))
        {
            return AssetPreviewAnimatorControllerContainerKind.AnyStateTransition;
        }

        if (IsNamedField(fieldName, "m_TransitionArray") ||
            IsNamedField(fieldName, "m_TransitionConstantArray") ||
            IsNamedField(fieldName, "m_EntryTransitionArray"))
        {
            return AssetPreviewAnimatorControllerContainerKind.Transition;
        }

        if (IsNamedField(fieldName, "m_BlendTreeConstantArray"))
        {
            return AssetPreviewAnimatorControllerContainerKind.BlendTree;
        }

        if (IsNamedField(fieldName, "m_ValueArray"))
        {
            return AssetPreviewAnimatorControllerContainerKind.Value;
        }

        return AssetPreviewAnimatorControllerContainerKind.Unknown;
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

    private static long? ReadPathId(AssetTypeValueField? pptrField)
    {
        var value = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(pptrField, "m_PathID"));
        return value is 0 ? null : value;
    }

    private static int CountPathDepth(string fieldPath)
    {
        return string.IsNullOrWhiteSpace(fieldPath)
            ? 0
            : fieldPath.Count(static character => character == '.') + 1;
    }

    private static bool IsAssetPointerField(AssetTypeValueField field)
    {
        return (field.TypeName?.StartsWith("PPtr<", StringComparison.OrdinalIgnoreCase) == true) ||
               (field.Children?.Count == 2 &&
                AssetsToolsFieldReader.TryGetField(field, "m_FileID") is not null &&
                AssetsToolsFieldReader.TryGetField(field, "m_PathID") is not null);
    }

    private static bool IsNamedField(string fieldName, string expectedFieldName)
    {
        var normalizedName = fieldName;
        var bracketIndex = normalizedName.IndexOf('[');
        if (bracketIndex >= 0)
        {
            normalizedName = normalizedName[..bracketIndex];
        }

        return string.Equals(normalizedName, expectedFieldName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveParameterTypeName(int? parameterType)
    {
        return parameterType switch
        {
            1 => "Float",
            3 => "Int",
            4 => "Bool",
            9 => "Trigger",
            _ => null
        };
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

    private sealed record AnimatorControllerFieldReadItem(
        AssetTypeValueField Field,
        string ParentPath,
        int Index);

    private sealed record AnimatorControllerResolvedAssetSummary(string? TypeName, string? Name);

    private sealed record AnimatorControllerFieldScanResult(
        int StateMachineCount,
        int StateCount,
        int TransitionCount,
        int AnyStateTransitionCount,
        int ReferenceCount,
        IReadOnlyList<AssetPreviewAnimatorControllerReferenceDto> References,
        IReadOnlyList<AssetPreviewAnimatorControllerContainerDto> Containers,
        bool IsReferenceListTruncated,
        bool IsContainerListTruncated);
}
