using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Services;

internal static class WorkspaceOperationLogServiceTests
{
    public static Task AppendOperation_ShouldAssignSequence_AndPreserveExistingChangeSetState()
    {
        const string workspaceKey = "workspace-a";
        var updatedAtUtc = new DateTimeOffset(2026, 5, 10, 8, 0, 0, TimeSpan.Zero);
        var service = new WorkspaceOperationLogService();
        var changeSet = new WorkspaceChangeSetDto(
            WorkspaceKey: workspaceKey,
            UpdatedAtUtc: updatedAtUtc,
            Changes:
            [
                new WorkspaceChangeRecordDto(
                    ChangeKey: "extract:bundle-a:asset-1",
                    ChangeKind: "extraction",
                    BundleId: "bundle-a",
                    AssetId: "asset-1",
                    Summary: "Existing summary",
                    RecordedAtUtc: updatedAtUtc,
                    SourceDescriptor: null,
                    OutputPath: "Exports/asset-1.png")
            ],
            EditDrafts: Array.Empty<WorkspaceAssetEditDraftDescriptorDto>());

        var appended = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: " workspace-a ",
                OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                Status: WorkspaceOperationStatus.Applied,
                Target: new WorkspaceOperationTargetDto(
                    BundleId: "bundle-a",
                    AssetId: "asset-1",
                    AssetName: "Texture_A",
                    AssetType: "Texture2D"),
                Source: new WorkspaceOperationSourceDto(
                    SourceKind: "file",
                    SourceFilePath: @"C:\Source\Texture_A.png"),
                CreatedAtUtc: updatedAtUtc.AddMinutes(1)));

        TestAssert.Equal(WorkspaceChangeSetDto.CurrentSchemaVersion, appended.SchemaVersion, "Schema version should be upgraded.");
        TestAssert.Equal(1, appended.OperationSequence, "First appended operation should receive sequence 1.");
        TestAssert.Equal("extract:bundle-a:asset-1", TestAssert.Single(appended.Changes).ChangeKey, "Existing change summary should be preserved.");

        var operation = TestAssert.Single(appended.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.True(operation.OperationId.StartsWith("op_", StringComparison.Ordinal), "Generated operation id should use the operation prefix.");
        TestAssert.Equal(1, operation.Sequence, "Operation sequence should be assigned.");
        TestAssert.Equal(workspaceKey, operation.WorkspaceKey, "Workspace key should be normalized.");
        TestAssert.Equal(WorkspaceOperationKind.ReplaceFromFile, operation.OperationKind, "Operation kind should be preserved.");
        TestAssert.Equal(WorkspaceOperationStatus.Applied, operation.Status, "Operation status should be preserved.");
        TestAssert.Equal(updatedAtUtc.AddMinutes(1), operation.AppliedAtUtc, "Applied timestamp should default to creation time.");
        TestAssert.Equal(updatedAtUtc.AddMinutes(1), operation.CompletedAtUtc, "Completed timestamp should default to creation time for applied operations.");
        return Task.CompletedTask;
    }

    public static Task UpdateOperationStatus_ShouldUpdateStatusTimestamps_AndDiagnostics()
    {
        var service = new WorkspaceOperationLogService();
        var startedAtUtc = new DateTimeOffset(2026, 5, 10, 9, 0, 0, TimeSpan.Zero);
        var completedAtUtc = startedAtUtc.AddSeconds(30);
        var changeSet = service.AppendOperation(
            null,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.ExtractBatch,
                Status: WorkspaceOperationStatus.Pending,
                OperationId: " operation-1 ",
                CreatedAtUtc: startedAtUtc));

        var updated = service.UpdateOperationStatus(
            changeSet,
            " operation-1 ",
            WorkspaceOperationStatus.Failed,
            completedAtUtc,
            diagnostics:
            [
                new WorkspaceOperationDiagnosticDto(
                    Code: " Workspace.Extract.OutputUnavailable ",
                    Severity: " Error ",
                    MessageToken: " Extract.OutputUnavailable ")
            ]);

        var operation = TestAssert.Single(updated.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal("operation-1", operation.OperationId, "Operation id should be normalized.");
        TestAssert.Equal(WorkspaceOperationStatus.Failed, operation.Status, "Status should be updated.");
        TestAssert.Equal(completedAtUtc, operation.CompletedAtUtc, "Failed operation should receive a completed timestamp.");

        var diagnostic = TestAssert.Single(operation.Diagnostics ?? Array.Empty<WorkspaceOperationDiagnosticDto>());
        TestAssert.Equal("Workspace.Extract.OutputUnavailable", diagnostic.Code, "Diagnostic code should be normalized.");
        TestAssert.Equal("Error", diagnostic.Severity, "Diagnostic severity should be normalized.");
        TestAssert.Equal("Extract.OutputUnavailable", diagnostic.MessageToken, "Diagnostic token should be normalized.");
        return Task.CompletedTask;
    }

    public static Task GetActiveOperations_ShouldExcludeAppliedUndoTargets()
    {
        var service = new WorkspaceOperationLogService();
        var now = new DateTimeOffset(2026, 5, 10, 10, 0, 0, TimeSpan.Zero);
        var changeSet = service.AppendOperation(
            null,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.ReplaceFromWorkspace,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "replace-1",
                CreatedAtUtc: now));
        changeSet = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.ExtractAsset,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "extract-1",
                CreatedAtUtc: now.AddSeconds(1)));
        changeSet = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.UndoReplacement,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "undo-1",
                SupersedesOperationId: "replace-1",
                CreatedAtUtc: now.AddSeconds(2)));

        var activeOperations = service.GetActiveOperations(changeSet);

        var operation = TestAssert.Single(activeOperations);
        TestAssert.Equal("extract-1", operation.OperationId, "Undo should remove the superseded replacement from active operations.");
        return Task.CompletedTask;
    }

    public static Task DeleteOperationRecord_ShouldRemoveRecord_AndPreserveSequenceWatermark()
    {
        var service = new WorkspaceOperationLogService();
        var now = new DateTimeOffset(2026, 5, 10, 10, 30, 0, TimeSpan.Zero);
        var changeSet = service.AppendOperation(
            null,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.ExtractAsset,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "extract-1",
                CreatedAtUtc: now));
        changeSet = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.ReplaceFromFile,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "replace-1",
                CreatedAtUtc: now.AddSeconds(1)));
        changeSet = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.RedoReplacement,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "redo-1",
                SupersedesOperationId: "extract-1",
                CreatedAtUtc: now.AddSeconds(2)));

        var updated = service.DeleteOperationRecord(changeSet, " extract-1 ", now.AddMinutes(1));

        TestAssert.Equal(3, updated.OperationSequence, "Deleting a record must not lower the append sequence watermark.");
        var operation = TestAssert.Single(updated.Operations ?? Array.Empty<WorkspaceOperationRecordDto>());
        TestAssert.Equal("replace-1", operation.OperationId, "The requested operation and dependent records should be removed.");
        TestAssert.Equal(now.AddMinutes(1), updated.UpdatedAtUtc, "Delete should update the change set timestamp.");
        return Task.CompletedTask;
    }

    public static Task AppendOperation_ShouldRespectRecordLimit_AndProtectCurrentReplacementChain()
    {
        var service = new WorkspaceOperationLogService(() => 2);
        var now = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);
        var changeSet = service.AppendOperation(
            null,
            CreateReplacementAppendRequest("replace-1", "asset-a", now));
        changeSet = service.AppendOperation(
            changeSet,
            CreateReplacementAppendRequest("replace-2", "asset-a", now.AddSeconds(1)));
        changeSet = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.UndoReplacement,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "undo-1",
                Target: CreateTarget("asset-a"),
                CreatedAtUtc: now.AddSeconds(2)));
        changeSet = service.AppendOperation(
            changeSet,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.ExtractAsset,
                Status: WorkspaceOperationStatus.Applied,
                OperationId: "extract-1",
                Target: CreateTarget("asset-b"),
                CreatedAtUtc: now.AddSeconds(3)));

        var operationIds = (changeSet.Operations ?? Array.Empty<WorkspaceOperationRecordDto>())
            .Select(static operation => operation.OperationId)
            .ToArray();

        TestAssert.True(operationIds.Contains("replace-1"), "The replacement currently materialized after undo must be retained.");
        TestAssert.True(operationIds.Contains("replace-2"), "The later operation needed to replay the undo chain must be retained.");
        TestAssert.True(operationIds.Contains("undo-1"), "The undo operation must stay with the protected replacement chain.");
        TestAssert.True(operationIds.Contains("extract-1"), "The latest extraction for a target should be retained.");
        return Task.CompletedTask;
    }

    public static Task CreateRecoveryPlan_ShouldReportPendingOperationsAsInterrupted()
    {
        var service = new WorkspaceOperationLogService();
        var changeSet = service.AppendOperation(
            null,
            new WorkspaceOperationAppendRequestDto(
                WorkspaceKey: "workspace-a",
                OperationKind: WorkspaceOperationKind.SaveExecutionStarted,
                Status: WorkspaceOperationStatus.Pending,
                OperationId: "save-1",
                CreatedAtUtc: new DateTimeOffset(2026, 5, 10, 11, 0, 0, TimeSpan.Zero)));

        var recoveryPlan = service.CreateRecoveryPlan(changeSet);

        TestAssert.True(recoveryPlan.HasRecoverableItems, "Pending operation should be recoverable.");
        var item = TestAssert.Single(recoveryPlan.Items);
        TestAssert.Equal("save-1", item.OperationId, "Recovery item should preserve operation id.");
        TestAssert.Equal(WorkspaceOperationStatus.Interrupted, item.Status, "Pending operations should be reported as interrupted.");
        TestAssert.Equal(WorkspaceOperationRecoveryIssueKind.InterruptedPendingOperation, item.IssueKind, "Recovery issue should classify interrupted pending operations.");
        TestAssert.Equal(WorkspaceOperationRecoveryActionKind.MarkFailed, item.RecommendedAction, "Recovery plan should recommend a conservative action.");
        return Task.CompletedTask;
    }

    private static WorkspaceOperationAppendRequestDto CreateReplacementAppendRequest(
        string operationId,
        string assetId,
        DateTimeOffset createdAtUtc) =>
        new(
            WorkspaceKey: "workspace-a",
            OperationKind: WorkspaceOperationKind.ReplaceFromFile,
            Status: WorkspaceOperationStatus.Applied,
            OperationId: operationId,
            Target: CreateTarget(assetId),
            Source: new WorkspaceOperationSourceDto(
                SourceKind: "file",
                SourceFilePath: $@"C:\Source\{operationId}.png"),
            CreatedAtUtc: createdAtUtc);

    private static WorkspaceOperationTargetDto CreateTarget(string assetId) =>
        new(
            BundleId: "bundle-a",
            AssetId: assetId,
            AssetType: "Texture2D");
}
