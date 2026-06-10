using System.Security.Cryptography;
using System.Text.Json;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Extraction;

/// <summary>
/// 基于文件系统保存和读取批量导出 manifest。
/// </summary>
public sealed class FileSystemWorkspaceExportManifestStore : IWorkspaceExportManifestStore
{
    public const string ManifestFileName = "_abt-export-manifest.json";
    private const int CurrentSchemaVersion = 1;
    private const string HashAlgorithm = "MD5";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<Result<WorkspaceExportManifestSaveResultDto>> SaveAsync(
        WorkspaceExportManifestSaveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceExportManifestSaveResultDto>.Cancelled();
        }

        var manifestPath = ResolveManifestPath(request.OutputRoot);
        try
        {
            var outputRoot = Path.GetFullPath(request.OutputRoot.Trim());
            var items = new List<WorkspaceExportManifestItemDto>(request.Items.Count);
            foreach (var item in request.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                items.Add(await CreateManifestItemAsync(outputRoot, item, cancellationToken));
            }

            var manifest = new WorkspaceExportManifestDto(
                SchemaVersion: CurrentSchemaVersion,
                WorkspaceKey: request.WorkspaceKey.Trim(),
                ExportedAtUtc: request.ExportedAtUtc,
                OutputRoot: outputRoot,
                Items: items);

            Directory.CreateDirectory(outputRoot);
            await WriteJsonAtomicallyAsync(manifestPath, manifest, cancellationToken);
            return Result<WorkspaceExportManifestSaveResultDto>.Success(
                new WorkspaceExportManifestSaveResultDto(manifestPath, manifest));
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceExportManifestSaveResultDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            return Result<WorkspaceExportManifestSaveResultDto>.Failure(
                ApplicationErrors.ExportManifestWriteFailed(manifestPath, exception.GetType().Name));
        }
    }

    public async Task<Result<WorkspaceExportManifestDto>> LoadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceExportManifestDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            return Result<WorkspaceExportManifestDto>.Failure(
                ApplicationErrors.ExportManifestReadFailed(string.Empty, "PathRequired"));
        }

        try
        {
            await using var stream = new FileStream(
                manifestPath.Trim(),
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            var manifest = await JsonSerializer.DeserializeAsync<WorkspaceExportManifestDto>(
                stream,
                SerializerOptions,
                cancellationToken);
            return manifest is null
                ? Result<WorkspaceExportManifestDto>.Failure(
                    ApplicationErrors.ExportManifestReadFailed(manifestPath, "EmptyManifest"))
                : Result<WorkspaceExportManifestDto>.Success(manifest);
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceExportManifestDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            return Result<WorkspaceExportManifestDto>.Failure(
                ApplicationErrors.ExportManifestReadFailed(manifestPath, exception.GetType().Name));
        }
    }

    private static string ResolveManifestPath(string outputRoot)
    {
        var root = string.IsNullOrWhiteSpace(outputRoot)
            ? Environment.CurrentDirectory
            : outputRoot.Trim();
        return Path.Combine(Path.GetFullPath(root), ManifestFileName);
    }

    private static async Task<WorkspaceExportManifestItemDto> CreateManifestItemAsync(
        string outputRoot,
        WorkspaceExportManifestSourceItemDto sourceItem,
        CancellationToken cancellationToken)
    {
        var outputPath = Path.GetFullPath(sourceItem.OutputPath);
        var fileInfo = new FileInfo(outputPath);
        await using var stream = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        var hash = await MD5.HashDataAsync(stream, cancellationToken);
        var relativePath = Path.GetRelativePath(outputRoot, outputPath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return new WorkspaceExportManifestItemDto(
            RelativePath: relativePath,
            BundleId: sourceItem.BundleId.Value,
            BundleDisplayName: sourceItem.BundleDisplayName,
            AssetId: sourceItem.AssetId.Value,
            PathId: sourceItem.PathId,
            AssetType: sourceItem.AssetType,
            AssetTypeDisplayName: sourceItem.AssetTypeDisplayName,
            OriginalName: sourceItem.OriginalName,
            DisambiguatedName: sourceItem.DisambiguatedName,
            ContainerPath: sourceItem.ContainerPath,
            OutputRole: sourceItem.OutputRole,
            SpineGroupId: sourceItem.SpineGroupId,
            SpineGroupDisplayName: sourceItem.SpineGroupDisplayName,
            ExportedFileSizeInBytes: fileInfo.Exists ? fileInfo.Length : 0,
            ExportedFileHashAlgorithm: HashAlgorithm,
            ExportedFileHash: Convert.ToHexString(hash).ToLowerInvariant(),
            ExportedFileLastWriteTimeUtc: fileInfo.Exists
                ? fileInfo.LastWriteTimeUtc
                : DateTimeOffset.MinValue);
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string filePath,
        T value,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryFilePath = Path.Combine(
            directory ?? Environment.CurrentDirectory,
            $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporaryFilePath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
            }

            File.Move(temporaryFilePath, filePath, overwrite: true);
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryFilePath);
        }
    }

    private static void TryDeleteTemporaryFile(string temporaryFilePath)
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
}
