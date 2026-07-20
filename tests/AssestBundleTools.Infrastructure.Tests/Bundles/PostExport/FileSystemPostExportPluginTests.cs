using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.PostExport;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.PostExport;

internal static class FileSystemPostExportPluginTests
{
    public static void Catalog_ShouldRoundTripAndDeleteUserManagedPlugin()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        File.WriteAllBytes(Path.Combine(temporaryDirectory.DirectoryPath, "plugin.exe"), [0]);
        var store = new FileSystemPostExportPluginCatalogStore([temporaryDirectory.DirectoryPath]);

        store.SaveUserManagedPlugin(new PostExportPluginDefinitionDto(
            Id: "post-export.zip",
            DisplayName: "ZIP Packager",
            Version: "1.2.0",
            Description: "Packages exported bundles.",
            ExecutablePath: "plugin.exe",
            WorkingDirectoryPath: ".",
            ProcessArguments: ["--manifest", "{manifestPath}", "--output", "{outputDirectory}"]));

        var entry = TestAssert.NotNull(
            store.Load().SingleOrDefault(static candidate => candidate.Id == "post-export.zip"),
            "Saved post-export plugin should be loaded from the manifest directory.");
        TestAssert.Equal(ResourceProcessorStatus.Available, entry.Status, "An existing executable should make the plugin available.");
        TestAssert.Equal("1.2.0", entry.Version, "Plugin version should round-trip through JSON.");
        TestAssert.Equal("{manifestPath}", entry.ProcessArguments[1], "Manifest placeholder should round-trip unchanged.");

        store.DeleteUserManagedPlugin(entry.Id);

        TestAssert.False(store.Load().Any(), "Deleting the user-managed plugin should remove its manifest.");
    }

    public static async Task Launcher_ShouldReportProcessStarted_WithoutOwningPluginCompletion()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var manifestPath = Path.Combine(temporaryDirectory.DirectoryPath, "_abt-bundle-export-manifest.json");
        File.WriteAllText(manifestPath, "{}");
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var plugin = new PostExportPluginCatalogEntryDto(
            "post-export.test",
            "Test Post Export",
            "1.0.0",
            string.Empty,
            ResourceProcessorStatus.Available,
            IsReadOnly: true,
            ManifestFilePath: null,
            ExecutablePath: Path.Combine(systemDirectory, "cmd.exe"),
            WorkingDirectoryPath: systemDirectory,
            ProcessArguments: ["/d", "/c", "exit", "/b", "0", "{manifestPath}"]);
        var launcher = new FileSystemWorkspacePostExportPluginLauncher();

        var result = await launcher.LaunchAsync(new WorkspacePostExportPluginLaunchRequestDto(
            plugin,
            manifestPath,
            temporaryDirectory.DirectoryPath,
            new WorkspaceId("workspace-post-export"),
            "workspace-key"));

        TestAssert.True(result.IsSuccess, "Starting the plugin process should complete the app-side handoff.");
        TestAssert.Equal(
            WorkspacePostExportPluginLaunchStatusDto.Launched,
            result.Value?.Status,
            "The launcher must report process creation rather than external processing completion.");
    }

    public static async Task Launcher_ShouldFailBeforeProcessStart_WhenManifestIsMissing()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var plugin = new PostExportPluginCatalogEntryDto(
            "post-export.test",
            "Test Post Export",
            "1.0.0",
            string.Empty,
            ResourceProcessorStatus.Available,
            IsReadOnly: true,
            ManifestFilePath: null,
            ExecutablePath: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
            WorkingDirectoryPath: null,
            ProcessArguments: ["{manifestPath}"]);
        var launcher = new FileSystemWorkspacePostExportPluginLauncher();

        var result = await launcher.LaunchAsync(new WorkspacePostExportPluginLaunchRequestDto(
            plugin,
            Path.Combine(temporaryDirectory.DirectoryPath, "missing.json"),
            temporaryDirectory.DirectoryPath,
            new WorkspaceId("workspace-post-export"),
            "workspace-key"));

        TestAssert.True(result.IsFailure, "A missing export manifest should block plugin process creation.");
        TestAssert.Equal("Bundle.Save.PostExportPlugin.LaunchFailed", TestAssert.Single(result.Errors).Code, "Launch failure should be structured.");
    }
}
