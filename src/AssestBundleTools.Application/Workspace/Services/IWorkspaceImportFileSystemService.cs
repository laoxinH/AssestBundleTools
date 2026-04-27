using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 为工作区导入链路提供最小必要的文件系统探测能力。
/// </summary>
/// <remarks>
/// 应用层只依赖这个技术端口，不直接访问 <c>File</c>、<c>Directory</c> 或 <c>FileInfo</c>，
/// 具体文件系统实现统一下沉到 Infrastructure。
/// </remarks>
public interface IWorkspaceImportFileSystemService
{
    WorkspaceImportPathKind ResolvePathKind(string path);

    WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath);
}
