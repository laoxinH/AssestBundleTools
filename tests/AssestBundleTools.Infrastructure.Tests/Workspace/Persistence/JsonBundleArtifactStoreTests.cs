using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonBundleArtifactStoreTests
{
    public static void Save_ShouldMaterializeDerivedBundle_AndRoundTripManifest()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var sourceBundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "staging", "decrypted.ab");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceBundlePath)!);
        File.WriteAllBytes(sourceBundlePath, [0x41, 0x42, 0x43]);
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        var sourceFingerprint = CreateFingerprint(@"C:\Game\hero.ab", 128);

        var manifest = store.Save(new BundleArtifactSaveRequestDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-a",
            SourceFingerprint: sourceFingerprint,
            DecryptorId: "decryptor-a",
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            ResolvedBundlePath: sourceBundlePath,
            ProducedDerivedFile: true,
            ResolvedBundleFingerprint: CreateFingerprint(sourceBundlePath, 3),
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero)));

        var restoredManifest = TestAssert.NotNull(
            store.Load("workspace-a", "bundle-cache-a"),
            "Saved bundle artifact manifest should be readable.");
        TestAssert.True(restoredManifest.IsMaterialized, "Derived bundle should be materialized into cache/bundles.");
        TestAssert.Equal(manifest.ResolvedBundlePath, restoredManifest.ResolvedBundlePath, "Manifest path should round-trip.");
        TestAssert.Equal(manifest.CreatedAtUtc, restoredManifest.LastAccessedAtUtc, "Saving artifact manifest should initialize last access time.");
        TestAssert.True(
            restoredManifest.ResolvedBundlePath.EndsWith(
                Path.Combine("cache", "bundles", "workspace-a", "bundle-cache-a", "normalized.bundle"),
                StringComparison.OrdinalIgnoreCase),
            "Derived bundle should be copied to the standardized bundle cache path.");
        TestAssert.True(File.Exists(restoredManifest.ResolvedBundlePath), "Materialized bundle file should exist.");
        TestAssert.Equal(3, new FileInfo(restoredManifest.ResolvedBundlePath).Length, "Materialized bundle bytes should match the source.");
        TestAssert.Equal(
            restoredManifest.ResolvedBundlePath,
            store.ResolveBundlePath(restoredManifest),
            "Stored materialized bundle path should resolve while the file exists.");
    }

    public static void Save_ShouldKeepPassThroughBundlePath_InManifestOnly()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var sourceBundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "source.ab");
        File.WriteAllBytes(sourceBundlePath, [0x10, 0x20]);
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);

        var manifest = store.Save(new BundleArtifactSaveRequestDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-pass-through",
            SourceFingerprint: CreateFingerprint(sourceBundlePath, 2),
            DecryptorId: "builtin.pass-through",
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            ResolvedBundlePath: sourceBundlePath,
            ProducedDerivedFile: false,
            ResolvedBundleFingerprint: CreateFingerprint(sourceBundlePath, 2),
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 5, 0, TimeSpan.Zero)));

        TestAssert.False(manifest.IsMaterialized, "Pass-through bundles should not be copied during the first cache pass.");
        TestAssert.Equal(Path.GetFullPath(sourceBundlePath), manifest.ResolvedBundlePath, "Manifest should reference the readable source path.");
        TestAssert.False(
            File.Exists(Path.Combine(
                temporaryDirectory.DirectoryPath,
                "cache",
                "bundles",
                "workspace-a",
                "bundle-cache-pass-through",
                "normalized.bundle")),
            "Pass-through cache should not create normalized.bundle.");
        TestAssert.Equal(
            manifest.ResolvedBundlePath,
            store.ResolveBundlePath(manifest),
            "Pass-through manifest should resolve to the original file while it exists.");
    }

    public static void UpdateLastAccessed_ShouldPersistManifestAccessTime()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var sourceBundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "source.ab");
        File.WriteAllBytes(sourceBundlePath, [0x10, 0x20]);
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        var manifest = store.Save(new BundleArtifactSaveRequestDto(
            WorkspaceKey: "workspace-access",
            BundleCacheKey: "bundle-cache-access",
            SourceFingerprint: CreateFingerprint(sourceBundlePath, 2),
            DecryptorId: "builtin.pass-through",
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            ResolvedBundlePath: sourceBundlePath,
            ProducedDerivedFile: false,
            ResolvedBundleFingerprint: CreateFingerprint(sourceBundlePath, 2),
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 5, 0, TimeSpan.Zero)));
        var accessedAtUtc = new DateTimeOffset(2026, 5, 17, 11, 30, 0, TimeSpan.Zero);

        var updatedManifest = store.UpdateLastAccessed(manifest, accessedAtUtc);
        var restoredManifest = TestAssert.NotNull(
            store.Load("workspace-access", "bundle-cache-access"),
            "Updated bundle artifact manifest should be readable.");

        TestAssert.Equal(accessedAtUtc, updatedManifest.LastAccessedAtUtc, "Updated manifest should expose the new access time.");
        TestAssert.Equal(accessedAtUtc, restoredManifest.LastAccessedAtUtc, "Updated access time should be persisted.");
        TestAssert.Equal(manifest.CreatedAtUtc, restoredManifest.CreatedAtUtc, "Touching access time should not rewrite creation time.");
    }

    public static void DeleteWorkspace_ShouldRemoveBundleArtifactDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var sourceBundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "source.ab");
        File.WriteAllBytes(sourceBundlePath, [0x33]);
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        store.Save(new BundleArtifactSaveRequestDto(
            WorkspaceKey: "workspace-delete",
            BundleCacheKey: "bundle-cache-delete",
            SourceFingerprint: CreateFingerprint(sourceBundlePath, 1),
            DecryptorId: "decryptor-a",
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            ResolvedBundlePath: sourceBundlePath,
            ProducedDerivedFile: true,
            ResolvedBundleFingerprint: CreateFingerprint(sourceBundlePath, 1),
            CreatedAtUtc: DateTimeOffset.UtcNow));

        store.DeleteWorkspace("workspace-delete");

        TestAssert.Null(
            store.Load("workspace-delete", "bundle-cache-delete"),
            "Deleting workspace artifact cache should remove saved manifests.");
        TestAssert.False(
            Directory.Exists(Path.Combine(temporaryDirectory.DirectoryPath, "cache", "bundles", "workspace-delete")),
            "Deleting workspace artifact cache should remove the workspace bundle artifact directory.");
    }

    private static WorkspaceFileFingerprintDto CreateFingerprint(string sourcePath, long size) =>
        new(
            SourcePath: Path.GetFullPath(sourcePath),
            FileSizeInBytes: size,
            LastWriteTimeUtc: new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero),
            ContentHash: null);
}
