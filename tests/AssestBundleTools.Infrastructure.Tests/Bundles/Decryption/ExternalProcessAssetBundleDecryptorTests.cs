using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;

internal static class ExternalProcessAssetBundleDecryptorTests
{
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
                ]
            },
            manifestDirectoryPath);
    }
}
