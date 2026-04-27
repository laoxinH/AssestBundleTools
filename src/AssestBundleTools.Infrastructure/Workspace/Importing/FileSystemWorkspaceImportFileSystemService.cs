using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Importing;

/// <summary>
/// 基于本地文件系统为工作区导入链路提供路径类型判断与来源指纹读取。
/// </summary>
public sealed class FileSystemWorkspaceImportFileSystemService : IWorkspaceImportFileSystemService
{
    public WorkspaceImportPathKind ResolvePathKind(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (File.Exists(path))
        {
            return WorkspaceImportPathKind.File;
        }

        if (Directory.Exists(path))
        {
            return WorkspaceImportPathKind.Directory;
        }

        return WorkspaceImportPathKind.Unknown;
    }

    public WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fileInfo = new FileInfo(sourcePath);
        return new WorkspaceFileFingerprintDto(
            SourcePath: fileInfo.FullName,
            FileSizeInBytes: fileInfo.Exists ? fileInfo.Length : 0,
            LastWriteTimeUtc: fileInfo.Exists
                ? new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero)
                : DateTimeOffset.MinValue,
            ContentHash: null);
    }
}
