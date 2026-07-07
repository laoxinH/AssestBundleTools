using System.IO;
using System.Linq;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Saving.Commands;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Queries;
using AssestBundleTools.Application.Saving.Services;
using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Tests.Saving;

internal static partial class SaveWorkspaceHandlersTests
{
    public static async Task GetWorkspaceSavePreviewHandler_ShouldReturnFailure_WhenWorkspaceDoesNotExist()
    {
        var registry = new TestWorkspaceSessionRegistry
        {
            ContainsWorkspaceResult = false
        };
        var handler = new GetWorkspaceSavePreviewHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceSavePreviewQuery(new WorkspaceId("missing-workspace")));

        TestAssert.True(result.IsFailure, "Missing workspace should fail.");
        TestAssert.Equal(
            "Workspace.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing workspace error.");
    }

    public static async Task GetWorkspaceSavePreviewHandler_ShouldReturnEmptyPreview_WhenWorkspaceHasNoChangeSummary()
    {
        var workspaceId = CreateWorkspaceId();
        var handler = new GetWorkspaceSavePreviewHandler(new TestWorkspaceSessionRegistry());

        var result = await handler.HandleAsync(new GetWorkspaceSavePreviewQuery(
            workspaceId,
            OutputConflictPolicy: WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists));

        TestAssert.True(result.IsSuccess, "Preview should still succeed when the workspace has no staged save changes.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(0, preview.PlannedBundleCount, "Empty workspace should not produce bundle plans.");
        TestAssert.Equal(0, preview.TotalChangeCount, "Empty workspace should not report staged changes.");
        TestAssert.Empty(preview.BundlePlans, "Empty workspace should expose an empty save plan.");
    }

    public static async Task GetWorkspaceSavePreviewHandler_ShouldGroupBundlePlans_AndResolveDefaultOutputDirectory()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-zeta"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true,
                        extracted: true,
                        workspaceReplacementSourceAssetName: "SourcePortrait"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-alpha"),
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true,
                        fileReplacementSourceFileName: "config.json"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-alpha"),
                        assetId: new AssetId("asset-3"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ThemeMusic",
                        isModified: false,
                        fileReplacementSourceFileName: "theme.ogg")
                ])
        };
        var handler = new GetWorkspaceSavePreviewHandler(registry);

        var result = await handler.HandleAsync(new GetWorkspaceSavePreviewQuery(
            workspaceId,
            OutputConflictPolicy: WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists));

        TestAssert.True(result.IsSuccess, "Save preview should succeed when staged changes exist.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(Path.Combine("Outputs", "Workspace One"), preview.OutputDirectory, "Default output directory should be derived from the workspace display name.");
        TestAssert.Equal(2, preview.PlannedBundleCount, "Preview should group staged changes by bundle.");
        TestAssert.Equal(3, preview.TotalChangeCount, "Preview should carry forward total staged change count.");
        TestAssert.Equal(2, preview.ModifiedAssetCount, "Preview should carry forward modified asset count.");
        TestAssert.Equal(1, preview.ExtractedAssetCount, "Preview should carry forward extracted asset count.");
        TestAssert.Equal(1, preview.WorkspaceReplacementCount, "Preview should carry forward workspace replacement count.");
        TestAssert.Equal(2, preview.FileReplacementCount, "Preview should carry forward file replacement count.");
        TestAssert.Equal(2, preview.BundlePlans.Count, "Preview should expose one bundle plan per affected bundle.");

        var firstPlan = preview.BundlePlans[0];
        TestAssert.Equal("Alpha.ab", firstPlan.BundleDisplayName, "Bundle plans should be ordered by display name.");
        TestAssert.Equal(Path.Combine(preview.OutputDirectory, "Alpha.ab"), firstPlan.OutputFilePath, "Bundle output path should combine the default directory and bundle display name.");
        TestAssert.Equal(2, firstPlan.ChangeCount, "Alpha bundle should aggregate both staged changes.");
        TestAssert.Equal(1, firstPlan.ModifiedAssetCount, "Alpha bundle should count modified assets within the bundle.");
        TestAssert.Equal(0, firstPlan.ExtractedAssetCount, "Alpha bundle should report extracted assets within the bundle.");
        TestAssert.Equal(0, firstPlan.WorkspaceReplacementCount, "Alpha bundle should report workspace replacements within the bundle.");
        TestAssert.Equal(2, firstPlan.FileReplacementCount, "Alpha bundle should report file replacements within the bundle.");

        var secondPlan = preview.BundlePlans[1];
        TestAssert.Equal("zeta.ab", secondPlan.BundleDisplayName, "Second bundle plan should preserve the remaining bundle.");
        TestAssert.Equal(1, secondPlan.ChangeCount, "Zeta bundle should expose its single staged change.");
        TestAssert.Equal(1, secondPlan.ModifiedAssetCount, "Zeta bundle should count its modified asset.");
        TestAssert.Equal(1, secondPlan.ExtractedAssetCount, "Zeta bundle should report extracted assets within the bundle.");
        TestAssert.Equal(1, secondPlan.WorkspaceReplacementCount, "Zeta bundle should report workspace replacements within the bundle.");
        TestAssert.Equal(0, secondPlan.FileReplacementCount, "Zeta bundle should report file replacements within the bundle.");
    }

    public static async Task GetWorkspaceSavePreviewHandler_ShouldExposeOutputPreflightBlockers()
    {
        var workspaceId = CreateWorkspaceId();
        var outputProbe = new TestWorkspaceSaveOutputProbe();
        outputProbe.BlockedFileNames.Add("Alpha.ab");
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-alpha"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-zeta"),
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true)
                ])
        };
        var handler = new GetWorkspaceSavePreviewHandler(
            registry,
            outputProbe: outputProbe);

        var result = await handler.HandleAsync(new GetWorkspaceSavePreviewQuery(
            workspaceId,
            OutputConflictPolicy: WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists));

        TestAssert.True(result.IsSuccess, "Save preview should succeed while exposing output preflight blockers.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(1, preview.OutputReadyBundleCount, "One output path should be writable.");
        TestAssert.Equal(1, preview.OutputBlockedBundleCount, "One output path should be blocked.");
        TestAssert.Equal(0, preview.OutputUnprobedBundleCount, "A configured output probe should inspect every planned bundle.");
        TestAssert.False(preview.IsReadyForWrite, "Blocked output should prevent the plan from being marked ready for real writing.");

        var alphaPlan = preview.BundlePlans[0];
        TestAssert.Equal("Alpha.ab", alphaPlan.BundleDisplayName, "Alpha should remain first by display name.");
        TestAssert.Equal(WorkspaceSaveOutputReadiness.FileAlreadyExists, alphaPlan.OutputReadiness, "Existing output files should be explicit blockers.");
        TestAssert.True(alphaPlan.IsOutputWriteBlocked, "Existing output files should block real writing.");
        TestAssert.Equal("Save.Output.FileAlreadyExists", alphaPlan.OutputDiagnosticCode, "Blocked output should carry a stable diagnostic code.");

        var zetaPlan = preview.BundlePlans[1];
        TestAssert.Equal(WorkspaceSaveOutputReadiness.Ready, zetaPlan.OutputReadiness, "Non-existing output path should be marked ready.");
        TestAssert.False(zetaPlan.IsOutputWriteBlocked, "Ready output path should not block real writing.");
    }

    public static async Task GetWorkspaceSavePreviewHandler_ShouldTreatExistingFilesAsReady_WhenOverwriteIsEnabled()
    {
        var workspaceId = CreateWorkspaceId();
        var outputProbe = new TestWorkspaceSaveOutputProbe();
        outputProbe.BlockedFileNames.Add("Alpha.ab");
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-alpha"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true)
                ])
        };
        var handler = new GetWorkspaceSavePreviewHandler(
            registry,
            outputProbe: outputProbe);

        var result = await handler.HandleAsync(new GetWorkspaceSavePreviewQuery(workspaceId));

        TestAssert.True(result.IsSuccess, "Default overwrite preview should succeed.");
        var preview = result.Value ?? throw new InvalidOperationException("Expected preview result.");
        TestAssert.Equal(1, preview.OutputReadyBundleCount, "Existing files should be writable when overwrite is enabled.");
        TestAssert.Equal(0, preview.OutputBlockedBundleCount, "Existing files should not block the default overwrite flow.");
        var plan = TestAssert.Single(preview.BundlePlans);
        TestAssert.Equal(WorkspaceSaveOutputReadiness.Ready, plan.OutputReadiness, "Existing-file readiness should be projected as ready.");
        TestAssert.False(plan.IsOutputWriteBlocked, "Existing files should not block when overwrite is enabled.");
        TestAssert.Equal("Save.Output.FileAlreadyExists", plan.OutputDiagnosticCode, "Diagnostic should remain available for observability.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldReturnFailure_WhenWorkspaceDoesNotExist()
    {
        var registry = new TestWorkspaceSessionRegistry
        {
            ContainsWorkspaceResult = false
        };
        var handler = new SaveWorkspaceAsNewBundleHandler(registry);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(new WorkspaceId("missing-workspace")));

        TestAssert.True(result.IsFailure, "Missing workspace should fail.");
        TestAssert.Equal(
            "Workspace.Query.NotFound",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing workspace error.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldReturnFailure_WhenNoBundlePlansExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(workspaceId, Array.Empty<WorkspaceChangeItemDto>())
        };
        var handler = new SaveWorkspaceAsNewBundleHandler(registry);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(workspaceId));

        TestAssert.True(result.IsFailure, "Save execution should fail when no bundles are staged.");
        TestAssert.Equal(
            "Bundle.Save.NoCandidates",
            TestAssert.Single(result.Errors).Code,
            "Failure should report the missing save candidate error.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldReturnExecutionSummary_WhenBundlePlansExist()
    {
        var workspaceId = CreateWorkspaceId();
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-zeta"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true,
                        extracted: true,
                        workspaceReplacementSourceAssetName: "SourcePortrait"),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-alpha"),
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true,
                        fileReplacementSourceFileName: "config.json")
                ])
        };
        var handler = new SaveWorkspaceAsNewBundleHandler(registry);
        var beforeExecution = DateTimeOffset.UtcNow;

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(workspaceId, "  D:\\Exports  "));

        var afterExecution = DateTimeOffset.UtcNow;
        TestAssert.True(result.IsSuccess, "Save execution summary should succeed when bundle plans exist.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        TestAssert.Equal(workspaceId, execution.WorkspaceId, "Execution result should preserve workspace id.");
        TestAssert.Equal("Workspace One", execution.WorkspaceDisplayName, "Execution result should preserve workspace display name.");
        TestAssert.Equal("D:\\Exports", execution.OutputDirectory, "Explicit output directory should be trimmed and preserved.");
        TestAssert.Equal(2, execution.PlannedBundleCount, "Execution result should report the number of affected bundles.");
        TestAssert.Equal(2, execution.TotalChangeCount, "Execution result should report the staged change count.");
        TestAssert.True(
            execution.CompletedAtUtc >= beforeExecution && execution.CompletedAtUtc <= afterExecution,
            "Execution timestamp should be captured during handler execution.");
    }

    public static async Task SaveWorkspaceAsNewBundleHandler_ShouldReportOutputPreflightCounts()
    {
        var workspaceId = CreateWorkspaceId();
        var outputProbe = new TestWorkspaceSaveOutputProbe();
        outputProbe.BlockedFileNames.Add("zeta.ab");
        var registry = new TestWorkspaceSessionRegistry
        {
            WorkspaceChangeSummary = CreateWorkspaceChangeSummary(
                workspaceId,
                [
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-alpha"),
                        assetId: new AssetId("asset-1"),
                        bundleDisplayName: "Alpha.ab",
                        assetName: "ConfigText",
                        isModified: true),
                    CreateChangeItem(
                        bundleId: new BundleId("bundle-zeta"),
                        assetId: new AssetId("asset-2"),
                        bundleDisplayName: "zeta.ab",
                        assetName: "EnemyPortrait",
                        isModified: true)
                ])
        };
        var handler = new SaveWorkspaceAsNewBundleHandler(
            registry,
            outputProbe: outputProbe);

        var result = await handler.HandleAsync(new SaveWorkspaceAsNewBundleCommand(
            workspaceId,
            OutputConflictPolicy: WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists));

        TestAssert.True(result.IsSuccess, "Current save skeleton should still return an execution summary.");
        var execution = result.Value ?? throw new InvalidOperationException("Expected save execution result.");
        TestAssert.Equal(1, execution.OutputReadyBundleCount, "Execution summary should preserve ready output count.");
        TestAssert.Equal(1, execution.OutputBlockedBundleCount, "Execution summary should preserve blocked output count.");
        TestAssert.Equal(0, execution.OutputUnprobedBundleCount, "Execution summary should preserve unprobed output count.");
        TestAssert.False(execution.IsReadyForWrite, "Blocked output should keep real-write readiness false.");
    }

    private static WorkspaceId CreateWorkspaceId() => new("workspace-save-1");

    private static WorkspaceChangeSummaryDto CreateWorkspaceChangeSummary(
        WorkspaceId workspaceId,
        IReadOnlyList<WorkspaceChangeItemDto> items)
    {
        return new WorkspaceChangeSummaryDto(
            WorkspaceId: workspaceId,
            WorkspaceDisplayName: "Workspace One",
            SourcePath: "C:\\Workspaces\\WorkspaceOne",
            BundleCount: items.Select(static item => item.BundleId.Value).Distinct(StringComparer.Ordinal).Count(),
            AssetCount: items.Count,
            TotalChangeCount: items.Count,
            ModifiedAssetCount: items.Count(static item => item.IsModified),
            ExtractedAssetCount: items.Count(static item => item.LastExtractedAtUtc.HasValue),
            WorkspaceReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.WorkspaceReplacementSourceAssetName)),
            FileReplacementCount: items.Count(static item => !string.IsNullOrWhiteSpace(item.FileReplacementSourceFileName)),
            Items: items);
    }

    private static WorkspaceChangeItemDto CreateChangeItem(
        BundleId bundleId,
        AssetId assetId,
        string bundleDisplayName,
        string assetName,
        bool isModified,
        bool extracted = false,
        string? workspaceReplacementSourceAssetName = null,
        string? fileReplacementSourceFileName = null,
        AssetType assetType = AssetType.Texture2D)
    {
        var changedAtUtc = new DateTimeOffset(2026, 4, 29, 9, 0, 0, TimeSpan.Zero);
        var extractedAtUtc = extracted ? changedAtUtc.AddMinutes(-5) : (DateTimeOffset?)null;
        return new WorkspaceChangeItemDto(
            BundleId: bundleId,
            AssetId: assetId,
            BundleDisplayName: bundleDisplayName,
            AssetName: assetName,
            AssetType: assetType,
            IsModified: isModified,
            LastExtractedAtUtc: extractedAtUtc,
            WorkspaceReplacementSourceBundleDisplayName: string.IsNullOrWhiteSpace(workspaceReplacementSourceAssetName) ? null : "SourceBundle",
            WorkspaceReplacementSourceAssetName: workspaceReplacementSourceAssetName,
            WorkspaceReplacementAppliedAtUtc: string.IsNullOrWhiteSpace(workspaceReplacementSourceAssetName) ? null : changedAtUtc,
            FileReplacementSourceFileName: fileReplacementSourceFileName,
            FileReplacementSourcePath: string.IsNullOrWhiteSpace(fileReplacementSourceFileName) ? null : Path.Combine("C:\\Imports", fileReplacementSourceFileName),
            FileReplacementAppliedAtUtc: string.IsNullOrWhiteSpace(fileReplacementSourceFileName) ? null : changedAtUtc,
            LastChangedAtUtc: changedAtUtc);
    }

    private static AssetDetailsDto CreateAssetDetails(
        WorkspaceId workspaceId,
        BundleId bundleId,
        AssetId assetId,
        string assetName,
        AssetType assetType,
        string serializedFileName,
        long pathId)
    {
        return new AssetDetailsDto(
            WorkspaceId: workspaceId,
            BundleId: bundleId,
            AssetId: assetId,
            Name: assetName,
            AssetPath: assetName,
            ContainerPath: assetName,
            AssetType: assetType,
            SizeInBytes: 128,
            PreferredDisplaySizeInBytes: 128,
            PathId: pathId,
            IsModified: false,
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: null,
            FileReplacementSourcePath: null,
            FileReplacementSourceAssetType: null,
            LastExtractedAtUtc: null,
            Dependencies: Array.Empty<AssetDependencyDto>(),
            AssetTypeDisplayName: assetType.ToString(),
            SerializedFileName: serializedFileName);
    }

    private sealed class TestWorkspaceSessionRegistry : IWorkspaceSessionRegistry
    {
        private readonly Dictionary<string, AssetDetailsDto> _assetDetails = new(StringComparer.Ordinal);

        public bool ContainsWorkspaceResult { get; set; } = true;

        public WorkspaceChangeSummaryDto? WorkspaceChangeSummary { get; set; }

        public void AddAssetDetails(AssetDetailsDto details)
        {
            _assetDetails[CreateAssetKey(details.BundleId, details.AssetId)] = details;
        }

        public WorkspaceSummaryDto OpenWorkspace(WorkspaceSessionOpenRequestDto request) => throw new NotSupportedException();

        public bool ContainsWorkspace(WorkspaceId workspaceId) => ContainsWorkspaceResult;

        public bool ContainsBundle(WorkspaceId workspaceId, BundleId bundleId) => throw new NotSupportedException();

        public bool ContainsAsset(WorkspaceId workspaceId, BundleId bundleId, AssetId assetId) => throw new NotSupportedException();

        public bool TryGetBundleTree(WorkspaceId workspaceId, out IReadOnlyList<BundleTreeNodeDto> bundleTree)
        {
            bundleTree = null!;
            throw new NotSupportedException();
        }

        public bool TryGetAssetList(
            WorkspaceId workspaceId,
            BundleId bundleId,
            string? searchKeyword,
            AssetType? assetTypeFilter,
            string? assetTypeDisplayNameFilter,
            out AssetListResultDto? assetListResult)
        {
            assetListResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetAssetDetails(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetDetailsDto? assetDetails)
        {
            return _assetDetails.TryGetValue(CreateAssetKey(bundleId, assetId), out assetDetails);
        }

        public bool TryGetWorkspaceChangeSummary(
            WorkspaceId workspaceId,
            out WorkspaceChangeSummaryDto? changeSummary)
        {
            changeSummary = WorkspaceChangeSummary;
            return changeSummary is not null;
        }

        public bool TryExtractAsset(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetExtractionResultDto? extractionResult)
        {
            extractionResult = null;
            throw new NotSupportedException();
        }

        public bool TryGetWorkspaceReplacementCandidates(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            out IReadOnlyList<WorkspaceReplacementCandidateDto> candidates)
        {
            candidates = null!;
            throw new NotSupportedException();
        }

        public bool TryReplaceAssetFromFile(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            string sourceFilePath,
            AssetType sourceAssetType,
            out AssetFileReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            throw new NotSupportedException();
        }

        public bool TryReplaceAssetFromWorkspace(
            WorkspaceId workspaceId,
            BundleId targetBundleId,
            AssetId targetAssetId,
            BundleId sourceBundleId,
            AssetId sourceAssetId,
            out AssetReplacementResultDto? replacementResult)
        {
            replacementResult = null;
            throw new NotSupportedException();
        }

        public bool TryUndoAssetReplacement(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            out AssetReplacementUndoResultDto? undoResult)
        {
            undoResult = null;
            throw new NotSupportedException();
        }

        public bool TryOpenAssetInPane(
            WorkspaceId workspaceId,
            BundleId bundleId,
            AssetId assetId,
            PaneId paneId,
            out AssetPaneContextDto? paneContext)
        {
            paneContext = null;
            throw new NotSupportedException();
        }

        private static string CreateAssetKey(BundleId bundleId, AssetId assetId) =>
            string.Concat(bundleId.Value, "\n", assetId.Value);
    }

    private sealed class TestWorkspaceSaveOutputProbe : IWorkspaceSaveOutputProbe
    {
        public HashSet<string> BlockedFileNames { get; } = new(StringComparer.OrdinalIgnoreCase);

        public WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request)
        {
            var fileName = Path.GetFileName(request.OutputFilePath);
            if (BlockedFileNames.Contains(fileName))
            {
                return WorkspaceSaveOutputProbeResult.Blocked(
                    WorkspaceSaveOutputReadiness.FileAlreadyExists,
                    "Save.Output.FileAlreadyExists");
            }

            return WorkspaceSaveOutputProbeResult.Ready();
        }
    }
}
