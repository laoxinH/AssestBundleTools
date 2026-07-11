using System.Security.Cryptography;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

/// <summary>
/// 基于文件系统和 AssetsTools.NET 写出单个 AB 文件。
/// </summary>
/// <remarks>
/// 写包会从标准化 artifact 独立加载 AB，应用对象级替换载荷后再原子提升到目标路径。
/// </remarks>
public sealed class FileSystemWorkspaceBundleExporter : IWorkspaceBundleExporter
{
    private const string HashAlgorithm = "SHA256";
    private readonly IAssetBundleDecryptorRegistry? _assetBundleDecryptorRegistry;

    public FileSystemWorkspaceBundleExporter(
        IAssetBundleDecryptorRegistry? assetBundleDecryptorRegistry = null)
    {
        _assetBundleDecryptorRegistry = assetBundleDecryptorRegistry;
    }

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
        string? standardTemporaryFilePath = null;
        string? codecTemporaryFilePath = null;
        string? promotionFilePath = null;
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

            standardTemporaryFilePath = CreateTemporaryFilePath(outputPath, "standard");
            var writeResult = AssetsToolsWorkspaceBundleObjectWriter.Write(
                sourcePath,
                standardTemporaryFilePath,
                request,
                cancellationToken);
            if (writeResult.IsFailure)
            {
                return Result<WorkspaceBundleExportResultDto>.Failure(writeResult.Errors);
            }

            var codecResult = await ApplySourceCodecAsync(
                request,
                standardTemporaryFilePath,
                outputPath,
                cancellationToken);
            if (codecResult.IsCancelled)
            {
                return Result<WorkspaceBundleExportResultDto>.Cancelled();
            }

            if (codecResult.IsFailure || codecResult.Value is null)
            {
                return Result<WorkspaceBundleExportResultDto>.Failure(codecResult.Errors);
            }

