using System.Text.Json;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

/// <summary>
/// 基于文件系统写出 AB 导出清单。
/// </summary>
public sealed class FileSystemWorkspaceBundleExportManifestStore : IWorkspaceBundleExportManifestStore
{
    public const string ManifestFileName = "_abt-bundle-export-manifest.json";
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<Result<WorkspaceBundleExportManifestSaveResultDto>> SaveAsync(
        WorkspaceBundleExportManifestSaveRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBundleExportManifestSaveResultDto>.Cancelled();
        }

        var manifestPath = ResolveManifestPath(request.OutputDirectory);
        try
        {
            var outputDirectory = Path.GetFullPath(request.OutputDirectory.Trim());
            Directory.CreateDirectory(outputDirectory);
            var manifest = new WorkspaceBundleExportManifestDto(
                SchemaVersion: CurrentSchemaVersion,
                WorkspaceId: request.WorkspaceId.Value,
                WorkspaceKey: NormalizeNullable(request.WorkspaceKey),
                WorkspaceDisplayName: request.WorkspaceDisplayName,
                OutputDirectory: outputDirectory,
                ExportedAtUtc: request.ExportedAtUtc,
                Bundles: request.BundleResults.Select(result => CreateManifestItem(outputDirectory, result)).ToArray(),
                ProcessorSelections: request.ProcessorSelections
                    .Select(static selection => new WorkspaceBundleExportManifestProcessorSelectionDto(
                        selection.ResourceKind,
                        selection.ProcessorId))
                    .ToArray());

            await WriteJsonAtomicallyAsync(manifestPath, manifest, cancellationToken);
            return Result<WorkspaceBundleExportManifestSaveResultDto>.Success(
                new WorkspaceBundleExportManifestSaveResultDto(manifestPath, manifest));
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBundleExportManifestSaveResultDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException)
        {
            return Result<WorkspaceBundleExportManifestSaveResultDto>.Failure(
                ApplicationErrors.BundleExportManifestWriteFailed(manifestPath, exception.GetType().Name));
        }
    }

    private static string ResolveManifestPath(string outputDirectory)
    {
        var directory = string.IsNullOrWhiteSpace(outputDirectory)
            ? Environment.CurrentDirectory
            : outputDirectory.Trim();
        return Path.Combine(Path.GetFullPath(directory), ManifestFileName);
    }

    private static WorkspaceBundleExportManifestItemDto CreateManifestItem(
        string outputDirectory,
        WorkspaceSaveBundleExecutionResultDto result)
    {
        var outputPath = string.IsNullOrWhiteSpace(result.OutputFilePath)
            ? string.Empty
            : Path.GetFullPath(result.OutputFilePath);
        var relativePath = string.IsNullOrWhiteSpace(outputPath)
            ? string.Empty
            : Path.GetRelativePath(outputDirectory, outputPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

        return new WorkspaceBundleExportManifestItemDto(
            BundleId: result.BundleId.Value,
            BundleDisplayName: result.BundleDisplayName,
            RelativePath: relativePath,
            OutputPath: outputPath,
            Status: result.Status.ToString(),
            ChangeCount: result.ChangeCount,
            ModifiedAssetCount: result.ModifiedAssetCount,
            WorkspaceReplacementCount: result.WorkspaceReplacementCount,
            FileReplacementCount: result.FileReplacementCount,
            BundleCacheKey: result.BundleCacheKey,
            SourceArtifactPath: result.SourceArtifactPath,
            WrittenFileSizeInBytes: result.WrittenFileSizeInBytes,
            WrittenFileHashAlgorithm: result.WrittenFileHashAlgorithm,
            WrittenFileHash: result.WrittenFileHash,
            WrittenFileLastWriteTimeUtc: result.WrittenFileLastWriteTimeUtc,
            DiagnosticCode: result.DiagnosticCode,
            DiagnosticDetail: result.DiagnosticDetail,
            WrittenFileMd5: result.WrittenFileMd5,
            WrittenFileSha256: result.WrittenFileSha256,
            ProcessorResults: (result.ProcessorResults ?? Array.Empty<WorkspaceBundleProcessorExecutionSummaryDto>())
                .Select(static summary => new WorkspaceBundleExportManifestProcessorResultDto(
                    summary.ResourceKind,
                    summary.ProcessorId,
                    summary.Status.ToString(),
                    summary.InputCount,
                    summary.ProcessedCount,
                    summary.FailedCount,
                    summary.ResultPaths,
                    summary.Diagnostics))
                .ToArray(),
            CompressionMode: result.CompressionMode.ToString(),
            OriginalCompression: result.OriginalCompression,
            WrittenCompression: result.WrittenCompression);
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
                             FileMode.CreateNew,
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

    private static string? NormalizeNullable(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
