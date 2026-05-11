using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Extraction.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Infrastructure.Workspace.Extraction;

/// <summary>
/// 基于文件系统写出资源提取 payload。
/// </summary>
public sealed class FileSystemAssetExtractionFileStore : IAssetExtractionFileStore
{
    public async Task<Result<AssetExtractionWriteResultDto>> WriteAsync(
        AssetExtractionWriteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return Result<AssetExtractionWriteResultDto>.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(request.WorkspaceDataRootPath))
        {
            return Result<AssetExtractionWriteResultDto>.Failure(ApplicationErrors.WorkspaceOpenDataRootPathRequired());
        }

        if (string.IsNullOrWhiteSpace(request.SuggestedOutputPath))
        {
            return Result<AssetExtractionWriteResultDto>.Failure(
                ApplicationErrors.AssetExtractionWriteFailed(string.Empty));
        }

        var temporaryFilePath = string.Empty;
        var outputPath = string.Empty;
        try
        {
            outputPath = ResolveOutputPath(
                request.WorkspaceDataRootPath,
                request.SuggestedOutputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath) ?? request.WorkspaceDataRootPath.Trim();
            Directory.CreateDirectory(outputDirectory);

            temporaryFilePath = Path.Combine(
                outputDirectory,
                $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");
            if (!string.IsNullOrWhiteSpace(request.SourcePayloadFilePath) &&
                File.Exists(request.SourcePayloadFilePath))
            {
                await CopyFileAsync(request.SourcePayloadFilePath, temporaryFilePath, cancellationToken);
            }
            else if (request.Content is { Length: > 0 })
            {
                await File.WriteAllBytesAsync(temporaryFilePath, request.Content, cancellationToken);
            }
            else
            {
                return Result<AssetExtractionWriteResultDto>.Failure(
                    ApplicationErrors.AssetExtractionPayloadUnavailable(
                        new AssetId(Path.GetFileNameWithoutExtension(outputPath))));
            }

            File.Move(temporaryFilePath, outputPath, overwrite: true);
            temporaryFilePath = string.Empty;

            var fileInfo = new FileInfo(outputPath);
            return Result<AssetExtractionWriteResultDto>.Success(
                new AssetExtractionWriteResultDto(
                    OutputPath: outputPath,
                    OutputDirectory: outputDirectory,
                    OutputSizeInBytes: fileInfo.Exists ? fileInfo.Length : 0,
                    OutputFileExists: fileInfo.Exists));
        }
        catch (OperationCanceledException)
        {
            DeleteTemporaryFile(temporaryFilePath);
            return Result<AssetExtractionWriteResultDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            DeleteTemporaryFile(temporaryFilePath);
            return Result<AssetExtractionWriteResultDto>.Failure(
                ApplicationErrors.AssetExtractionWriteFailed(
                    outputPath,
                    exception.GetType().Name));
        }
    }

    private static async Task CopyFileAsync(
        string sourceFilePath,
        string targetFilePath,
        CancellationToken cancellationToken)
    {
        await using var sourceStream = new FileStream(
            sourceFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        await using var targetStream = new FileStream(
            targetFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await sourceStream.CopyToAsync(targetStream, cancellationToken);
    }

    private static string ResolveOutputPath(string workspaceDataRootPath, string suggestedOutputPath)
    {
        var sanitizedPath = SanitizePath(suggestedOutputPath);
        if (Path.IsPathRooted(suggestedOutputPath))
        {
            return Path.GetFullPath(sanitizedPath);
        }

        var workspaceRootPath = Path.GetFullPath(workspaceDataRootPath.Trim());
        var outputPath = Path.GetFullPath(Path.Combine(workspaceRootPath, sanitizedPath));
        if (!IsSameOrChildPath(workspaceRootPath, outputPath))
        {
            throw new ArgumentException("Extraction output path must stay inside the workspace data root.");
        }

        return outputPath;
    }

    private static bool IsSameOrChildPath(string rootPath, string candidatePath)
    {
        var normalizedRoot = EnsureTrailingSeparator(Path.GetFullPath(rootPath));
        var normalizedCandidate = Path.GetFullPath(candidatePath);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : $"{path}{Path.DirectorySeparatorChar}";

    private static string SanitizePath(string path)
    {
        var root = Path.IsPathRooted(path) ? Path.GetPathRoot(path) : null;
        var remainder = string.IsNullOrEmpty(root) ? path : path[root.Length..];
        var parts = remainder
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
            .Where(static part => part is not "." and not "..")
            .Select(SanitizeFileSegment)
            .ToArray();

        var relativePath = parts.Length == 0
            ? "asset.bin"
            : Path.Combine(parts);
        return string.IsNullOrEmpty(root)
            ? relativePath
            : Path.Combine(root, relativePath);
    }

    private static string SanitizeFileSegment(string segment)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedChars = segment.Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(sanitizedChars).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized)
            ? "asset"
            : sanitized;
    }

    private static void DeleteTemporaryFile(string temporaryFilePath)
    {
        if (string.IsNullOrWhiteSpace(temporaryFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(temporaryFilePath))
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
