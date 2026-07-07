using AssetsTools.NET;
using AssetsTools.NET.Extra;
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
    private const string SourceKindTextDraft = "text-draft";
    private const string StageLoadBundle = "load-bundle";
    private const string StageApplyChanges = "apply-changes";
    private const string StageWriteUncompressed = "write-uncompressed";
    private const string StageRepackBundle = "repack-bundle";
    private const string StageWriteFinalUncompressed = "write-final-uncompressed";

    public static Result<WorkspaceBundleObjectWriteResult> Write(
        string sourcePath,
        string temporaryFilePath,
        WorkspaceBundleExportRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.AssetWriteItems.Count == 0)
        {
            return Result<WorkspaceBundleObjectWriteResult>.Failure(
                ApplicationErrors.BundleExportWriteFailed(request.OutputFilePath, "Save.BundleWrite.NoObjectChanges"));
        }

        AssetsToolsBundleLoadedSession? session = null;
        try
        {
            session = BundleExportWriteDiagnostics.ExecuteStage(
                StageLoadBundle,
                request,
                () => AssetsToolsBundleLoadService.LoadIsolated(sourcePath));
            var bundleInstance = session.BundleInstance;
            var serializedFiles = LoadSerializedFiles(session);
            var applyResult = BundleExportWriteDiagnostics.ExecuteStage(
                StageApplyChanges,
                request,
                () =>
                {
                    foreach (var writeItem in request.AssetWriteItems)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var result = ApplyWriteItem(session.AssetsManager, serializedFiles, writeItem);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }

                    return Result<object>.Success(new object());
                });
            if (applyResult.IsFailure)
            {
                return Result<WorkspaceBundleObjectWriteResult>.Failure(applyResult.Errors);
            }

            BundleExportWriteDiagnostics.ExecuteStage(
                StageApplyChanges,
                request,
                () => ApplyModifiedSerializedFiles(bundleInstance, serializedFiles));
            var originalCompression = AssetsToolsBundleCompressionPolicy.ResolveOriginalCompression(
                request.OriginalCompression,
                bundleInstance.originalCompression);
            var writtenCompression = WriteBundleWithReplacers(bundleInstance, temporaryFilePath, request, originalCompression);
            return Result<WorkspaceBundleObjectWriteResult>.Success(new WorkspaceBundleObjectWriteResult(
                originalCompression.ToString(),
                writtenCompression.ToString()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (BundleExportWriteDiagnostics.IsDiagnosableWriteException(exception))
        {
            return Result<WorkspaceBundleObjectWriteResult>.Failure(
                ApplicationErrors.BundleExportWriteFailed(
                    request.OutputFilePath,
                    BundleExportWriteDiagnostics.ResolveTopLevelDetail("write-bundle", request, exception)));
        }
        finally
        {
            session?.Dispose();
        }
    }

    private static AssetBundleCompressionType WriteBundleWithReplacers(
        BundleFileInstance bundleInstance,
        string temporaryFilePath,
        WorkspaceBundleExportRequestDto request,
        AssetBundleCompressionType originalCompression)
    {
        var targetCompression = AssetsToolsBundleCompressionPolicy.ResolveTargetCompression(
            originalCompression,
            request.CompressionMode);
        if (targetCompression == AssetBundleCompressionType.None)
        {
            WriteUncompressedBundle(bundleInstance.file, temporaryFilePath, StageWriteFinalUncompressed, request);
            return targetCompression;
        }

        var uncompressedTemporaryFilePath = CreateScratchTemporaryFilePath(temporaryFilePath, ".uncompressed");
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
                    () => uncompressedBundle.Pack(writer, targetCompression, blockDirAtEnd: false, progress: null));
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

        return targetCompression;
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
        AssetsToolsBundleLoadedSession session)
    {
        var serializedFiles = new List<LoadedSerializedFile>();
        foreach (var serializedFile in session.SerializedFiles)
        {
            if (!TryResolveDirectoryIndex(session, serializedFile, out var directoryIndex))
            {
                continue;
            }

            serializedFiles.Add(new LoadedSerializedFile(
                serializedFile,
                directoryIndex));
        }

        return serializedFiles;
    }

    private static bool TryResolveDirectoryIndex(
        AssetsToolsBundleLoadedSession session,
        AssetsFileInstance serializedFile,
        out int directoryIndex)
    {
        if (!string.IsNullOrWhiteSpace(serializedFile.name) &&
            session.SerializedFileDirectoryIndexLookup.TryGetValue(serializedFile.name, out directoryIndex))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(serializedFile.path) &&
            session.SerializedFileDirectoryIndexLookup.TryGetValue(serializedFile.path, out directoryIndex))
        {
            return true;
        }

        directoryIndex = -1;
        return false;
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
            SourceKindSpriteEdit => AssetsToolsSpriteEditPayloadWriter.Apply(baseField, writeItem),
            SourceKindTextDraft => AssetsToolsTextAssetPayloadWriter.ApplyDraft(baseField, writeItem),
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
            AssetType.TextAsset => AssetsToolsTextAssetPayloadWriter.ApplyFilePayload(baseField, writeItem, payloadPath),
            AssetType.Texture2D => AssetsToolsTexturePayloadWriter.Apply(baseField, writeItem, payloadPath),
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

    private static string NormalizeSourceKind(string? sourceKind) =>
        string.IsNullOrWhiteSpace(sourceKind)
            ? string.Empty
            : sourceKind.Trim().ToLowerInvariant();

    private static string CreateScratchTemporaryFilePath(
        string outputPath,
        string suffix)
    {
        var fileName = Path.GetFileName(outputPath);
        var scratchDirectory = Path.Combine(Path.GetTempPath(), "AssestBundleTools", "bundle-export");
        Directory.CreateDirectory(scratchDirectory);
        return Path.Combine(
            scratchDirectory,
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
