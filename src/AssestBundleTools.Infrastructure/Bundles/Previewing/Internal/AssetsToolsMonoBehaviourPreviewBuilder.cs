using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsMonoBehaviourPreviewBuilder
{
    private const string MonoBehaviourPreviewContentType = "application/vnd.assestbundletools.mono-behaviour-preview+json";
    private const int MonoBehaviourPreviewSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static bool TryBuildMonoBehaviourPreview(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField monoBehaviourField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        long serializedObjectSizeInBytes,
        out AssetPreviewContentDto previewContent)
    {
        ArgumentNullException.ThrowIfNull(assetsManager);
        ArgumentNullException.ThrowIfNull(serializedFiles);
        ArgumentNullException.ThrowIfNull(currentSerializedFile);
        ArgumentNullException.ThrowIfNull(monoBehaviourField);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        var script = ReadScriptReference(
            assetsManager,
            serializedFiles,
            currentSerializedFile,
            monoBehaviourField,
            internalSerializedFileLookup);
        var scan = AssetsToolsMonoBehaviourFieldScanner.ScanFields(
            assetsManager,
            serializedFiles,
            currentSerializedFile,
            monoBehaviourField,
            internalSerializedFileLookup);
        var monoBehaviour = new AssetPreviewMonoBehaviourDto(
            SchemaVersion: MonoBehaviourPreviewSchemaVersion,
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(monoBehaviourField, "m_Name")),
            IsEnabled: AssetsToolsFieldReader.ReadBoolean(AssetsToolsFieldReader.TryGetField(monoBehaviourField, "m_Enabled")),
            Script: script,
            FieldCount: scan.FieldCount,
            ScalarFieldCount: scan.ScalarFieldCount,
            ReferenceCount: scan.ReferenceCount,
            ArrayFieldCount: scan.ArrayFieldCount,
            Fields: scan.Fields,
            References: scan.References,
            FieldGroups: scan.FieldGroups,
            Containers: scan.Containers,
            IsFieldListTruncated: scan.IsFieldListTruncated,
            IsReferenceListTruncated: scan.IsReferenceListTruncated);

        previewContent = new AssetPreviewContentDto(
            Kind: AssetPreviewKind.MonoBehaviour,
            ContentType: MonoBehaviourPreviewContentType,
            Content: JsonSerializer.SerializeToUtf8Bytes(monoBehaviour, JsonSerializerOptions),
            PreviewInfoItems: BuildPreviewInfoItems(monoBehaviour, serializedObjectSizeInBytes),
            PreferredDisplaySizeInBytes: null);
        return true;
    }

    private static AssetPreviewMonoBehaviourScriptDto? ReadScriptReference(
        AssetsManager assetsManager,
        IReadOnlyList<AssetsFileInstance> serializedFiles,
        AssetsFileInstance currentSerializedFile,
        AssetTypeValueField monoBehaviourField,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup)
    {
        var scriptField = AssetsToolsFieldReader.TryGetField(monoBehaviourField, "m_Script");
        var fileId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(scriptField, "m_FileID")) ?? 0;
        var pathId = AssetsToolsFieldReader.ReadInt64(AssetsToolsFieldReader.TryGetField(scriptField, "m_PathID"));
        if (pathId is null || pathId.Value == 0)
        {
            return null;
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
        var scriptInfo = TryResolveScriptInfo(
            assetsManager,
            serializedFiles,
            targetReference.SerializedFileName,
            pathId.Value);

        return new AssetPreviewMonoBehaviourScriptDto(
            FileId: fileId,
            PathId: pathId.Value,
            Name: scriptInfo?.Name,
            ClassName: scriptInfo?.ClassName,
            Namespace: scriptInfo?.Namespace,
            AssemblyName: scriptInfo?.AssemblyName,
            SerializedFileName: targetReference.SerializedFileName,
            ExternalReferencePath: targetReference.ExternalReferencePath,
            IsExternal: targetReference.IsExternal);
    }

    private static IReadOnlyList<AssetPreviewInfoItemDto> BuildPreviewInfoItems(
        AssetPreviewMonoBehaviourDto monoBehaviour,
        long serializedObjectSizeInBytes)
    {
        var items = new List<AssetPreviewInfoItemDto>();
        AssetsToolsImagePreviewSupport.AddByteSizeInfo(items, AssetPreviewInfoKey.SerializedObjectSize, serializedObjectSizeInBytes);
        AssetsToolsImagePreviewSupport.AddBooleanInfo(items, AssetPreviewInfoKey.MonoBehaviourEnabled, monoBehaviour.IsEnabled);
        AssetsToolsImagePreviewSupport.AddInteger64Info(items, AssetPreviewInfoKey.MonoBehaviourScriptPathId, monoBehaviour.Script?.PathId);
        return items;
    }

    private static MonoBehaviourScriptInfo? TryResolveScriptInfo(
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

        var scriptField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, targetSerializedFile, assetInfo);
        return new MonoBehaviourScriptInfo(
            Name: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(scriptField, "m_Name")),
            ClassName: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(scriptField, "m_ClassName")),
            Namespace: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(scriptField, "m_Namespace")),
            AssemblyName: AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(scriptField, "m_AssemblyName")));
    }

    private sealed record MonoBehaviourScriptInfo(
        string? Name,
        string? ClassName,
        string? Namespace,
        string? AssemblyName);
}
