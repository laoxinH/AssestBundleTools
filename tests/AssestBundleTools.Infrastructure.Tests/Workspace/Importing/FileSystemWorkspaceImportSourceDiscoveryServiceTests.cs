using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Importing;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Importing;

internal static class FileSystemWorkspaceImportSourceDiscoveryServiceTests
{
    public static void Discover_ShouldExcludeWorkspaceDataRootWithoutExcludingPathPrefixSiblings()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var importRootPath = Path.Combine(temporaryDirectory.DirectoryPath, "demo");
        var bundleDirectoryPath = Path.Combine(importRootPath, "bundles");
        var workspaceDataRootPath = Path.Combine(importRootPath, "workspace-data");
        var recognitionCachePath = Path.Combine(workspaceDataRootPath, "cache", "import-recognition");
        var prefixSiblingPath = Path.Combine(importRootPath, "workspace-data-backup");
        Directory.CreateDirectory(bundleDirectoryPath);
        Directory.CreateDirectory(recognitionCachePath);
        Directory.CreateDirectory(prefixSiblingPath);

        var rootBundlePath = Path.Combine(importRootPath, "root.ab");
        var nestedBundlePath = Path.Combine(bundleDirectoryPath, "nested.ab");
        var generatedCachePath = Path.Combine(recognitionCachePath, "generated.json");
        var prefixSiblingBundlePath = Path.Combine(prefixSiblingPath, "backup.ab");
        File.WriteAllText(rootBundlePath, "root");
        File.WriteAllText(nestedBundlePath, "nested");
        File.WriteAllText(generatedCachePath, "generated");
        File.WriteAllText(prefixSiblingBundlePath, "backup");

        var service = new FileSystemWorkspaceImportSourceDiscoveryService();
        var discoveredFiles = service.Discover(
            [CreateDirectorySource(importRootPath), CreateFileSource(generatedCachePath)],
            ignoredDirectoryPaths: [workspaceDataRootPath]);

        TestAssert.Equal(3, discoveredFiles.Count, "Only files outside the workspace data root should be discovered.");
        TestAssert.True(discoveredFiles.Any(file => PathEquals(file.SourcePath, rootBundlePath)), "Root bundle should remain discoverable.");
        TestAssert.True(discoveredFiles.Any(file => PathEquals(file.SourcePath, nestedBundlePath)), "Nested bundle should remain discoverable.");
        TestAssert.True(discoveredFiles.Any(file => PathEquals(file.SourcePath, prefixSiblingBundlePath)), "A directory sharing only the reserved-root prefix should not be excluded.");
        TestAssert.False(discoveredFiles.Any(file => PathEquals(file.SourcePath, generatedCachePath)), "Workspace-generated cache files must never become import candidates.");
    }

    private static WorkspaceImportedSourceDto CreateDirectorySource(string path) =>
        new(
            Path: path,
            ImportOrigin: WorkspaceImportOriginKind.BundleDirectory,
            PathKind: WorkspaceImportPathKind.Directory,
            RecordedAtUtc: DateTimeOffset.UtcNow);

    private static WorkspaceImportedSourceDto CreateFileSource(string path) =>
        new(
            Path: path,
            ImportOrigin: WorkspaceImportOriginKind.BundleFiles,
            PathKind: WorkspaceImportPathKind.File,
            RecordedAtUtc: DateTimeOffset.UtcNow);

    private static bool PathEquals(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
}
