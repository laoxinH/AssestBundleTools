using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;

internal static class ExternalProcessAssetBundleDecryptorTests
{
    public static void RegistryAndCatalog_ShouldExposeBuiltInArknightsCodec_AsReadOnlyEncryptor()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var registry = new FileSystemAssetBundleDecryptorRegistry([temporaryDirectory.DirectoryPath]);
        var catalog = new FileSystemAssetBundleDecryptorCatalogStore([temporaryDirectory.DirectoryPath]);

        TestAssert.True(
            registry.TryGet(AssetBundleDecryptorIds.BuiltInArknightsLz4Ak, out var decryptor),
            "Registry should always expose the built-in Arknights codec.");
        TestAssert.True(decryptor.Descriptor.SupportsAutoDetection, "Built-in Arknights codec should participate in auto detection.");
        TestAssert.True(decryptor.Descriptor.SupportsEncryption, "Built-in Arknights codec should advertise export encoding.");
        TestAssert.True(decryptor is AssestBundleTools.Application.Bundles.Services.IAssetBundleEncryptor, "Built-in Arknights codec should implement the optional encryptor port.");

        var entry = catalog.Load().Single(item =>
            string.Equals(
                item.Definition.Id,
                AssetBundleDecryptorIds.BuiltInArknightsLz4Ak,
                StringComparison.OrdinalIgnoreCase));
        TestAssert.True(entry.IsBuiltIn, "Built-in Arknights catalog entry should be marked built-in.");
        TestAssert.True(entry.IsReadOnly, "Built-in Arknights catalog entry should be read-only.");
        TestAssert.True(entry.Definition.EncryptArguments is { Count: > 0 }, "Built-in catalog entry should expose encrypt arguments.");
        var codecDirectoryPath = Path.Combine(temporaryDirectory.DirectoryPath, "codecs", "arknights");
        var resolvedPath = ExternalProcessAssetBundleDecryptorSupport.ResolveExecutablePath(
            entry.Definition.ExecutablePath,
            codecDirectoryPath);

