using AssestBundleTools.Application.Tests.Testing;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.Workspace.Services;

internal static class WorkspaceOperationRelationMatcherTests
{
    public static Task OperationConcernsTarget_ShouldMatchRelatedSourceFingerprint()
    {
        var operation = new WorkspaceOperationRecordDto(
            OperationId: "op-spine-1",
            Sequence: 1,
            OperationKind: WorkspaceOperationKind.ReplaceFromFile,
            Status: WorkspaceOperationStatus.Applied,
            WorkspaceKey: "workspace",
            Target: new WorkspaceOperationTargetDto(
                BundleId: "bundle-a",
                AssetId: "texture-1",
                AssetName: "Hero.png"),
            Source: null,
            PreviousState: null,
            Result: null,
            CreatedAtUtc: new DateTimeOffset(2026, 6, 4, 9, 0, 0, TimeSpan.Zero),
            Relations:
            [
                new WorkspaceOperationRelationDto(
                    RelationId: "relation-spine",
                    RelationKind: "linked-resource-operation",
                    Role: "subject",
                    RelatedTarget: new WorkspaceOperationTargetDto(
                        BundleId: "bundle-a",
                        AssetName: "HeroSpine",
                        AssetType: "SpineAssetGroup",
                        SourceFingerprint: "spine-group:bundle-a:assets/hero/hero"))
            ]);

        var matches = WorkspaceOperationRelationMatcher.OperationConcernsTarget(
            operation,
            "bundle-a",
            assetId: null,
            sourceFingerprint: "spine-group:bundle-a:assets/hero/hero");

        TestAssert.True(
            matches,
            "Spine group operation projection should match relation targets identified by SourceFingerprint.");
        return Task.CompletedTask;
    }
}
