using System.IO;
using System.Security.Cryptography;
using System.Text;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Workspace.Internal;

/// <summary>
/// 统一处理工作区导入来源路径的标准化、缓存键生成与覆盖判定。
/// </summary>
internal static class WorkspaceImportPathUtility
{
    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = path.Trim();
        try
        {
            normalizedPath = Path.GetFullPath(normalizedPath);
        }
        catch (Exception)
        {
            normalizedPath = WorkspaceCatalogKeyFactory.NormalizeSourcePath(normalizedPath);
        }

        return WorkspaceCatalogKeyFactory.NormalizeSourcePath(normalizedPath);
    }

    public static string CreateBundleCacheKey(string sourcePath)
    {
        var normalizedPath = NormalizePath(sourcePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string CreateBundleId(string sourcePath)
    {
        var bundleCacheKey = CreateBundleCacheKey(sourcePath);
        return string.IsNullOrWhiteSpace(bundleCacheKey)
            ? string.Empty
            : $"bundle.{bundleCacheKey}";
    }

    public static bool IsCoveredByImportedSource(
        WorkspaceImportedSourceDto importedSource,
        string bundleSourcePath)
    {
        ArgumentNullException.ThrowIfNull(importedSource);

        var normalizedImportedPath = NormalizePath(importedSource.Path);
        var normalizedBundlePath = NormalizePath(bundleSourcePath);
        if (string.IsNullOrWhiteSpace(normalizedImportedPath) ||
            string.IsNullOrWhiteSpace(normalizedBundlePath))
        {
            return false;
        }

        if (string.Equals(normalizedImportedPath, normalizedBundlePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (importedSource.PathKind != WorkspaceImportPathKind.Directory)
        {
            return false;
        }

        return IsPathInsideDirectory(normalizedBundlePath, normalizedImportedPath);
    }

    private static bool IsPathInsideDirectory(string path, string directoryPath)
    {
        try
        {
            var relativePath = Path.GetRelativePath(directoryPath, path);
            return !string.IsNullOrWhiteSpace(relativePath) &&
                   !relativePath.StartsWith("..", StringComparison.OrdinalIgnoreCase) &&
                   !Path.IsPathRooted(relativePath);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
