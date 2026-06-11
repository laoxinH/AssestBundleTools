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

    public Task<Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>> EnumerateFilesAsync(
        string sourceRootDirectory,
        int maxDepth,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>.Cancelled());
        }

        try
        {
            var root = Path.GetFullPath(sourceRootDirectory.Trim());
            if (!Directory.Exists(root))
            {
                return Task.FromResult(Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>.Failure(
                    ApplicationErrors.BatchReplacementSourceFileReadFailed(sourceRootDirectory, "DirectoryNotFound")));
            }

            var entries = new List<WorkspaceBatchReplacementSourceFileEntryDto>();
            EnumerateFiles(root, root, Math.Max(0, maxDepth), entries, cancellationToken);
            return Task.FromResult(Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>.Success(entries));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>.Cancelled());
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return Task.FromResult(Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>.Failure(
                ApplicationErrors.BatchReplacementSourceFileReadFailed(sourceRootDirectory, exception.GetType().Name)));
        }
    }

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

    private static void EnumerateFiles(
        string root,
        string directory,
        int remainingDepth,
        List<WorkspaceBatchReplacementSourceFileEntryDto> entries,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var filePath in Directory.EnumerateFiles(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInfo = new FileInfo(filePath);
            if ((fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                continue;
            }

            var relativePath = Path.GetRelativePath(root, filePath).Replace(Path.DirectorySeparatorChar, '/');
            entries.Add(new WorkspaceBatchReplacementSourceFileEntryDto(relativePath, filePath, fileInfo.Length));
        }

        if (remainingDepth <= 0)
        {
            return;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryInfo = new DirectoryInfo(childDirectory);
            if ((directoryInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                continue;
            }

            EnumerateFiles(root, childDirectory, remainingDepth - 1, entries, cancellationToken);
        }
    }
}
