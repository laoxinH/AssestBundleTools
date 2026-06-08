using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

internal static class JsonSpineGroupCatalogCacheStoreTests
{
    public static void Save_ShouldRoundTripCatalogEntry_AndDeleteWorkspace()
    {
        using var temporaryDirectory = new TemporaryDirectoryScope();
        var store = new JsonSpineGroupCatalogCacheStore(temporaryDirectory.DirectoryPath);
        var workspaceId = new WorkspaceId("workspace-a");
        var bundleId = new BundleId("bundle-a");
        var createdAtUtc = new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);
        var entry = new SpineGroupCatalogCacheEntryDto(
            SchemaVersion: 1,
            WorkspaceKey: workspaceId.Value,
            CatalogFingerprint: "catalog-fingerprint",
            DetectorVersion: "detector-v1",
            BundleIndexFingerprint: "bundle-index-fingerprint",
            CreatedAtUtc: createdAtUtc,
            LastAccessedAtUtc: createdAtUtc,
            BundleResults:
            [
                new SpineAssetGroupListResultDto(
                    workspaceId,
                    bundleId,
                    "bundle-a.ab",
                    SearchKeyword: null,
                    TotalCount: 1,
                    FilteredCount: 1,
                    Items:
                    [
                        new SpineAssetGroupDto(
                            "group-1",
                            bundleId,
                            "hero",
                            "assets/hero/hero",
                            SkeletonAssets: Array.Empty<SpineAssetGroupPartDto>(),
                            AtlasAssets: Array.Empty<SpineAssetGroupPartDto>(),
                            TexturePageAssets: Array.Empty<SpineAssetGroupPartDto>(),
                            Diagnostics: Array.Empty<SpineAssetGroupDiagnosticDto>())
                    ])
            ]);

        store.Save(entry);

        var loaded = TestAssert.NotNull(store.Load(workspaceId.Value), "Saved Spine group catalog should be readable.");
        TestAssert.Equal("catalog-fingerprint", loaded.CatalogFingerprint, "Catalog fingerprint should round-trip.");
        TestAssert.Equal("hero", TestAssert.Single(TestAssert.Single(loaded.BundleResults).Items).DisplayName, "Group item should round-trip.");

        store.DeleteWorkspace(workspaceId.Value);

        TestAssert.Null(store.Load(workspaceId.Value), "Deleting workspace cache should remove the Spine group catalog.");
    }
}
