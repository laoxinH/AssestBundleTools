using System.Security.Cryptography;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Replacement;

/// <summary>
/// 基于文件系统读取批量替换来源文件状态。
/// </summary>
public sealed class FileSystemWorkspaceBatchReplacementSourceFileProbe : IWorkspaceBatchReplacementSourceFileProbe
{
    private const string HashAlgorithm = "MD5";

    public async Task<Result<WorkspaceBatchReplacementSourceFileSnapshotDto>> GetSnapshotAsync(
        string sourceRootDirectory,
        string relativePath,
        bool computeHash,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Cancelled();
        }

        try
        {
            var root = Path.GetFullPath(sourceRootDirectory.Trim());
            var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Failure(
                    ApplicationErrors.BatchReplacementSourceFileReadFailed(relativePath, "PathEscapesRoot"));
            }

            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                return Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Success(
                    new WorkspaceBatchReplacementSourceFileSnapshotDto(fullPath, false, null, null, null));
            }

            string? hash = null;
            if (computeHash)
            {
                await using var stream = new FileStream(
                    fullPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite);
                var hashBytes = await MD5.HashDataAsync(stream, cancellationToken);
                hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }

            return Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Success(
                new WorkspaceBatchReplacementSourceFileSnapshotDto(
                    fullPath,
                    true,
                    fileInfo.Length,
                    computeHash ? HashAlgorithm : null,
                    hash));
        }
        catch (OperationCanceledException)
        {
            return Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Result<WorkspaceBatchReplacementSourceFileSnapshotDto>.Failure(
                ApplicationErrors.BatchReplacementSourceFileReadFailed(relativePath, exception.GetType().Name));
        }
    }
}
