using System.Security.Cryptography;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

/// <summary>
/// 基于文件系统写出单个 AB 文件。
/// </summary>
/// <remarks>
/// 该实现目前以标准化 artifact 为输入做文件级原子输出；
/// 后续对象级替换写包会在同一端口内扩展为 AssetsTools 写入实现。
/// </remarks>
public sealed class FileSystemWorkspaceBundleExporter : IWorkspaceBundleExporter
{
    private const string HashAlgorithm = "SHA256";

    public async Task<Result<WorkspaceBundleExportResultDto>> ExportAsync(
        WorkspaceBundleExportRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBundleExportResultDto>.Cancelled();
        }

        var outputPath = NormalizePath(request.OutputFilePath);
        var sourcePath = NormalizePath(request.SourceArtifactPath);
        string? temporaryFilePath = null;
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return Result<WorkspaceBundleExportResultDto>.Failure(
                    ApplicationErrors.BundleExportArtifactMissing(request.BundleDisplayName, "Save.Artifact.SourceFileMissing"));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                return Result<WorkspaceBundleExportResultDto>.Failure(
                    ApplicationErrors.BundleExportWriteFailed(request.OutputFilePath, "OutputPathRequired"));
            }

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            temporaryFilePath = CreateTemporaryFilePath(outputPath);
            await using (var sourceStream = new FileStream(
                             sourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read))
            await using (var outputStream = new FileStream(
                             temporaryFilePath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None))
            {
                await sourceStream.CopyToAsync(outputStream, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryFilePath, outputPath, overwrite: false);
            temporaryFilePath = null;

            var fileInfo = new FileInfo(outputPath);
            var hashes = await ComputeHashesAsync(outputPath, cancellationToken);
            return Result<WorkspaceBundleExportResultDto>.Success(
                new WorkspaceBundleExportResultDto(
                    BundleId: request.BundleId,
                    BundleDisplayName: request.BundleDisplayName,
                    OutputFilePath: outputPath,
                    SourceArtifactPath: sourcePath,
                    BundleCacheKey: request.BundleCacheKey,
                    WrittenFileSizeInBytes: fileInfo.Length,
                    WrittenFileHashAlgorithm: HashAlgorithm,
                    WrittenFileHash: hashes.Sha256,
                    WrittenFileLastWriteTimeUtc: new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
                    WrittenFileMd5: hashes.Md5,
                    WrittenFileSha256: hashes.Sha256,
                    ProcessorResults: SummarizeProcessorResults(request.ProcessorResults)));
        }
        catch (OperationCanceledException)
        {
            TryDeleteTemporaryFile(temporaryFilePath);
            return Result<WorkspaceBundleExportResultDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            TryDeleteTemporaryFile(temporaryFilePath);
            return Result<WorkspaceBundleExportResultDto>.Failure(
                ApplicationErrors.BundleExportWriteFailed(outputPath ?? request.OutputFilePath, exception.GetType().Name));
        }
    }

    private static string? NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.GetFullPath(path.Trim());
    }

    private static string CreateTemporaryFilePath(string outputPath)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        var fileName = Path.GetFileName(outputPath);
        return Path.Combine(
            string.IsNullOrWhiteSpace(outputDirectory) ? Environment.CurrentDirectory : outputDirectory,
            $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static async Task<WorkspaceBundleExportFileHashes> ComputeHashesAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var md5Stream = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        var md5 = await MD5.HashDataAsync(md5Stream, cancellationToken);

        await using var sha256Stream = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        var sha256 = await SHA256.HashDataAsync(sha256Stream, cancellationToken);
        return new WorkspaceBundleExportFileHashes(
            Convert.ToHexString(md5).ToLowerInvariant(),
            Convert.ToHexString(sha256).ToLowerInvariant());
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

    private sealed record WorkspaceBundleExportFileHashes(string Md5, string Sha256);

    private static IReadOnlyList<WorkspaceBundleProcessorExecutionSummaryDto> SummarizeProcessorResults(
        IReadOnlyList<WorkspaceResourceProcessorExecutionResultDto>? processorResults)
    {
        if (processorResults is not { Count: > 0 })
        {
            return Array.Empty<WorkspaceBundleProcessorExecutionSummaryDto>();
        }

        return processorResults
            .GroupBy(
                static result => new ProcessorSummaryKey(result.ResourceKind, result.ProcessorId),
                ProcessorSummaryKeyComparer.Instance)
            .Select(static group =>
            {
                var orderedResults = group
                    .OrderBy(static result => result.AssetName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static result => result.AssetId.Value, StringComparer.Ordinal)
                    .ToArray();
                var failedCount = orderedResults.Count(static result => result.Status == WorkspaceResourceProcessorStatusDto.Failed);
                var skippedCount = orderedResults.Count(static result => result.Status == WorkspaceResourceProcessorStatusDto.Skipped);
                var status = failedCount > 0
                    ? WorkspaceResourceProcessorStatusDto.Failed
                    : skippedCount == orderedResults.Length
                        ? WorkspaceResourceProcessorStatusDto.Skipped
                        : WorkspaceResourceProcessorStatusDto.Succeeded;

                return new WorkspaceBundleProcessorExecutionSummaryDto(
                    ResourceKind: group.Key.ResourceKind,
                    ProcessorId: group.Key.ProcessorId,
                    Status: status,
                    InputCount: orderedResults.Length,
                    ProcessedCount: orderedResults.Count(static result => result.Status == WorkspaceResourceProcessorStatusDto.Succeeded),
                    FailedCount: failedCount,
                    ResultPaths: orderedResults
                        .Select(static result => result.ResultPath)
                        .Where(static path => !string.IsNullOrWhiteSpace(path))
                        .Select(static path => path!)
                        .ToArray(),
                    Diagnostics: orderedResults
                        .SelectMany(static result => result.Diagnostics)
                        .ToArray());
            })
            .OrderBy(static summary => summary.ResourceKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static summary => summary.ProcessorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private readonly record struct ProcessorSummaryKey(string ResourceKind, string ProcessorId);

    private sealed class ProcessorSummaryKeyComparer : IEqualityComparer<ProcessorSummaryKey>
    {
        public static ProcessorSummaryKeyComparer Instance { get; } = new();

        public bool Equals(ProcessorSummaryKey x, ProcessorSummaryKey y) =>
            string.Equals(x.ResourceKind, y.ResourceKind, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.ProcessorId, y.ProcessorId, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(ProcessorSummaryKey obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ResourceKind),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ProcessorId));
    }
}
