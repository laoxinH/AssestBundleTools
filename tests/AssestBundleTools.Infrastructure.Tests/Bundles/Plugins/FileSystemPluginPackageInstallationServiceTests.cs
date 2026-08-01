using System.IO.Compression;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Bundles.Plugins;
using AssestBundleTools.Infrastructure.Bundles.PostExport;
using AssestBundleTools.Infrastructure.Bundles.Processing;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Plugins;

internal static class FileSystemPluginPackageInstallationServiceTests
{
    public static void ManualCatalogSaves_ShouldWriteExplicitKindsForAllPluginTypes()
    {
        using var scope = new TemporaryDirectoryScope();
        var codecRoot = Path.Combine(scope.DirectoryPath, "manual", "codecs");
        var processorRoot = Path.Combine(scope.DirectoryPath, "manual", "processors");
        var postExportRoot = Path.Combine(scope.DirectoryPath, "manual", "post-export");
        var codecStore = new FileSystemAssetBundleDecryptorCatalogStore([codecRoot]);
        var processorStore = new FileSystemResourceProcessorCatalogStore([processorRoot]);
        var postExportStore = new FileSystemPostExportPluginCatalogStore([postExportRoot]);
        codecStore.SaveUserManagedDecryptor(new AssetBundleDecryptorDefinitionDto(
            "manual.codec",
            "Manual Codec",
            "1.0.0",
            string.Empty,
            true,
            false,
            "tool.exe",
            ".",
            15000,
            ["probe", "{inputPath}", "{resultPath}"],
            ["decrypt", "{inputPath}", "{outputPath}", "{resultPath}"],
            []));
        processorStore.SaveUserManagedProcessor(new ResourceProcessorDefinitionDto(
            "manual.processor",
            "Manual Processor",
            "1.0.0",
            string.Empty,
            ["Texture2D"],
            "tool.exe",
            ".",
            15000,
            ["{inputPath}", "{outputPath}", "{resultPath}"]));
        postExportStore.SaveUserManagedPlugin(new PostExportPluginDefinitionDto(
            "manual.post-export",
            "Manual Post Export",
            "1.0.0",
            string.Empty,
            "tool.exe",
            ".",
            ["{manifestPath}"]));

        TestAssert.Equal("assetBundleCodec", ReadKind(Directory.GetFiles(codecRoot, "*.json").Single()), "Codec form save should write the explicit kind.");
        TestAssert.Equal("resourceProcessor", ReadKind(Directory.GetFiles(processorRoot, "*.json").Single()), "Processor form save should write the explicit kind.");
        TestAssert.Equal("postExport", ReadKind(Directory.GetFiles(postExportRoot, "*.json").Single()), "Post-export form save should write the explicit kind.");
        TestAssert.Equal(1, ReadSchemaVersion(Directory.GetFiles(codecRoot, "*.json").Single()), "Codec form save should write schema version 1.");
        TestAssert.Equal(1, ReadSchemaVersion(Directory.GetFiles(processorRoot, "*.json").Single()), "Processor form save should write schema version 1.");
        TestAssert.Equal(1, ReadSchemaVersion(Directory.GetFiles(postExportRoot, "*.json").Single()), "Post-export form save should write schema version 1.");
    }

    public static async Task Catalogs_ShouldRejectManifestsWithoutExplicitKind()
    {
        using var scope = new TemporaryDirectoryScope();
        var environment = CreateEnvironment(scope.DirectoryPath);
        var codecRoot = environment.CodecRoot;
        Directory.CreateDirectory(codecRoot);
        await File.WriteAllTextAsync(
            Path.Combine(codecRoot, "legacy-codec.json"),
            CodecManifest("legacy.codec").Replace("\"kind\": \"assetBundleCodec\",", string.Empty, StringComparison.Ordinal));
        var processorRoot = Path.Combine(scope.DirectoryPath, "installed", "processors");
        Directory.CreateDirectory(processorRoot);
        await File.WriteAllTextAsync(
            Path.Combine(processorRoot, "legacy-processor.json"),
            ProcessorManifest("legacy.processor").Replace("\"kind\": \"resourceProcessor\",", string.Empty, StringComparison.Ordinal));
        var postExportRoot = Path.Combine(scope.DirectoryPath, "installed", "post-export");
        Directory.CreateDirectory(postExportRoot);
        await File.WriteAllTextAsync(
            Path.Combine(postExportRoot, "legacy-post-export.json"),
            PostExportManifest("legacy.post-export").Replace("\"kind\": \"postExport\",", string.Empty, StringComparison.Ordinal));

        TestAssert.False(
            new FileSystemAssetBundleDecryptorCatalogStore([codecRoot]).Load().Any(entry => entry.Definition.Id == "legacy.codec"),
            "Codec catalog should reject a manifest without explicit kind.");
        TestAssert.False(environment.ProcessorStore.Load().Any(entry => entry.Id == "legacy.processor"), "Processor catalog should reject a manifest without explicit kind.");
        TestAssert.False(environment.PostExportStore.Load().Any(entry => entry.Id == "legacy.post-export"), "Post-export catalog should reject a manifest without explicit kind.");
    }

