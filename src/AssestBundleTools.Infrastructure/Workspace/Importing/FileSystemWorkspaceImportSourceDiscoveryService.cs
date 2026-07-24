using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Importing;

/// <summary>
/// 基于文件系统把工作区导入来源展开为真实文件列表。
/// </summary>
public sealed class FileSystemWorkspaceImportSourceDiscoveryService : IWorkspaceImportSourceDiscoveryService
{
    public IReadOnlyList<DiscoveredWorkspaceImportFileDto> Discover(
        IReadOnlyList<WorkspaceImportedSourceDto> importedSources,
        IReadOnlyList<string>? excludedSourcePaths = null,
        IReadOnlyList<string>? ignoredDirectoryPaths = null)
    {
        if (importedSources.Count == 0)
        {
            return Array.Empty<DiscoveredWorkspaceImportFileDto>();
        }

        var normalizedExcludedPaths = new HashSet<string>(
            (excludedSourcePaths ?? Array.Empty<string>())
            .Select(NormalizeAbsolutePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path)),
            StringComparer.OrdinalIgnoreCase);
        var normalizedIgnoredDirectoryPaths = (ignoredDirectoryPaths ?? Array.Empty<string>())
            .Select(NormalizeAbsolutePath)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var discoveredFiles = new Dictionary<string, DiscoveredWorkspaceImportFileDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var importedSource in importedSources)
        {
            if (string.IsNullOrWhiteSpace(importedSource.Path))
            {
                continue;
            }

            var normalizedPath = NormalizeAbsolutePath(importedSource.Path);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (IsWithinAnyDirectory(normalizedPath, normalizedIgnoredDirectoryPaths))
            {
                continue;
            }

            if (File.Exists(normalizedPath))
            {
                if (normalizedExcludedPaths.Contains(normalizedPath))
                {
                    continue;
                }

                discoveredFiles[normalizedPath] = new DiscoveredWorkspaceImportFileDto(
                    SourcePath: normalizedPath,
                    LogicalPath: BuildLogicalPathForFile(normalizedPath));
                continue;
            }

            if (!Directory.Exists(normalizedPath))
            {
                continue;
            }

            foreach (var filePath in EnumerateFilesExcludingDirectories(
                         normalizedPath,
                         normalizedIgnoredDirectoryPaths))
            {
                var normalizedFilePath = NormalizeAbsolutePath(filePath);
                if (string.IsNullOrWhiteSpace(normalizedFilePath))
                {
                    continue;
                }

                if (normalizedExcludedPaths.Contains(normalizedFilePath))
                {
                    continue;
                }

                discoveredFiles[normalizedFilePath] = new DiscoveredWorkspaceImportFileDto(
                    SourcePath: normalizedFilePath,
                    LogicalPath: BuildLogicalPathForDirectoryFile(normalizedPath, normalizedFilePath));
            }
        }

        return discoveredFiles.Values
            .OrderBy(file => file.LogicalPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> EnumerateFilesExcludingDirectories(
        string rootDirectoryPath,
        IReadOnlyList<string> ignoredDirectoryPaths)
    {
        var discoveredFiles = new List<string>();
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(rootDirectoryPath);

        while (pendingDirectories.TryPop(out var currentDirectoryPath))
        {
            if (IsWithinAnyDirectory(currentDirectoryPath, ignoredDirectoryPaths))
            {
                continue;
            }

            string[] filePaths;
            string[] directoryPaths;
            try
            {
                filePaths = Directory.GetFiles(currentDirectoryPath);
                directoryPaths = Directory.GetDirectories(currentDirectoryPath);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            discoveredFiles.AddRange(filePaths);
            foreach (var directoryPath in directoryPaths)
            {
                var normalizedDirectoryPath = NormalizeAbsolutePath(directoryPath);
                if (string.IsNullOrWhiteSpace(normalizedDirectoryPath) ||
                    IsWithinAnyDirectory(normalizedDirectoryPath, ignoredDirectoryPaths) ||
                    IsReparsePoint(normalizedDirectoryPath))
                {
                    continue;
                }

                pendingDirectories.Push(normalizedDirectoryPath);
            }
        }

        return discoveredFiles;
    }

    private static bool IsWithinAnyDirectory(
        string candidatePath,
        IReadOnlyList<string> directoryPaths) =>
        directoryPaths.Any(directoryPath => IsSameOrDescendantPath(candidatePath, directoryPath));

    private static bool IsSameOrDescendantPath(string candidatePath, string directoryPath)
    {
        if (string.Equals(candidatePath, directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedDirectoryPrefix = directoryPath.TrimEnd(
                                            Path.DirectorySeparatorChar,
                                            Path.AltDirectorySeparatorChar) +
                                        Path.DirectorySeparatorChar;
        return candidatePath.StartsWith(normalizedDirectoryPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReparsePoint(string directoryPath)
    {
        try
        {
            return (File.GetAttributes(directoryPath) & FileAttributes.ReparsePoint) != 0;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return true;
        }
    }

    private static string NormalizeAbsolutePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).Trim();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string BuildLogicalPathForFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var parentDirectoryName = Path.GetFileName(Path.GetDirectoryName(filePath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return string.IsNullOrWhiteSpace(parentDirectoryName)
            ? fileName
            : $"{parentDirectoryName}/{fileName}";
    }

    private static string BuildLogicalPathForDirectoryFile(string sourceDirectoryPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(sourceDirectoryPath, filePath)
            .Replace('\\', '/');
        var sourceDirectoryName = Path.GetFileName(sourceDirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return string.IsNullOrWhiteSpace(sourceDirectoryName)
            ? relativePath
            : $"{sourceDirectoryName}/{relativePath}";
    }
}