        TestAssert.Equal(
            Path.GetFullPath(Path.Combine(codecDirectoryPath, "AssestBundleTools.BundleCodec.Arknights.exe")),
            resolvedPath,
            "Built-in codec executable should resolve from its application codec directory instead of PATH.");
    }

    public static void RegistryAndCatalog_ShouldExposeBuiltInCrossCoreCodec_AsReadOnlyEncryptor()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var registry = new FileSystemAssetBundleDecryptorRegistry([temporaryDirectory.DirectoryPath]);
        var catalog = new FileSystemAssetBundleDecryptorCatalogStore([temporaryDirectory.DirectoryPath]);

        TestAssert.True(
            registry.TryGet(AssetBundleDecryptorIds.BuiltInCrossCoreDuplicatedHeader, out var decryptor),
            "Registry should always expose the built-in CrossCore codec.");
        TestAssert.True(decryptor.Descriptor.SupportsAutoDetection, "Built-in CrossCore codec should participate in auto detection.");
        TestAssert.True(decryptor.Descriptor.SupportsEncryption, "Built-in CrossCore codec should advertise export encoding.");
        TestAssert.True(decryptor is AssestBundleTools.Application.Bundles.Services.IAssetBundleEncryptor, "Built-in CrossCore codec should implement the optional encryptor port.");

        var entry = catalog.Load().Single(item =>
            string.Equals(
                item.Definition.Id,
                AssetBundleDecryptorIds.BuiltInCrossCoreDuplicatedHeader,
                StringComparison.OrdinalIgnoreCase));
        TestAssert.True(entry.IsBuiltIn, "Built-in CrossCore catalog entry should be marked built-in.");
        TestAssert.True(entry.IsReadOnly, "Built-in CrossCore catalog entry should be read-only.");
        TestAssert.True(entry.Definition.EncryptArguments is { Count: > 0 }, "Built-in CrossCore catalog entry should expose encrypt arguments.");
        var codecDirectoryPath = Path.Combine(temporaryDirectory.DirectoryPath, "codecs", "crosscore");
        var resolvedPath = ExternalProcessAssetBundleDecryptorSupport.ResolveExecutablePath(
            entry.Definition.ExecutablePath,
            codecDirectoryPath);

        TestAssert.Equal(
            Path.GetFullPath(Path.Combine(codecDirectoryPath, "AssestBundleTools.BundleCodec.CrossCore.exe")),
            resolvedPath,
            "Built-in CrossCore executable should resolve from its application codec directory instead of PATH.");
    }

    public static void DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(temporaryDirectory.DirectoryPath, "Bundles", "hero.ab");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "encrypted-bundle");

        var decryptor = CreateDecryptor(temporaryDirectory.DirectoryPath);
        var request = new AssetBundleDecryptionRequestDto(
            SourcePath: sourcePath,
            WorkspaceKey: "workspace-a",
            WorkspaceDataRootPath: temporaryDirectory.DirectoryPath,
            BundleCacheKey: "bundle-cache-001");

        var result = decryptor.DecryptAsync(request).GetAwaiter().GetResult();

        TestAssert.True(result.IsSuccess, "External decryptor should succeed for the test script.");
        var decryptionResult = TestAssert.NotNull(result.Value, "Successful decryption should return a value.");
        var expectedDirectoryPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "staging",
            "decrypt",
            "workspace-a",
            "decryptor.test",
            "bundle-cache-001");
        var actualDirectoryPath = Path.GetDirectoryName(decryptionResult.ResolvedBundlePath);
        TestAssert.Equal(
            Path.GetFullPath(expectedDirectoryPath),
            Path.GetFullPath(actualDirectoryPath!),
            "Workspace staging path should include workspace, decryptor and bundle cache key segments.");
        TestAssert.True(File.Exists(decryptionResult.ResolvedBundlePath), "Decrypted output file should exist inside the bundle-scoped staging directory.");
    }

    public static void DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(temporaryDirectory.DirectoryPath, "Bundles", "villain.ab");
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, "encrypted-bundle");
        var customWorkingDirectoryPath = Path.Combine(temporaryDirectory.DirectoryPath, "custom-root");

        var decryptor = CreateDecryptor(temporaryDirectory.DirectoryPath);
        var request = new AssetBundleDecryptionRequestDto(
            SourcePath: sourcePath,
            WorkspaceKey: "workspace-b",
            WorkspaceDataRootPath: temporaryDirectory.DirectoryPath,
            BundleCacheKey: "bundle-cache-002",
            WorkingDirectoryPath: customWorkingDirectoryPath);

        var result = decryptor.DecryptAsync(request).GetAwaiter().GetResult();

        TestAssert.True(result.IsSuccess, "External decryptor should succeed for the custom working directory test script.");
        var decryptionResult = TestAssert.NotNull(result.Value, "Successful decryption should return a value.");
        var expectedDirectoryPath = Path.Combine(
            customWorkingDirectoryPath,
            "workspace-b",
            "decryptor.test",
            "bundle-cache-002");
        var actualDirectoryPath = Path.GetDirectoryName(decryptionResult.ResolvedBundlePath);
        TestAssert.Equal(
            Path.GetFullPath(expectedDirectoryPath),
            Path.GetFullPath(actualDirectoryPath!),
            "Custom working directory should still append bundle-scoped uniqueness segments.");
    }

    public static void EncryptAsync_ShouldRunOptionalEncryptCommand_AndReturnRequestedOutput()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(temporaryDirectory.DirectoryPath, "standard.bundle");
        var outputPath = Path.Combine(temporaryDirectory.DirectoryPath, "encoded.bundle");
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        File.WriteAllText(sourcePath, "standard-bundle");
        var decryptor = CreateDecryptor(temporaryDirectory.DirectoryPath);

        var result = decryptor.EncryptAsync(new AssetBundleEncryptionRequestDto(
            SourcePath: sourcePath,
            OutputPath: outputPath,
            WorkspaceKey: "workspace-c",
            WorkspaceDataRootPath: temporaryDirectory.DirectoryPath,
            BundleCacheKey: "bundle-cache-003",
            Parameters: new Dictionary<string, string>
            {
                ["test.value"] = "exact-prefix"
            })).GetAwaiter().GetResult();

        TestAssert.True(result.IsSuccess, "Optional external encrypt command should succeed.");
        var encryptionResult = TestAssert.NotNull(result.Value, "Successful encryption should return a value.");
        TestAssert.Equal(
            Path.GetFullPath(outputPath),
            Path.GetFullPath(encryptionResult.ResolvedBundlePath),
            "Encrypt command should return the requested output path.");
        TestAssert.Equal(
            "standard-bundle",
            File.ReadAllText(outputPath),
            "Encrypt command should receive the standard bundle input.");
    }

    private static ExternalProcessAssetBundleDecryptor CreateDecryptor(string manifestDirectoryPath)
    {
        return new ExternalProcessAssetBundleDecryptor(
            new AssetBundleExternalProcessDecryptorManifest
            {
                Id = "decryptor.test",
                DisplayName = "Test Decryptor",
                Version = "1.0.0",
                ExecutablePath = "powershell",
                DecryptArguments =
                [
                    "-NoProfile",
                    "-Command",
                    "$output = '{outputPath}'; $result = '{resultPath}'; Set-Content -LiteralPath $output -Value 'bundle'; Set-Content -LiteralPath $result -Value '{\"isSuccess\":true}'"
                ],
                EncryptArguments =
                [
                    "-NoProfile",
                    "-Command",
                    "$inputPath = '{inputPath}'; $output = '{outputPath}'; $result = '{resultPath}'; $parameter = '{parameter:test.value}'; if ($parameter -ne 'exact-prefix') { exit 9 }; Copy-Item -LiteralPath $inputPath -Destination $output; Set-Content -LiteralPath $result -Value '{\"isSuccess\":true}'"
                ]
            },
            manifestDirectoryPath);
    }
}