    public static async Task ImportFolder_ShouldInstallAndInvokeAssetBundleCodecPythonPlugin()
    {
        using var scope = new TemporaryDirectoryScope();
        var environment = CreateEnvironment(scope.DirectoryPath);
        var packagePath = Path.Combine(scope.DirectoryPath, "packages", "codec");
        Directory.CreateDirectory(packagePath);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "codec.py"), CodecPythonScript);
        await File.WriteAllTextAsync(
            Path.Combine(packagePath, "plugin.json"),
            CodecManifest("test.package.codec"));

        var importResult = await environment.Installer.ImportAsync(packagePath, replaceExisting: false);

        TestAssert.True(importResult.IsSuccess, "A valid codec folder package should install.");
        TestAssert.Equal(PluginPackageImportStatusDto.Installed, importResult.Value!.Status, "First import should install directly.");
        var registry = new FileSystemAssetBundleDecryptorRegistry([environment.CodecRoot]);
        TestAssert.True(registry.TryGet("test.package.codec", out var codec), "Installed codec should be linked into the registry.");
        var sourcePath = Path.Combine(scope.DirectoryPath, "sample.ab");
        await File.WriteAllTextAsync(sourcePath, "codec-payload");

        var probe = await codec.ProbeAsync(new AssetBundleDecryptorProbeRequestDto(sourcePath));
        TestAssert.True(probe.IsMatch, "Imported codec probe should execute and match the sample.");
        var decrypt = await codec.DecryptAsync(new AssetBundleDecryptionRequestDto(
            sourcePath,
            "workspace",
            Path.Combine(scope.DirectoryPath, "workspace"),
            "bundle-key"));
        TestAssert.True(decrypt.IsSuccess, "Imported codec decrypt command should execute.");
        TestAssert.Equal("codec-payload", File.ReadAllText(decrypt.Value!.ResolvedBundlePath), "Codec output should preserve the scripted payload.");
    }

    public static async Task ImportZip_ShouldInstallAndInvokeResourceProcessorPythonPlugin()
    {
        using var scope = new TemporaryDirectoryScope();
        var environment = CreateEnvironment(scope.DirectoryPath);
        var packagePath = Path.Combine(scope.DirectoryPath, "packages", "processor");
        Directory.CreateDirectory(packagePath);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "processor.py"), ProcessorPythonScript);
        await File.WriteAllTextAsync(
            Path.Combine(packagePath, "plugin.json"),
            ProcessorManifest("test.package.processor"));
        var archivePath = Path.Combine(scope.DirectoryPath, "processor.zip");
        ZipFile.CreateFromDirectory(packagePath, archivePath);

        var importResult = await environment.Installer.ImportAsync(archivePath, replaceExisting: false);

        TestAssert.True(importResult.IsSuccess, "A valid processor ZIP package should install.");
        var processor = TestAssert.NotNull(
            environment.ProcessorStore.Load().SingleOrDefault(entry => entry.Id == "test.package.processor"),
            "Installed processor should be linked into its catalog.");
        var inputPath = Path.Combine(scope.DirectoryPath, "input.png");
        await File.WriteAllTextAsync(inputPath, "processor-payload");
        var execution = await new FileSystemResourceProcessorExecutor().ExecuteAsync(new WorkspaceResourceProcessorWorkItemDto(
            new WorkspaceId("workspace"),
            "workspace-key",
            scope.DirectoryPath,
            "operation",
            new BundleId("bundle"),
            "bundle.ab",
            "bundle-key",
            new AssetId("asset"),
            "texture",
            AssetType.Texture2D,
            "Texture2D",
            inputPath,
            Path.Combine(scope.DirectoryPath, "processor-staging"),
            processor));
        TestAssert.True(execution.IsSuccess, "Imported resource processor should execute through the real executor.");
        TestAssert.Equal("processor-payload-processed", File.ReadAllText(execution.Value!.OutputPath!), "Processor output should come from the imported script.");
        var installationDirectoryPath = Path.GetDirectoryName(processor.ManifestFilePath!)!;
        environment.ProcessorStore.DeleteUserManagedProcessor(processor.Id);
        TestAssert.False(Directory.Exists(installationDirectoryPath), "Deleting an imported plugin should remove its complete installation unit.");
    }

    public static async Task ImportZip_ShouldInstallAndLaunchPostExportExePlugin()
    {
        using var scope = new TemporaryDirectoryScope();
        var environment = CreateEnvironment(scope.DirectoryPath);
        var packagePath = Path.Combine(scope.DirectoryPath, "packages", "post-export");
        Directory.CreateDirectory(packagePath);
        CopyTestPluginRuntime(packagePath);
        await File.WriteAllTextAsync(
            Path.Combine(packagePath, "plugin.json"),
            PostExportManifest("test.package.post-export"));
        var archivePath = Path.Combine(scope.DirectoryPath, "post-export.zip");
        ZipFile.CreateFromDirectory(packagePath, archivePath);

        var importResult = await environment.Installer.ImportAsync(archivePath, replaceExisting: false);

        TestAssert.True(importResult.IsSuccess, "A valid post-export ZIP package should install.");
        var plugin = TestAssert.NotNull(
            environment.PostExportStore.Load().SingleOrDefault(entry => entry.Id == "test.package.post-export"),
            "Installed post-export plugin should be linked into its catalog.");
        var exportDirectory = Path.Combine(scope.DirectoryPath, "export");
        Directory.CreateDirectory(exportDirectory);
        var exportManifestPath = Path.Combine(exportDirectory, "_abt-bundle-export-manifest.json");
        await File.WriteAllTextAsync(exportManifestPath, "{}");
        var launch = await new FileSystemWorkspacePostExportPluginLauncher().LaunchAsync(
            new WorkspacePostExportPluginLaunchRequestDto(
                plugin,
                exportManifestPath,
                exportDirectory,
                new WorkspaceId("workspace"),
                "workspace-key"));
        TestAssert.True(launch.IsSuccess, "Imported post-export plugin should be launched through the real launcher.");
        var markerPath = Path.Combine(Path.GetDirectoryName(plugin.ManifestFilePath!)!, "invoked.txt");
        await WaitForFileAsync(markerPath);
        TestAssert.Equal(Path.GetFullPath(exportManifestPath), File.ReadAllText(markerPath), "Post-export plugin should receive the real export manifest path.");
        await WaitForFileUnlockAsync(Path.Combine(
            Path.GetDirectoryName(plugin.ManifestFilePath!)!,
            "AssestBundleTools.TestPlugin.dll"));
    }

    public static async Task Import_ShouldRequireConfirmationBeforeReplacingUserPlugin()
    {
        using var scope = new TemporaryDirectoryScope();
        var environment = CreateEnvironment(scope.DirectoryPath);
        var packagePath = Path.Combine(scope.DirectoryPath, "packages", "replace");
        Directory.CreateDirectory(packagePath);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "processor.py"), ProcessorPythonScript);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "plugin.json"), ProcessorManifest("test.package.replace", "1.0.0"));
        TestAssert.True((await environment.Installer.ImportAsync(packagePath, false)).IsSuccess, "Initial package should install.");
        await File.WriteAllTextAsync(Path.Combine(packagePath, "plugin.json"), ProcessorManifest("test.package.replace", "2.0.0"));

        var conflict = await environment.Installer.ImportAsync(packagePath, replaceExisting: false);

        TestAssert.True(conflict.IsSuccess, "Existing user plugin should produce a structured confirmation result.");
        TestAssert.Equal(PluginPackageImportStatusDto.OverwriteConfirmationRequired, conflict.Value!.Status, "Replacement should require confirmation.");
        TestAssert.Equal("1.0.0", environment.ProcessorStore.Load().Single(entry => entry.Id == "test.package.replace").Version, "Unconfirmed replacement must preserve the installed version.");
        var replaced = await environment.Installer.ImportAsync(packagePath, replaceExisting: true);
        TestAssert.True(replaced.IsSuccess, "Confirmed replacement should succeed.");
        TestAssert.Equal("2.0.0", environment.ProcessorStore.Load().Single(entry => entry.Id == "test.package.replace").Version, "Confirmed replacement should promote the new version.");
    }

    public static async Task Import_ShouldRejectMissingKindAndZipPathTraversal()
    {
        using var scope = new TemporaryDirectoryScope();
        var environment = CreateEnvironment(scope.DirectoryPath);
        var packagePath = Path.Combine(scope.DirectoryPath, "packages", "invalid");
        Directory.CreateDirectory(packagePath);
        await File.WriteAllTextAsync(Path.Combine(packagePath, "processor.py"), ProcessorPythonScript);
        await File.WriteAllTextAsync(
            Path.Combine(packagePath, "plugin.json"),
            ProcessorManifest("test.package.invalid").Replace("\"kind\": \"resourceProcessor\",", string.Empty, StringComparison.Ordinal));

        var missingKind = await environment.Installer.ImportAsync(packagePath, false);

        TestAssert.True(missingKind.IsFailure, "A manifest without kind should be rejected by the new package contract.");
        TestAssert.Equal("Plugin.Package.ManifestInvalid", TestAssert.Single(missingKind.Errors).Code, "Missing kind should use the manifest error contract.");
        await File.WriteAllTextAsync(
            Path.Combine(packagePath, "plugin.json"),
            ProcessorManifest("test.package.invalid").Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));
        var unsupportedSchema = await environment.Installer.ImportAsync(packagePath, false);
        TestAssert.True(unsupportedSchema.IsFailure, "An unsupported schema version should be rejected by the current package contract.");
        TestAssert.Equal("Plugin.Package.ManifestInvalid", TestAssert.Single(unsupportedSchema.Errors).Code, "Unsupported schema should use the manifest error contract.");
        var archivePath = Path.Combine(scope.DirectoryPath, "traversal.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../outside.txt");
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("invalid");
        }

        var traversal = await environment.Installer.ImportAsync(archivePath, false);
        TestAssert.True(traversal.IsFailure, "ZIP path traversal should be rejected.");
        TestAssert.False(File.Exists(Path.Combine(scope.DirectoryPath, "outside.txt")), "Rejected archive must not write outside staging.");
    }

    private static TestEnvironment CreateEnvironment(string rootPath)
    {
        var codecRoot = Path.Combine(rootPath, "installed", "codecs");
        var processorRoot = Path.Combine(rootPath, "installed", "processors");
        var postExportRoot = Path.Combine(rootPath, "installed", "post-export");
        var codecStore = new FileSystemAssetBundleDecryptorCatalogStore([codecRoot]);
        var processorStore = new FileSystemResourceProcessorCatalogStore([processorRoot]);
        var postExportStore = new FileSystemPostExportPluginCatalogStore([postExportRoot]);
        var installer = new FileSystemPluginPackageInstallationService(
            codecStore,
            processorStore,
            postExportStore,
            codecRoot,
            processorRoot,
            postExportRoot,
            Path.Combine(rootPath, "staging"));
        return new TestEnvironment(codecRoot, processorStore, postExportStore, installer);
    }

    private static async Task WaitForFileAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!File.Exists(path) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        TestAssert.True(File.Exists(path), "The launched plugin should create its marker file.");
    }

    private static async Task WaitForFileUnlockAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                return;
            }
            catch (IOException)
            {
                await Task.Delay(50);
            }
        }

        TestAssert.True(false, "The launched EXE plugin should release its runtime files after completing.");
    }

    private static string ReadKind(string manifestPath)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.GetProperty("kind").GetString() ?? string.Empty;
    }

    private static int ReadSchemaVersion(string manifestPath)
    {
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.GetProperty("schemaVersion").GetInt32();
    }

    private static void CopyTestPluginRuntime(string destinationDirectoryPath)
    {
        var pluginProjectRoot = Path.Combine(FindRepositoryRoot(), "tests", "AssestBundleTools.TestPlugin");
        var executablePath = Directory
            .EnumerateFiles(pluginProjectRoot, "AssestBundleTools.TestPlugin.exe", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        TestAssert.True(!string.IsNullOrWhiteSpace(executablePath), "The packaged EXE test plugin should be built before the integration test runs.");

        var runtimeDirectoryPath = Path.GetDirectoryName(executablePath!)!;
        foreach (var sourcePath in Directory.EnumerateFiles(runtimeDirectoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            File.Copy(sourcePath, Path.Combine(destinationDirectoryPath, Path.GetFileName(sourcePath)));
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssestBundleTools.Infrastructure")) &&
                Directory.Exists(Path.Combine(directory.FullName, "tests", "AssestBundleTools.TestPlugin")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found from the test output directory.");
    }

    private static string CodecManifest(string id) => $$"""
        {
          "schemaVersion": 1,
          "kind": "assetBundleCodec",
          "id": "{{id}}",
          "displayName": "Package Codec",
          "version": "1.0.0",
          "supportsAutoDetection": true,
          "isFallback": false,
          "executablePath": "python",
          "workingDirectoryPath": ".",
          "timeoutMs": 15000,
          "probeArguments": ["{manifestDirectory}\\codec.py", "probe", "--input", "{inputPath}", "--result", "{resultPath}"],
          "decryptArguments": ["{manifestDirectory}\\codec.py", "decrypt", "--input", "{inputPath}", "--output", "{outputPath}", "--result", "{resultPath}"],
          "encryptArguments": []
        }
        """;

    private static string ProcessorManifest(string id, string version = "1.0.0") => $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "displayName": "Package Processor",
          "version": "{{version}}",
          "kind": "resourceProcessor",
          "resourceKinds": ["Texture2D"],
          "executablePath": "python",
          "workingDirectoryPath": ".",
          "timeoutMs": 15000,
          "processArguments": ["processor.py", "--input", "{inputPath}", "--output", "{outputPath}", "--result", "{resultPath}"]
        }
        """;

    private static string PostExportManifest(string id) => $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "displayName": "Package Post Export",
          "version": "1.0.0",
          "kind": "postExport",
          "executablePath": ".\\AssestBundleTools.TestPlugin.exe",
          "workingDirectoryPath": ".",
          "processArguments": ["post-export", "{manifestPath}"]
        }
        """;

    private const string CodecPythonScript = """
        import argparse, json, shutil
        parser = argparse.ArgumentParser()
        parser.add_argument('mode')
        parser.add_argument('--input', required=True)
        parser.add_argument('--output')
        parser.add_argument('--result', required=True)
        args = parser.parse_args()
        if args.mode == 'probe':
            payload = {'schemaVersion': 1, 'isMatch': True, 'confidence': 100, 'formatId': 'test.package.codec'}
        else:
            shutil.copyfile(args.input, args.output)
            payload = {'schemaVersion': 1, 'isSuccess': True, 'outputPath': args.output}
        with open(args.result, 'w', encoding='utf-8') as stream:
            json.dump(payload, stream)
        """;

    private const string ProcessorPythonScript = """
        import argparse, json
        parser = argparse.ArgumentParser()
        parser.add_argument('--input', required=True)
        parser.add_argument('--output', required=True)
        parser.add_argument('--result', required=True)
        args = parser.parse_args()
        with open(args.input, 'r', encoding='utf-8') as stream:
            content = stream.read()
        with open(args.output, 'w', encoding='utf-8') as stream:
            stream.write(content + '-processed')
        with open(args.result, 'w', encoding='utf-8') as stream:
            json.dump({'schemaVersion': 1, 'status': 'succeeded', 'outputPath': args.output}, stream)
        """;

    private sealed record TestEnvironment(
        string CodecRoot,
        FileSystemResourceProcessorCatalogStore ProcessorStore,
        FileSystemPostExportPluginCatalogStore PostExportStore,
        FileSystemPluginPackageInstallationService Installer);
}
