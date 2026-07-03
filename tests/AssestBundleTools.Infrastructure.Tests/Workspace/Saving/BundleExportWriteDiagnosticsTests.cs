using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static class BundleExportWriteDiagnosticsTests
{
    public static async Task ResolveTopLevelDetail_ShouldIncludeStageProcessSourceSizeAndException()
    {
        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "source.ab");
        Directory.CreateDirectory(scope.DirectoryPath);
        await File.WriteAllTextAsync(sourcePath, "diagnostic source");

        var request = new WorkspaceBundleExportRequestDto(
            WorkspaceId: new WorkspaceId("workspace"),
            WorkspaceDisplayName: "Workspace",
            BundleId: new BundleId("bundle"),
            BundleDisplayName: "source.ab",
            OutputFilePath: Path.Combine(scope.DirectoryPath, "export.ab"),
            SourceArtifactPath: sourcePath,
            BundleCacheKey: "bundle-cache-key",
            ChangeItems: Array.Empty<AssestBundleTools.Application.Workspace.Dtos.WorkspaceChangeItemDto>(),
            AssetWriteItems:
            [
                new WorkspaceBundleAssetWriteItemDto(
                    BundleId: new BundleId("bundle"),
                    AssetId: new AssetId("asset-text"),
                    AssetName: "Text",
                    AssetType: AssetType.TextAsset,
                    AssetTypeDisplayName: nameof(AssetType.TextAsset),
                    SerializedFileName: "source",
                    PathId: 1,
                    SourceKind: "file",
                    SourceFilePath: sourcePath)
            ],
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation");

        var detail = BundleExportWriteDiagnostics.ResolveTopLevelDetail(
            "load-bundle",
            request,
            new OutOfMemoryException("large allocation failed"));

        TestAssert.True(
            detail.Contains("stage=load-bundle", StringComparison.Ordinal),
            "Bundle write diagnostics should include the failing stage.");
        TestAssert.True(
            detail.Contains("process=", StringComparison.Ordinal),
            "Bundle write diagnostics should include process architecture.");
        TestAssert.True(
            detail.Contains("sourceSize=", StringComparison.Ordinal),
            "Bundle write diagnostics should include input file size.");
        TestAssert.True(
            detail.Contains("bundle=source.ab", StringComparison.Ordinal),
            "Bundle write diagnostics should include bundle display name.");
        TestAssert.True(
            detail.Contains("exception=OutOfMemoryException", StringComparison.Ordinal),
            "Bundle write diagnostics should include exception type.");
        TestAssert.True(
            detail.Contains("managedMemory=", StringComparison.Ordinal),
            "Bundle write diagnostics should include managed memory snapshot.");
    }
}
