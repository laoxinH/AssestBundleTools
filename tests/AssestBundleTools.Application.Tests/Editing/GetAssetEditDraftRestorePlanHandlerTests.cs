using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Queries;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Editing;

internal static class GetAssetEditDraftRestorePlanHandlerTests
{
    public static async Task HandleAsync_ShouldReturnFailure_WhenWorkspaceKeyIsBlank()
    {
        var handler = new GetAssetEditDraftRestorePlanHandler(new TestAssetEditDraftStore());

        var result = await handler.HandleAsync(new GetAssetEditDraftRestorePlanQuery(
            WorkspaceKey: " ",
            DraftReferences: Array.Empty<AssetEditDraftReferenceDto>()));

        TestAssert.True(result.IsFailure, "Blank workspace key should fail.");
        TestAssert.Equal(
            "Asset.EditDraft.WorkspaceKeyRequired",
            TestAssert.Single(result.Errors).Code,
            "Failure should expose the stable draft workspace key error.");
    }

    public static async Task HandleAsync_ShouldReturnRestorableCandidate_WhenDraftMatchesReference()
    {
        var updatedAtUtc = new DateTimeOffset(2026, 5, 7, 10, 0, 0, TimeSpan.Zero);
        var store = new TestAssetEditDraftStore();
        store.Add(new AssetEditDraftDescriptorDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-1",
            DraftMode: "text",
            SourceFingerprint: "source-v1",
            UpdatedAtUtc: updatedAtUtc,
            ExpiresAtUtc: updatedAtUtc.AddDays(1)));
        var handler = new GetAssetEditDraftRestorePlanHandler(store);

        var result = await handler.HandleAsync(new GetAssetEditDraftRestorePlanQuery(
            WorkspaceKey: " workspace-a ",
            DraftReferences:
            [
                new AssetEditDraftReferenceDto(" draft-1 ", " text ", " source-v1 ")
            ],
            CurrentTimeUtc: updatedAtUtc));

        TestAssert.True(result.IsSuccess, "Matching draft reference should resolve successfully.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected restore plan.");
        TestAssert.Equal("workspace-a", plan.WorkspaceKey, "Workspace key should be normalized.");
        TestAssert.Equal(1, plan.TotalReferenceCount, "Plan should include one candidate.");
        TestAssert.Equal(1, plan.RestorableCount, "Plan should count the restorable draft.");
        TestAssert.False(plan.HasUnrestorableCandidates, "Matching draft should not produce unrestorable candidates.");

