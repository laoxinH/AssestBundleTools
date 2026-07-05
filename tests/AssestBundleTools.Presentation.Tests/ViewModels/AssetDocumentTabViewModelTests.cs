using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetDocumentTabViewModelTests
{
    [Fact]
    public void EditDraftReference_ShouldTrackDraftMarkerWithoutContent()
    {
        var assetTab = CreateAssetTab();
        var draftReference = new AssetDocumentEditDraftReference(
            DraftId: "draft-1",
            DraftMode: "text",
            SourceFingerprint: "source-v1",
            SourceDescription: "TextAsset edit");

        assetTab.SetEditDraftReference(draftReference);

        Assert.True(assetTab.HasEditDraft);
        Assert.Same(draftReference, assetTab.EditDraftReference);

        assetTab.ClearEditDraftReference();

        Assert.False(assetTab.HasEditDraft);
        Assert.Null(assetTab.EditDraftReference);
    }

    [Fact]
    public void RestoreEditDraftReference_ShouldApplyPersistedDraftMarker()
    {
        var assetTab = CreateAssetTab();

        assetTab.RestoreEditDraftReference(
            new AssetDocumentEditDraftReference("draft-2", "material", "source-v2"));

        Assert.True(assetTab.HasEditDraft);
        Assert.Equal("draft-2", assetTab.EditDraftReference?.DraftId);
        Assert.Equal("material", assetTab.EditDraftReference?.DraftMode);
        Assert.Equal("source-v2", assetTab.EditDraftReference?.SourceFingerprint);
    }

    [Fact]
    public void StartTextEditMode_ShouldPersistDraftSourceFingerprint()
    {
        var assetTab = CreateAssetTab();
        var draft = CreateTextEditDraftViewModel("source-v3", "story.json");

        assetTab.StartTextEditMode(draft);

        Assert.True(assetTab.HasEditDraft);
        Assert.Equal("draft-text-1", assetTab.EditDraftReference?.DraftId);
        Assert.Equal("text-content", assetTab.EditDraftReference?.DraftMode);
        Assert.Equal("source-v3", assetTab.EditDraftReference?.SourceFingerprint);
        Assert.Equal("story.json", assetTab.EditDraftReference?.SourceDescription);
        Assert.False(assetTab.EditDraftReference?.IsAppliedBaseline);
        Assert.Null(assetTab.EditDraftReference?.DraftRevision);
    }

    [Fact]
    public void ApplyDetails_ShouldPromoteMatchingTextDraftReferenceToAppliedBaseline()
    {
        var assetTab = CreateAssetTab();
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-text-1",
            "text-content",
            "source-v3",
            "story.json"));

        assetTab.ApplyDetails(CreateTextAssetDetails(
            textEditDraftId: "draft-text-1",
            textEditDraftMode: "text-content",
            textEditDraftRevision: 2));

        Assert.True(assetTab.HasEditDraft);
        Assert.True(assetTab.EditDraftReference?.IsAppliedBaseline);
        Assert.Equal(2, assetTab.EditDraftReference?.DraftRevision);
    }

    [Fact]
    public void ApplyDetails_ShouldKeepUnappliedDraftReferenceWhenNoTextEditIsEffective()
    {
        var assetTab = CreateAssetTab();
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-text-1",
            "text-content",
            "source-v3",
            "story.json"));

        assetTab.ApplyDetails(CreateTextAssetDetails());

        Assert.True(assetTab.HasEditDraft);
        Assert.False(assetTab.EditDraftReference?.IsAppliedBaseline);
        Assert.Null(assetTab.EditDraftReference?.DraftRevision);
    }

    [Fact]
    public void ApplyDetails_ShouldClearStaleAppliedBaselineReferenceWhenLastTextEditRecordIsRemoved()
    {
        var assetTab = CreateAssetTab();
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-text-1",
            "text-content",
            "source-v3",
            "story.json",
            IsAppliedBaseline: true,
            DraftRevision: 2));

        assetTab.ApplyDetails(CreateTextAssetDetails());

        Assert.False(assetTab.HasEditDraft);
        Assert.Null(assetTab.EditDraftReference);
    }

    [Fact]
    public void ApplyDetails_ShouldMoveAppliedBaselineReferenceToCurrentRevision()
    {
        var assetTab = CreateAssetTab();
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-text-1",
            "text-content",
            "source-v3",
            "story.json",
            IsAppliedBaseline: true,
            DraftRevision: 4));

        assetTab.ApplyDetails(CreateTextAssetDetails(
            textEditDraftId: "draft-text-1",
            textEditDraftMode: "text-content",
            textEditDraftRevision: 3));

        Assert.True(assetTab.HasEditDraft);
        Assert.True(assetTab.EditDraftReference?.IsAppliedBaseline);
        Assert.Equal(3, assetTab.EditDraftReference?.DraftRevision);
        Assert.Equal("source-v3", assetTab.EditDraftReference?.SourceFingerprint);
    }

    private static AssetDocumentTabViewModel CreateAssetTab()
    {
        var localizationService = new TestLocalizationService();
        return new AssetDocumentTabViewModel(
            new BundleId("bundle-a"),
            new AssetId("asset-1"),
            "Asset 1",
            InspectorInformationTextSetFactory.Create(localizationService));
    }

    private static TextAssetEditDraftViewModel CreateTextEditDraftViewModel(
        string sourceFingerprint,
        string sourceDescription)
    {
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            "text/plain; charset=utf-8",
            5,
            "workspace-a",
            DraftId: "draft-text-1",
            DraftMode: "text-content");
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            LoadPreviewTextLinesHandler = _ => Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(
                new AssetPreviewTextLineRangeDto(
                    reference,
                    ["hello"],
                    StartLineIndex: 0,
                    LineCount: 1,
                    TotalLineCount: 1,
                    HasMore: false)))
        };

        return new TextAssetEditDraftViewModel(
            new AssetTextEditDraftDto(
                WorkspaceId: new Domain.Workspace.WorkspaceId("workspace-session"),
                BundleId: new BundleId("bundle-a"),
                AssetId: new AssetId("asset-1"),
                AssetName: "story.json",
                AssetType: AssetType.TextAsset,
                AssetTypeDisplayName: AssetType.TextAsset.ToString(),
                DraftId: "draft-text-1",
                DraftMode: "text-content",
                Reference: reference,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                CanApply: true,
                Summary: "TextAsset",
                CanEditInline: false,
                SourceFingerprint: sourceFingerprint,
                SourceDescription: sourceDescription),
            inspectionGateway,
            new TestBackgroundTaskRunner(),
            @"D:\WorkspaceData\A",
            "story.json",
            "read only",
            "inline",
            "deferred",
            "{0} / {1}");
    }

    private static AssetDetailsDto CreateTextAssetDetails(
        string? textEditDraftId = null,
        string? textEditDraftMode = null,
        int? textEditDraftRevision = null)
    {
        return new AssetDetailsDto(
            WorkspaceId: new Domain.Workspace.WorkspaceId("workspace-session"),
            BundleId: new BundleId("bundle-a"),
            AssetId: new AssetId("asset-1"),
            Name: "story.json",
            AssetPath: "Assets/story.json",
            ContainerPath: "Assets",
            AssetType: AssetType.TextAsset,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: null,
            PathId: 1001,
            IsModified: textEditDraftId is not null,
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
            AssetTypeDisplayName: AssetType.TextAsset.ToString(),
            TextEditDraftId: textEditDraftId,
            TextEditDraftMode: textEditDraftMode,
            TextEditAppliedAtUtc: textEditDraftId is null ? null : DateTimeOffset.UtcNow,
            TextEditDraftRevision: textEditDraftRevision);
    }
}
