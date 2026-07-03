using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

/// <summary>
/// 使用 AssetsTools.NET 把对象级写入项应用到单个 AB artifact。
/// </summary>
internal static class AssetsToolsWorkspaceBundleObjectWriter
{
    private const string SourceKindFile = "file";
    private const string SourceKindWorkspace = "workspace";
    private const string SourceKindSpriteEdit = "sprite-edit";
    private const string StageLoadBundle = "load-bundle";
    private const string StageApplyChanges = "apply-changes";
    private const string StageWriteUncompressed = "write-uncompressed";
    private const string StageRepackBundle = "repack-bundle";
    private const string StageWriteFinalUncompressed = "write-final-uncompressed";

    public static Result<object> Write(
        string sourcePath,
        string temporaryFilePath,
        WorkspaceBundleExportRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.AssetWriteItems.Count == 0)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(request.OutputFilePath, "Save.BundleWrite.NoObjectChanges"));
        }

        var assetsManager = new AssetsManager
        {
            UseQuickLookup = true,
            UseTemplateFieldCache = true,
            UseMonoTemplateFieldCache = true
        };

        try
        {
            var bundleInstance = BundleExportWriteDiagnostics.ExecuteStage(
                StageLoadBundle,
                request,
                () => assetsManager.LoadBundleFile(sourcePath, unpackIfPacked: true));
            var serializedFiles = LoadSerializedFiles(assetsManager, bundleInstance);
            var applyResult = BundleExportWriteDiagnostics.ExecuteStage(
                StageApplyChanges,
                request,
                () =>
                {
                    foreach (var writeItem in request.AssetWriteItems)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = ApplyWriteItem(assetsManager, serializedFiles, writeItem);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }

                    return Result<object>.Success(new object());
                });
            if (applyResult.IsFailure)
            {
                return applyResult;
            }

            BundleExportWriteDiagnostics.ExecuteStage(
                StageApplyChanges,
                request,
                () => ApplyModifiedSerializedFiles(bundleInstance, serializedFiles));
            WriteBundleWithReplacers(bundleInstance, temporaryFilePath, request);
            return Result<object>.Success(new object());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (BundleExportWriteDiagnostics.IsDiagnosableWriteException(exception))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    request.OutputFilePath,
                    BundleExportWriteDiagnostics.ResolveTopLevelDetail("write-bundle", request, exception)));
        }
        finally
        {
            assetsManager.UnloadAll(unloadClassData: false);
        }
    }

    private static void WriteBundleWithReplacers(
        BundleFileInstance bundleInstance,
        string temporaryFilePath,
        WorkspaceBundleExportRequestDto request)
    {
        if (bundleInstance.originalCompression == AssetBundleCompressionType.None)
        {
            WriteUncompressedBundle(bundleInstance.file, temporaryFilePath, StageWriteFinalUncompressed, request);
            return;
        }

        var uncompressedTemporaryFilePath = CreateSiblingTemporaryFilePath(temporaryFilePath, ".uncompressed");
        try
        {
            WriteUncompressedBundle(bundleInstance.file, uncompressedTemporaryFilePath, StageWriteUncompressed, request);
            var uncompressedBundle = new AssetBundleFile();
            try
            {
                using var inputStream = File.OpenRead(uncompressedTemporaryFilePath);
                using var reader = new AssetsFileReader(inputStream);
                BundleExportWriteDiagnostics.ExecuteStage(
                    StageRepackBundle,
                    request,
                    BundleExportStageContext.FromBundle(bundleInstance),
                    () => uncompressedBundle.Read(reader));
                using var outputStream = new FileStream(
                    temporaryFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);
                using var writer = new AssetsFileWriter(outputStream);
                BundleExportWriteDiagnostics.ExecuteStage(
                    StageRepackBundle,
                    request,
                    BundleExportStageContext.FromBundle(bundleInstance),
                    () => uncompressedBundle.Pack(writer, bundleInstance.originalCompression, blockDirAtEnd: false, progress: null));
            }
            finally
            {
                uncompressedBundle.Close();
            }
        }
        finally
        {
            TryDeleteTemporaryFile(uncompressedTemporaryFilePath);
        }
    }

    private static void WriteUncompressedBundle(
        AssetBundleFile bundleFile,
        string outputPath,
        string stage,
        WorkspaceBundleExportRequestDto request)
    {
        using var outputStream = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new AssetsFileWriter(outputStream);
        BundleExportWriteDiagnostics.ExecuteStage(
            stage,
            request,
            BundleExportStageContext.FromBundle(bundleFile),
            () => bundleFile.Write(writer, filePos: -1));
    }

    private static IReadOnlyList<LoadedSerializedFile> LoadSerializedFiles(
        AssetsManager assetsManager,
        BundleFileInstance bundleInstance)
    {
        var serializedFiles = new List<LoadedSerializedFile>();
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;
        for (var index = 0; index < directoryInfos.Count; index++)
        {
            if (!bundleInstance.file.IsAssetsFile(index))
            {
                continue;
            }

            serializedFiles.Add(new LoadedSerializedFile(
                assetsManager.LoadAssetsFileFromBundle(bundleInstance, index, loadDeps: false),
                index));
        }

        return serializedFiles;
    }

    private static void ApplyModifiedSerializedFiles(
        BundleFileInstance bundleInstance,
        IReadOnlyList<LoadedSerializedFile> serializedFiles)
    {
        var directoryInfos = bundleInstance.file.BlockAndDirInfo.DirectoryInfos;
        foreach (var serializedFile in serializedFiles.Where(static file => file.IsDirty))
        {
            var directoryInfo = directoryInfos[serializedFile.DirectoryIndex];
            directoryInfo.SetNewData(serializedFile.File.file);
            directoryInfos[serializedFile.DirectoryIndex] = directoryInfo;
        }
    }

    private static Result<object> ApplyWriteItem(
        AssetsManager assetsManager,
        IReadOnlyList<LoadedSerializedFile> serializedFiles,
        WorkspaceBundleAssetWriteItemDto writeItem)
    {
        var serializedFile = FindSerializedFile(serializedFiles, writeItem.SerializedFileName);
        if (serializedFile is null ||
            !AssetsToolsFieldReader.TryFindAssetInfoByPathId(serializedFile.File, writeItem.PathId, out var assetInfo))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.AssetNotFound"));
        }

        var baseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, serializedFile.File, assetInfo);
        if (baseField is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.BaseFieldUnavailable"));
        }

        var payloadPath = ResolvePayloadPath(writeItem);
        var result = NormalizeSourceKind(writeItem.SourceKind) switch
        {
            SourceKindFile => ApplyExternalPayload(baseField, writeItem, payloadPath),
            SourceKindWorkspace => ApplyWorkspacePayload(assetsManager, serializedFiles, baseField, writeItem),
            SourceKindSpriteEdit => ApplySpriteEditPayload(baseField, writeItem),
            _ => Result<object>.Failure(ApplicationErrors.BundleExportWriteFailed(
                writeItem.AssetName,
                "Save.BundleWrite.UnsupportedSourceKind"))
        };
        if (result.IsFailure)
        {
            return result;
        }

        assetInfo.SetNewData(baseField);
        serializedFile.IsDirty = true;
        return Result<object>.Success(new object());
    }

    private static Result<object> ApplyExternalPayload(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string? payloadPath)
    {
        if (string.IsNullOrWhiteSpace(payloadPath) || !File.Exists(payloadPath))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.PayloadMissing"));
        }

        return writeItem.AssetType switch
        {
            AssetType.TextAsset => ApplyTextAssetPayload(baseField, writeItem, payloadPath),
            AssetType.Texture2D => ApplyTexturePayload(baseField, writeItem, payloadPath),
            _ => Result<object>.Failure(ApplicationErrors.BundleExportWriteFailed(
                writeItem.AssetName,
                $"Save.BundleWrite.UnsupportedAssetType.{writeItem.AssetType}"))
        };
    }

    private static Result<object> ApplyWorkspacePayload(
        AssetsManager assetsManager,
        IReadOnlyList<LoadedSerializedFile> serializedFiles,
        AssetTypeValueField targetBaseField,
        WorkspaceBundleAssetWriteItemDto writeItem)
    {
        if (string.IsNullOrWhiteSpace(writeItem.SourceSerializedFileName) ||
            !writeItem.SourcePathId.HasValue)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.WorkspaceSourceNotResolved"));
        }

        var sourceSerializedFile = FindSerializedFile(serializedFiles, writeItem.SourceSerializedFileName);
        if (sourceSerializedFile is null ||
            !AssetsToolsFieldReader.TryFindAssetInfoByPathId(sourceSerializedFile.File, writeItem.SourcePathId.Value, out var sourceAssetInfo))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.WorkspaceSourceNotFound"));
        }

        var sourceBaseField = AssetsToolsFieldReader.TryGetBaseField(assetsManager, sourceSerializedFile.File, sourceAssetInfo);
        if (sourceBaseField is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.WorkspaceSourceBaseFieldUnavailable"));
        }

        CopyMutableAssetFields(targetBaseField, sourceBaseField);
        return Result<object>.Success(new object());
    }

    private static Result<object> ApplySpriteEditPayload(
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

        var rectField = AssetsToolsFieldReader.TryGetField(baseField, "m_Rect");
        if (rectField is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.SpriteRectUnavailable"));
        }

        var geometry = writeItem.SpriteEditGeometry;
        SetFloat(rectField, "x", geometry.RegionX);
        SetFloat(rectField, "y", geometry.RegionY);
        SetFloat(rectField, "width", geometry.RegionWidth);
        SetFloat(rectField, "height", geometry.RegionHeight);

        var pivotField = AssetsToolsFieldReader.TryGetField(baseField, "m_Pivot");
        if (pivotField is not null)
        {
            SetFloat(pivotField, "x", geometry.PivotX);
            SetFloat(pivotField, "y", geometry.PivotY);
        }

        return Result<object>.Success(new object());
    }

    private static Result<object> ApplyTextAssetPayload(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath)
    {
        var scriptField = AssetsToolsFieldReader.TryGetField(baseField, "m_Script");
        if (scriptField is null)
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    "Save.BundleWrite.TextAssetScriptUnavailable"));
        }

        scriptField.AsString = File.ReadAllText(payloadPath, Encoding.UTF8);
        return Result<object>.Success(new object());
    }

    private static Result<object> ApplyTexturePayload(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath)
    {
        try
        {
            var texture = TextureFile.ReadTextureFile(baseField);
            texture.EncodeTextureImage(payloadPath, quality: 90);
            texture.WriteTo(baseField);
            return Result<object>.Success(new object());
        }
        catch (Exception exception) when (BundleExportWriteDiagnostics.IsDiagnosableWriteException(exception))
        {
            return Result<object>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    writeItem.AssetName,
                    BuildTextureFailureDetail(baseField, writeItem, payloadPath, exception)));
        }
    }

    private static string BuildTextureFailureDetail(
        AssetTypeValueField baseField,
        WorkspaceBundleAssetWriteItemDto writeItem,
        string payloadPath,
        Exception exception)
    {
        var textureFormat = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_TextureFormat"));
        var width = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Width"));
        var height = AssetsToolsFieldReader.ReadInt32(AssetsToolsFieldReader.TryGetField(baseField, "m_Height"));
        return string.Join(
            "; ",
            new[]
            {
                "Save.BundleWrite.TextureEncodeFailed",
                $"asset={writeItem.AssetName}",
                $"pathId={writeItem.PathId}",
                $"format={FormatTextureFormat(textureFormat)}",
                $"size={FormatTextureSize(width, height)}",
                $"payload={Path.GetFileName(payloadPath)}",
                $"process={BundleExportWriteDiagnostics.FormatProcessArchitecture()}",
                BundleExportWriteDiagnostics.FormatManagedMemory(),
                BundleExportWriteDiagnostics.FormatExceptionDiagnostic(exception)
            }.Where(static part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string FormatTextureFormat(int? textureFormat)
    {
        if (!textureFormat.HasValue)
        {
            return "unknown";
        }

        return Enum.IsDefined(typeof(TextureFormat), textureFormat.Value)
            ? $"{(TextureFormat)textureFormat.Value}({textureFormat.Value})"
            : textureFormat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string FormatTextureSize(int? width, int? height) =>
        width.HasValue && height.HasValue
            ? $"{width.Value}x{height.Value}"
            : "unknown";

    private static string NormalizeDiagnosticText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();

    private static void CopyMutableAssetFields(
        AssetTypeValueField targetBaseField,
        AssetTypeValueField sourceBaseField)
    {
        var sourceClone = sourceBaseField.Clone();
        var originalName = AssetsToolsFieldReader.ReadString(AssetsToolsFieldReader.TryGetField(targetBaseField, "m_Name"));
        targetBaseField.Value = sourceClone.Value;
        targetBaseField.Children = sourceClone.Children;

        var nameField = AssetsToolsFieldReader.TryGetField(targetBaseField, "m_Name");
        if (nameField is not null && originalName is not null)
        {
            nameField.AsString = originalName;
        }
    }

    private static LoadedSerializedFile? FindSerializedFile(
        IReadOnlyList<LoadedSerializedFile> serializedFiles,
        string serializedFileName)
    {
        return serializedFiles.FirstOrDefault(file =>
            string.Equals(file.File.name, serializedFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(file.File.path, serializedFileName, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolvePayloadPath(WorkspaceBundleAssetWriteItemDto writeItem) =>
        !string.IsNullOrWhiteSpace(writeItem.ProcessorOutputPath)
            ? writeItem.ProcessorOutputPath
            : writeItem.SourceFilePath;

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

    private static string NormalizeSourceKind(string? sourceKind) =>
        string.IsNullOrWhiteSpace(sourceKind)
            ? string.Empty
            : sourceKind.Trim().ToLowerInvariant();

    private static string CreateSiblingTemporaryFilePath(
        string outputPath,
        string suffix)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        var fileName = Path.GetFileName(outputPath);
        return Path.Combine(
            string.IsNullOrWhiteSpace(outputDirectory) ? Environment.CurrentDirectory : outputDirectory,
            $".{fileName}.{Guid.NewGuid():N}{suffix}");
    }

    private static void TryDeleteTemporaryFile(string? temporaryFilePath)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(temporaryFilePath) && File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class LoadedSerializedFile
    {
        public LoadedSerializedFile(
            AssetsFileInstance file,
            int directoryIndex)
        {
            File = file;
            DirectoryIndex = directoryIndex;
        }

        public AssetsFileInstance File { get; }

        public int DirectoryIndex { get; }

        public bool IsDirty { get; set; }
    }
}