            codecTemporaryFilePath = codecResult.Value.CodecTemporaryFilePath;
            promotionFilePath = codecResult.Value.PromotionFilePath;
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(
                promotionFilePath,
                outputPath,
                overwrite: request.OutputConflictPolicy == WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting);
            promotionFilePath = null;

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
                    ProcessorResults: SummarizeProcessorResults(request.ProcessorResults),
                    CompressionMode: request.CompressionMode,
                    OriginalCompression: writeResult.Value?.OriginalCompression,
                    WrittenCompression: writeResult.Value?.WrittenCompression,
                    OutputCodecId: codecResult.Value.OutputCodecId,
                    OutputCodecVersion: codecResult.Value.OutputCodecVersion));
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBundleExportResultDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<WorkspaceBundleExportResultDto>.Failure(
                ApplicationErrors.BundleExportWriteFailed(outputPath ?? request.OutputFilePath, exception.GetType().Name));
        }
        finally
        {
            TryDeleteTemporaryFile(promotionFilePath);
            TryDeleteTemporaryFile(codecTemporaryFilePath);
            TryDeleteTemporaryFile(standardTemporaryFilePath);
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

    private async Task<Result<WorkspaceBundleCodecOutput>> ApplySourceCodecAsync(
        WorkspaceBundleExportRequestDto request,
        string standardBundlePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceCodecId))
        {
            return Result<WorkspaceBundleCodecOutput>.Success(
                WorkspaceBundleCodecOutput.Standard(standardBundlePath));
        }

        var codecId = request.SourceCodecId.Trim();
        if (_assetBundleDecryptorRegistry is null ||
            !_assetBundleDecryptorRegistry.TryGet(codecId, out var decryptor))
        {
            return Result<WorkspaceBundleCodecOutput>.Failure(
                ApplicationErrors.BundleExportCodecNotFound(codecId));
        }

        if (!string.IsNullOrWhiteSpace(request.SourceCodecVersion) &&
            !string.Equals(
                request.SourceCodecVersion.Trim(),
                decryptor.Descriptor.Version,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<WorkspaceBundleCodecOutput>.Failure(
                ApplicationErrors.BundleExportCodecVersionMismatch(
                    codecId,
                    request.SourceCodecVersion.Trim(),
                    decryptor.Descriptor.Version));
        }

        if (!decryptor.Descriptor.SupportsEncryption)
        {
            return Result<WorkspaceBundleCodecOutput>.Success(
                WorkspaceBundleCodecOutput.Standard(standardBundlePath));
        }

        if (decryptor is not IAssetBundleEncryptor encryptor)
        {
            return Result<WorkspaceBundleCodecOutput>.Failure(
                ApplicationErrors.BundleExportCodecEncryptionUnsupported(codecId));
        }

        var codecOutputPath = CreateTemporaryFilePath(outputPath, "encoded");
        var workingDirectoryPath = ResolveCodecWorkingDirectory(request, outputPath);
        var encryptionResult = await encryptor.EncryptAsync(
            new AssetBundleEncryptionRequestDto(
                SourcePath: standardBundlePath,
                OutputPath: codecOutputPath,
                WorkspaceKey: string.IsNullOrWhiteSpace(request.WorkspaceKey)
                    ? request.WorkspaceId.Value
                    : request.WorkspaceKey,
                WorkspaceDataRootPath: string.IsNullOrWhiteSpace(request.WorkspaceDataRootPath)
                    ? Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory
                    : request.WorkspaceDataRootPath,
                BundleCacheKey: request.BundleCacheKey,
                WorkingDirectoryPath: workingDirectoryPath,
                Parameters: request.SourceCodecParameters),
            cancellationToken);
        if (encryptionResult.IsCancelled)
        {
            TryDeleteTemporaryFile(codecOutputPath);
            return Result<WorkspaceBundleCodecOutput>.Cancelled();
        }

        if (encryptionResult.IsFailure || encryptionResult.Value is null)
        {
            TryDeleteTemporaryFile(codecOutputPath);
            return Result<WorkspaceBundleCodecOutput>.Failure(encryptionResult.Errors);
        }

        var resolvedPath = Path.GetFullPath(encryptionResult.Value.ResolvedBundlePath);
        if (!File.Exists(resolvedPath))
        {
            TryDeleteTemporaryFile(codecOutputPath);
            return Result<WorkspaceBundleCodecOutput>.Failure(
                ApplicationErrors.BundleExportCodecOutputMissing(codecId));
        }

        if (!PathsEqual(resolvedPath, codecOutputPath))
        {
            File.Copy(resolvedPath, codecOutputPath, overwrite: true);
            TryDeleteTemporaryFile(resolvedPath);
        }

        return Result<WorkspaceBundleCodecOutput>.Success(
            new WorkspaceBundleCodecOutput(
                PromotionFilePath: codecOutputPath,
                CodecTemporaryFilePath: codecOutputPath,
                OutputCodecId: decryptor.Descriptor.Id,
                OutputCodecVersion: decryptor.Descriptor.Version));
    }

    private static string ResolveCodecWorkingDirectory(
        WorkspaceBundleExportRequestDto request,
        string outputPath)
    {
        var root = string.IsNullOrWhiteSpace(request.WorkspaceDataRootPath)
            ? Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory
            : Path.GetFullPath(request.WorkspaceDataRootPath);
        return Path.Combine(
            root,
            "staging",
            "exports",
            string.IsNullOrWhiteSpace(request.OperationId) ? "unknown" : request.OperationId,
            "bundle-codec");
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static string CreateTemporaryFilePath(string outputPath, string purpose)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        var fileName = Path.GetFileName(outputPath);
        return Path.Combine(
            string.IsNullOrWhiteSpace(outputDirectory) ? Environment.CurrentDirectory : outputDirectory,
            $".{fileName}.{Guid.NewGuid():N}.{purpose}.tmp");
    }

    private static async Task<WorkspaceBundleExportFileHashes> ComputeHashesAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var inputStream = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var md5 = IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.MD5);
        using var sha256 = IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 128];
        while (true)
        {
            var bytesRead = await inputStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (bytesRead == 0)
            {
                break;
            }

            md5.AppendData(buffer, 0, bytesRead);
            sha256.AppendData(buffer, 0, bytesRead);
        }

        return new WorkspaceBundleExportFileHashes(
            Convert.ToHexString(md5.GetHashAndReset()).ToLowerInvariant(),
            Convert.ToHexString(sha256.GetHashAndReset()).ToLowerInvariant());
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

    private sealed record WorkspaceBundleCodecOutput(
        string PromotionFilePath,
        string? CodecTemporaryFilePath,
        string? OutputCodecId,
        string? OutputCodecVersion)
    {
        public static WorkspaceBundleCodecOutput Standard(string path) =>
            new(path, null, null, null);
    }

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
