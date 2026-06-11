using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 读取批量替换外部来源文件的大小和哈希。
/// </summary>
public interface IWorkspaceBatchReplacementSourceFileProbe
{
    Task<Result<IReadOnlyList<WorkspaceBatchReplacementSourceFileEntryDto>>> EnumerateFilesAsync(
        string sourceRootDirectory,
        int maxDepth,
        CancellationToken cancellationToken = default);

    Task<Result<WorkspaceBatchReplacementSourceFileSnapshotDto>> GetSnapshotAsync(
        string sourceRootDirectory,
        string relativePath,
        bool computeHash,
        CancellationToken cancellationToken = default);
}