        var candidate = TestAssert.Single(plan.Candidates);
        TestAssert.True(candidate.IsRestorable, "Candidate should be restorable.");
        TestAssert.Equal("draft-1", candidate.Reference.DraftId, "Draft id should be normalized.");
        TestAssert.Equal("text", candidate.Reference.DraftMode, "Draft mode should be normalized.");
        TestAssert.Equal(updatedAtUtc, candidate.DraftDescriptor?.UpdatedAtUtc, "Descriptor metadata should be preserved.");
    }

    public static async Task HandleAsync_ShouldClassifyMissingMismatchExpiredAndInvalidReferences()
    {
        var nowUtc = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        var store = new TestAssetEditDraftStore();
        store.Add(new AssetEditDraftDescriptorDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-mismatch",
            DraftMode: "text",
            SourceFingerprint: "source-new",
            UpdatedAtUtc: nowUtc));
        store.Add(new AssetEditDraftDescriptorDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-expired",
            DraftMode: "text",
            SourceFingerprint: "source-v1",
            UpdatedAtUtc: nowUtc.AddDays(-3),
            ExpiresAtUtc: nowUtc.AddMinutes(-1)));
        var handler = new GetAssetEditDraftRestorePlanHandler(store);

        var result = await handler.HandleAsync(new GetAssetEditDraftRestorePlanQuery(
            WorkspaceKey: "workspace-a",
            DraftReferences:
            [
                new AssetEditDraftReferenceDto("draft-missing", "text", "source-v1"),
                new AssetEditDraftReferenceDto("draft-mismatch", "text", "source-v1"),
                new AssetEditDraftReferenceDto("draft-expired", "text", "source-v1"),
                new AssetEditDraftReferenceDto("", "text", "source-v1"),
                new AssetEditDraftReferenceDto("draft-without-mode", "", "source-v1")
            ],
            CurrentTimeUtc: nowUtc));

        TestAssert.True(result.IsSuccess, "Draft diagnostics should be returned as a successful plan.");
        var plan = result.Value ?? throw new InvalidOperationException("Expected restore plan.");
        TestAssert.Equal(5, plan.TotalReferenceCount, "Plan should preserve all input references.");
        TestAssert.Equal(0, plan.RestorableCount, "Plan should not count any restorable drafts.");
        TestAssert.Equal(1, plan.MissingCount, "Plan should count missing drafts.");
        TestAssert.Equal(1, plan.SourceMismatchCount, "Plan should count source mismatches.");
        TestAssert.Equal(1, plan.ExpiredCount, "Plan should count expired drafts.");
        TestAssert.Equal(2, plan.InvalidReferenceCount, "Plan should count invalid references.");
        TestAssert.True(plan.HasUnrestorableCandidates, "Plan should expose unrestorable candidates.");

        TestAssert.Equal(
            AssetEditDraftRestoreStatus.Missing,
            plan.Candidates[0].Status,
            "First candidate should be missing.");
        TestAssert.Equal(
            "Asset.EditDraft.NotFound",
            plan.Candidates[0].Diagnostic?.Code,
            "Missing candidate should carry a not-found diagnostic.");
        TestAssert.Equal(
            AssetEditDraftRestoreStatus.SourceMismatch,
            plan.Candidates[1].Status,
            "Second candidate should report a source mismatch.");
        TestAssert.Equal(
            AssetEditDraftRestoreStatus.Expired,
            plan.Candidates[2].Status,
            "Third candidate should report expiration.");
        TestAssert.Equal(
            "Asset.EditDraft.IdRequired",
            plan.Candidates[3].Diagnostic?.Code,
            "Blank draft id should carry the id-required diagnostic.");
        TestAssert.Equal(
            "Asset.EditDraft.ModeRequired",
            plan.Candidates[4].Diagnostic?.Code,
            "Blank draft mode should carry the mode-required diagnostic.");
    }

    public static async Task HandleAsync_ShouldAllowReferenceWithoutSourceFingerprint()
    {
        var nowUtc = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero);
        var store = new TestAssetEditDraftStore();
        store.Add(new AssetEditDraftDescriptorDto(
            WorkspaceKey: "workspace-a",
            DraftId: "draft-1",
            DraftMode: "text",
            SourceFingerprint: "source-v2",
            UpdatedAtUtc: nowUtc));
        var handler = new GetAssetEditDraftRestorePlanHandler(store);

        var result = await handler.HandleAsync(new GetAssetEditDraftRestorePlanQuery(
            WorkspaceKey: "workspace-a",
            DraftReferences:
            [
                new AssetEditDraftReferenceDto("draft-1", "text")
            ],
            CurrentTimeUtc: nowUtc));

        TestAssert.True(result.IsSuccess, "Plan should succeed.");
        var candidate = TestAssert.Single(result.Value?.Candidates ?? Array.Empty<AssetEditDraftRestoreCandidateDto>());
        TestAssert.Equal(
            AssetEditDraftRestoreStatus.Restorable,
            candidate.Status,
            "Missing reference fingerprint should not block draft restoration.");
    }

    private sealed class TestAssetEditDraftStore : IAssetEditDraftStore
    {
        private readonly Dictionary<AssetEditDraftLookupKey, AssetEditDraftDescriptorDto> _drafts = [];

        public AssetEditDraftDescriptorDto? Load(AssetEditDraftLookupKey lookupKey) =>
            _drafts.TryGetValue(lookupKey, out var descriptor)
                ? descriptor
                : null;

        public void Add(AssetEditDraftDescriptorDto descriptor)
        {
            var lookupKey = new AssetEditDraftLookupKey(
                descriptor.WorkspaceKey,
                descriptor.DraftId,
                descriptor.DraftMode);
            _drafts[lookupKey] = descriptor;
        }
    }
}
