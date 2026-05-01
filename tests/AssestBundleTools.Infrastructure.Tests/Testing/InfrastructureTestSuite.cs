using AssestBundleTools.Infrastructure.Tests.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Tests.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Tests.Testing;

internal static class InfrastructureTestSuite
{
    public static async Task<int> RunAsync()
    {
        var failures = new List<string>();

        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceCatalogStoreTests.Load_ShouldReturnEmptySnapshot_WhenCatalogFileDoesNotExist),
            JsonWorkspaceCatalogStoreTests.Load_ShouldReturnEmptySnapshot_WhenCatalogFileDoesNotExist);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceCatalogStoreTests.Save_ShouldRoundTripCatalogSnapshot),
            JsonWorkspaceCatalogStoreTests.Save_ShouldRoundTripCatalogSnapshot);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceMetadataStoreTests.Save_ShouldRoundTripWorkspaceMetadata),
            JsonWorkspaceMetadataStoreTests.Save_ShouldRoundTripWorkspaceMetadata);
        await RunTestAsync(
            failures,
            nameof(JsonWorkspaceMetadataStoreTests.Delete_ShouldRemoveSavedMetadataFile),
            JsonWorkspaceMetadataStoreTests.Delete_ShouldRemoveSavedMetadataFile);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Save_ShouldRoundTripDescriptorAndPayload),
            JsonPreviewCacheStoreTests.Save_ShouldRoundTripDescriptorAndPayload);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptorPayload_AndPruneEmptyPayloadDirectory),
            JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptorPayload_AndPruneEmptyPayloadDirectory);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenDescriptorExistsButPayloadFileIsMissing),
            JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenDescriptorExistsButPayloadFileIsMissing);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenStoredDescriptorPayloadPathIsInvalid),
            JsonPreviewCacheStoreTests.Load_ShouldReturnNull_WhenStoredDescriptorPayloadPathIsInvalid);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.ResolvePayloadFilePath_ShouldReturnNull_WhenDescriptorFieldsAreInvalid),
            JsonPreviewCacheStoreTests.ResolvePayloadFilePath_ShouldReturnNull_WhenDescriptorFieldsAreInvalid);
        await RunTestAsync(
            failures,
            nameof(JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptor_WhenStoredDescriptorPayloadPathIsInvalid),
            JsonPreviewCacheStoreTests.Delete_ShouldRemoveDescriptor_WhenStoredDescriptorPayloadPathIsInvalid);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided),
            ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldUseBundleScopedWorkspaceStagingPath_WhenWorkspaceDataRootIsProvided);
        await RunTestAsync(
            failures,
            nameof(ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided),
            ExternalProcessAssetBundleDecryptorTests.DecryptAsync_ShouldAppendUniqueSegments_WhenCustomWorkingDirectoryIsProvided);

        if (failures.Count == 0)
        {
            Console.WriteLine("All infrastructure tests passed.");
            return 0;
        }

        Console.Error.WriteLine("Infrastructure tests failed:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($"- {failure}");
        }

        return 1;
    }

    private static Task RunTestAsync(
        ICollection<string> failures,
        string testName,
        Action test)
    {
        try
        {
            test();
            Console.WriteLine($"[PASS] {testName}");
        }
        catch (Exception exception)
        {
            failures.Add($"{testName}: {exception.Message}");
        }

        return Task.CompletedTask;
    }
}
