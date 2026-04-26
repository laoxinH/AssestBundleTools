using System.Security.Cryptography;
using System.Text;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责为工作区目录项生成稳定键值。
/// </summary>
/// <remarks>
/// 工作区目录和元数据文件都依赖同一个稳定键，
/// 这样可以避免直接把原始路径当文件名带来的兼容性问题。
/// </remarks>
public static class WorkspaceCatalogKeyFactory
{
    public static string NormalizeSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        return sourcePath.Trim().TrimEnd('\\', '/');
    }

    public static string CreateWorkspaceKey(string sourcePath)
    {
        var normalizedSourcePath = NormalizeSourcePath(sourcePath);
        if (string.IsNullOrWhiteSpace(normalizedSourcePath))
        {
            return string.Empty;
        }

        var sourceBytes = Encoding.UTF8.GetBytes(normalizedSourcePath.ToUpperInvariant());
        var hashBytes = SHA256.HashData(sourceBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// 允许在来源路径为空时回退到工作区数据目录生成稳定键值。
    /// 这样“空工作区、后续再导入资源”的模式也能被最近工作区和元数据系统稳定识别。
    /// </summary>
    public static string CreateWorkspaceKey(string? sourcePath, string? fallbackPath)
    {
        var workspaceKey = CreateWorkspaceKey(sourcePath ?? string.Empty);
        return !string.IsNullOrWhiteSpace(workspaceKey)
            ? workspaceKey
            : CreateWorkspaceKey(fallbackPath ?? string.Empty);
    }
}
