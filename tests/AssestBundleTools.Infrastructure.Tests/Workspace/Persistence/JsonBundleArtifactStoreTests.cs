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
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero),
            CodecParameters: new Dictionary<string, string>
            {
                ["crosscore.outerPrefixBase64"] = "VW5pdHlGUwA="
            }));

        var restoredManifest = TestAssert.NotNull(
            store.Load("workspace-a", "bundle-cache-a"),
            "Saved bundle artifact manifest should be readable.");
        TestAssert.True(restoredManifest.IsMaterialized, "Derived bundle should be materialized into cache/bundles.");
        TestAssert.Equal(manifest.ResolvedBundlePath, restoredManifest.ResolvedBundlePath, "Manifest path should round-trip.");
        TestAssert.Equal(
            "VW5pdHlGUwA=",
            restoredManifest.CodecParameters!["crosscore.outerPrefixBase64"],
            "Codec parameters required for exact re-encryption should round-trip through the manifest.");
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

    public static void Save_ShouldMaterializePassThroughBundle_AndRoundTripManifest()
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

        TestAssert.True(manifest.IsMaterialized, "Pass-through bundles should be materialized into the standardized cache.");
        TestAssert.True(
            manifest.ResolvedBundlePath.EndsWith(
                Path.Combine("cache", "bundles", "workspace-a", "bundle-cache-pass-through", "normalized.bundle"),
                StringComparison.OrdinalIgnoreCase),
            "Pass-through manifest should reference the standardized bundle cache path.");
        TestAssert.True(
            File.Exists(Path.Combine(
                temporaryDirectory.DirectoryPath,
                "cache",
                "bundles",
                "workspace-a",
                "bundle-cache-pass-through",
                "normalized.bundle")),
            "Pass-through cache should create normalized.bundle.");
        TestAssert.Equal(2, new FileInfo(manifest.ResolvedBundlePath).Length, "Materialized bundle bytes should match the source.");
        TestAssert.Equal(
            manifest.ResolvedBundlePath,
            store.ResolveBundlePath(manifest),
            "Pass-through manifest should resolve to the materialized file while it exists.");
    }

    public static void ResolveBundlePath_ShouldRejectLegacyManifestOnlyPassThroughArtifact()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        Directory.CreateDirectory(temporaryDirectory.DirectoryPath);
        var sourceBundlePath = Path.Combine(temporaryDirectory.DirectoryPath, "source.ab");
        File.WriteAllBytes(sourceBundlePath, [0x10, 0x20]);
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        var legacyManifest = new BundleArtifactManifestDto(
            WorkspaceKey: "workspace-a",
            BundleCacheKey: "bundle-cache-pass-through",
            ResolvedBundlePath: sourceBundlePath,
            IsMaterialized: false,
            SourceFingerprint: CreateFingerprint(sourceBundlePath, 2),
            BundleFingerprint: CreateFingerprint(sourceBundlePath, 2),
            DecryptorId: "builtin.pass-through",
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            CreatedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 5, 0, TimeSpan.Zero),
            LastAccessedAtUtc: new DateTimeOffset(2026, 5, 17, 10, 5, 0, TimeSpan.Zero));

        TestAssert.Null(
            store.ResolveBundlePath(legacyManifest),
            "Legacy manifest-only pass-through artifacts should be regenerated into normalized.bundle.");
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

    public static void GetUsage_ShouldReturnArtifactEntriesAndTotalSize()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        SaveMaterializedArtifact(
            temporaryDirectory.DirectoryPath,
            store,
            "workspace-usage",
            "bundle-cache-a",
            [0x01, 0x02, 0x03],
            new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero));
        SaveMaterializedArtifact(
            temporaryDirectory.DirectoryPath,
            store,
            "workspace-usage",
            "bundle-cache-b",
            [0x04, 0x05],
            new DateTimeOffset(2026, 5, 17, 10, 5, 0, TimeSpan.Zero));

        var usage = store.GetUsage("workspace-usage");

        TestAssert.Equal(2, usage.ArtifactCount, "Usage should count saved artifact manifests.");
        TestAssert.Equal(2, usage.Entries.Count, "Usage should expose each artifact entry.");
        TestAssert.True(usage.TotalSizeInBytes >= 5, "Usage should include materialized bundle bytes.");
        TestAssert.True(
            usage.Entries.Any(static entry => string.Equals(entry.BundleCacheKey, "bundle-cache-a", StringComparison.OrdinalIgnoreCase)),
            "Usage should include the first artifact.");
        TestAssert.True(
            usage.Entries.All(static entry => entry.LastAccessedAtUtc is not null),
            "Usage entries should expose last access times.");
    }

    public static void Prune_ShouldDeleteLeastRecentlyAccessedArtifacts_WhenSizeLimitIsExceeded()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        var oldManifest = SaveMaterializedArtifact(
            temporaryDirectory.DirectoryPath,
            store,
            "workspace-prune",
            "bundle-cache-old",
            [0x01, 0x02, 0x03],
            new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero));
        var newManifest = SaveMaterializedArtifact(
            temporaryDirectory.DirectoryPath,
            store,
            "workspace-prune",
            "bundle-cache-new",
            [0x04, 0x05, 0x06, 0x07],
            new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero));
        store.UpdateLastAccessed(oldManifest, new DateTimeOffset(2026, 5, 17, 9, 30, 0, TimeSpan.Zero));
        store.UpdateLastAccessed(newManifest, new DateTimeOffset(2026, 5, 17, 11, 0, 0, TimeSpan.Zero));
        var usageBefore = store.GetUsage("workspace-prune");
        var oldEntry = usageBefore.Entries.First(entry =>
            string.Equals(entry.BundleCacheKey, "bundle-cache-old", StringComparison.OrdinalIgnoreCase));
        var sizeLimit = usageBefore.TotalSizeInBytes - oldEntry.SizeInBytes;

        var pruneResult = store.Prune(new BundleArtifactPruneRequestDto(
            WorkspaceKey: "workspace-prune",
            MaximumSizeInBytes: sizeLimit,
            MaximumArtifactCount: null));

        TestAssert.Equal(2, pruneResult.ArtifactCountBefore, "Prune should report the original artifact count.");
        TestAssert.Equal(1, pruneResult.ArtifactCountAfter, "Prune should remove one artifact to reach the size limit.");
        TestAssert.True(pruneResult.SizeAfterInBytes <= sizeLimit, "Prune should reduce size to the requested limit.");
        TestAssert.Equal(
            "bundle-cache-old",
            TestAssert.Single(pruneResult.PrunedEntries).BundleCacheKey,
            "Prune should remove the least recently accessed artifact first.");
        TestAssert.Null(store.Load("workspace-prune", "bundle-cache-old"), "Pruned artifact manifest should be removed.");
        TestAssert.True(
            store.Load("workspace-prune", "bundle-cache-new") is not null,
            "Newer artifact should remain after LRU pruning.");
    }

    public static void Prune_ShouldPreserveProtectedArtifact_WhenSizeLimitIsExceeded()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonBundleArtifactStore(temporaryDirectory.DirectoryPath);
        var oldManifest = SaveMaterializedArtifact(
            temporaryDirectory.DirectoryPath,
            store,
            "workspace-protected-prune",
            "bundle-cache-old",
            [0x01, 0x02, 0x03],
            new DateTimeOffset(2026, 5, 17, 9, 0, 0, TimeSpan.Zero));
        var newManifest = SaveMaterializedArtifact(
            temporaryDirectory.DirectoryPath,
            store,
            "workspace-protected-prune",
            "bundle-cache-new",
            [0x04, 0x05, 0x06, 0x07],
            new DateTimeOffset(2026, 5, 17, 10, 0, 0, TimeSpan.Zero));
        store.UpdateLastAccessed(oldManifest, new DateTimeOffset(2026, 5, 17, 9, 30, 0, TimeSpan.Zero));
        store.UpdateLastAccessed(newManifest, new DateTimeOffset(2026, 5, 17, 11, 0, 0, TimeSpan.Zero));
        var usageBefore = store.GetUsage("workspace-protected-prune");
        var oldEntry = usageBefore.Entries.First(entry =>
            string.Equals(entry.BundleCacheKey, "bundle-cache-old", StringComparison.OrdinalIgnoreCase));
        var sizeLimit = usageBefore.TotalSizeInBytes - oldEntry.SizeInBytes;

        var pruneResult = store.Prune(new BundleArtifactPruneRequestDto(
            WorkspaceKey: "workspace-protected-prune",
            MaximumSizeInBytes: sizeLimit,
            MaximumArtifactCount: null,
            ProtectedBundleCacheKeys: ["bundle-cache-old"]));

        TestAssert.Equal(
            "bundle-cache-new",
            TestAssert.Single(pruneResult.PrunedEntries).BundleCacheKey,
            "Prune should skip protected artifacts and remove the next eligible artifact.");
        TestAssert.True(
            store.Load("workspace-protected-prune", "bundle-cache-old") is not null,
            "Protected artifact should remain after pruning.");
        TestAssert.Null(
            store.Load("workspace-protected-prune", "bundle-cache-new"),
            "Unprotected artifact should be removed when it is the only eligible candidate.");
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

    private static BundleArtifactManifestDto SaveMaterializedArtifact(
        string rootDirectoryPath,
        JsonBundleArtifactStore store,
        string workspaceKey,
        string bundleCacheKey,
        byte[] bytes,
        DateTimeOffset createdAtUtc)
    {
        var sourceBundlePath = Path.Combine(rootDirectoryPath, "staging", $"{bundleCacheKey}.ab");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceBundlePath)!);
        File.WriteAllBytes(sourceBundlePath, bytes);

        return store.Save(new BundleArtifactSaveRequestDto(
            WorkspaceKey: workspaceKey,
            BundleCacheKey: bundleCacheKey,
            SourceFingerprint: CreateFingerprint(sourceBundlePath, bytes.Length),
            DecryptorId: "decryptor-a",
            DecryptorVersion: "1.0.0",
            NormalizerVersion: "bundle-artifact/1",
            ResolvedBundlePath: sourceBundlePath,
            ProducedDerivedFile: true,
            ResolvedBundleFingerprint: CreateFingerprint(sourceBundlePath, bytes.Length),
            CreatedAtUtc: createdAtUtc));
    }
}
