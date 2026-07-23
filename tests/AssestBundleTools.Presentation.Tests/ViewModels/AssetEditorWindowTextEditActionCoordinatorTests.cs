using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Commands;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Extraction.Commands;
using AssestBundleTools.Application.Extraction.Dtos;
using AssestBundleTools.Application.Replacement.Commands;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Replacement.Queries;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class AssetEditorWindowTextEditActionCoordinatorTests
{
    [Fact]
    public async Task OpenOrCreateTextAssetEditDraftAsync_ShouldPreferCurrentAppliedDetailsOverStaleTabReference()
    {
        var workspaceSummary = new WorkspaceSummaryDto(
            WorkspaceId: new WorkspaceId("workspace-session"),
            DisplayName: "Workspace",
            SourcePath: @"C:\Workspace\Source",
            Mode: WorkspaceMode.SinglePane,
            ActivePaneId: PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\Workspace\Data");
        var staleDetails = CreateTextAssetDetails(
            textEditDraftId: "draft-latest",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 2);
        var currentDetails = CreateTextAssetDetails(
            textEditDraftId: "draft-current",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 1);
        var assetTab = new AssetDocumentTabViewModel(
            staleDetails.BundleId,
            staleDetails.AssetId,
            staleDetails.Name,
            InspectorInformationTextSetFactory.Create(new TestLocalizationService()));
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-latest",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            "source-latest",
            "latest",
            IsAppliedBaseline: true,
            DraftRevision: 2));
        assetTab.ApplyDetails(staleDetails);

        var operationGateway = new RecordingWorkspaceAssetOperationGateway
        {
            OpenDraftHandler = command => Task.FromResult(Result<AssetTextEditDraftDto>.Success(CreateDraft(command)))
        };
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetDetailsHandler = _ => Task.FromResult(Result<AssetDetailsDto>.Success(currentDetails)),
            LoadPreviewTextLinesHandler = query => Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(
                new AssetPreviewTextLineRangeDto(
                    query.Reference,
                    ["current"],
                    StartLineIndex: 0,
                    LineCount: 1,
                    TotalLineCount: 1,
                    HasMore: false)))
        };
        var coordinator = new AssetEditorWindowTextEditActionCoordinator(
            new TestWorkspaceShellHost(workspaceSummary),
            new TestLocalizationService(),
            new BundleExplorerOperationFeedbackBridge(
                new TestAppLogService(),
                new TestAppNotificationService(),
                new TestLocalizationService()),
            operationGateway,
            inspectionGateway,
            new TestBackgroundTaskRunner(),
            () => assetTab,
            TryGetAssetCommandContext,
            _ => { });

        var draft = await coordinator.OpenOrCreateTextAssetEditDraftAsync();

        Assert.NotNull(draft);
        Assert.Equal("draft-current", operationGateway.LastOpenCommand?.DraftId);
        Assert.Equal(WorkspaceTextEditOperationRecorder.TextDraftMode, operationGateway.LastOpenCommand?.DraftMode);
        Assert.Null(operationGateway.LastOpenCommand?.SourceFingerprint);
        Assert.Equal(1, operationGateway.LastOpenCommand?.DraftRevision);
        Assert.Null(operationGateway.LastCreateCommand);
        Assert.Equal(1, draft.Reference.DraftRevision);
        Assert.Equal("draft-current", assetTab.Details?.TextEditDraftId);
        Assert.Null(assetTab.EditDraftReference);

        bool TryGetAssetCommandContext(
            Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute,
            [NotNullWhen(true)] out WorkspaceSummaryDto? summary,
            [NotNullWhen(true)] out AssetDocumentTabViewModel? tab,
            [NotNullWhen(true)] out AssetDetailsDto? activeDetails)
        {
            summary = workspaceSummary;
            tab = assetTab;
            activeDetails = assetTab.Details;
            return activeDetails is not null && canExecute(assetTab, activeDetails);
        }
    }

    [Fact]
    public async Task OpenOrCreateTextAssetEditDraftAsync_ShouldPinCurrentRevision_WhenSameDraftRolledBack()
    {
        var workspaceSummary = new WorkspaceSummaryDto(
            WorkspaceId: new WorkspaceId("workspace-session"),
            DisplayName: "Workspace",
            SourcePath: @"C:\Workspace\Source",
            Mode: WorkspaceMode.SinglePane,
            ActivePaneId: PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\Workspace\Data");
        var staleDetails = CreateTextAssetDetails(
            textEditDraftId: "draft-text",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 4);
        var currentDetails = CreateTextAssetDetails(
            textEditDraftId: "draft-text",
            textEditDraftMode: WorkspaceTextEditOperationRecorder.TextDraftMode,
            textEditDraftRevision: 3);
        var assetTab = new AssetDocumentTabViewModel(
            staleDetails.BundleId,
            staleDetails.AssetId,
            staleDetails.Name,
            InspectorInformationTextSetFactory.Create(new TestLocalizationService()));
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-text",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            "source-text",
            "story.atlas",
            IsAppliedBaseline: true,
            DraftRevision: 4));
        assetTab.ApplyDetails(staleDetails);

        var operationGateway = new RecordingWorkspaceAssetOperationGateway
        {
            OpenDraftHandler = command => Task.FromResult(Result<AssetTextEditDraftDto>.Success(CreateDraft(command)))
        };
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetDetailsHandler = _ => Task.FromResult(Result<AssetDetailsDto>.Success(currentDetails)),
            LoadPreviewTextLinesHandler = query => Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(
                new AssetPreviewTextLineRangeDto(
                    query.Reference,
                    ["current"],
                    StartLineIndex: 0,
                    LineCount: 1,
                    TotalLineCount: 1,
                    HasMore: false)))
        };
        var coordinator = new AssetEditorWindowTextEditActionCoordinator(
            new TestWorkspaceShellHost(workspaceSummary),
            new TestLocalizationService(),
            new BundleExplorerOperationFeedbackBridge(
                new TestAppLogService(),
                new TestAppNotificationService(),
                new TestLocalizationService()),
            operationGateway,
            inspectionGateway,
            new TestBackgroundTaskRunner(),
            () => assetTab,
            TryGetAssetCommandContext,
            _ => { });

        var draft = await coordinator.OpenOrCreateTextAssetEditDraftAsync();

        Assert.NotNull(draft);
        Assert.Equal("draft-text", operationGateway.LastOpenCommand?.DraftId);
        Assert.Equal("source-text", operationGateway.LastOpenCommand?.SourceFingerprint);
        Assert.Equal(3, operationGateway.LastOpenCommand?.DraftRevision);
        Assert.Equal(3, draft.Reference.DraftRevision);
        Assert.Equal(3, assetTab.Details?.TextEditDraftRevision);

        bool TryGetAssetCommandContext(
            Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute,
            [NotNullWhen(true)] out WorkspaceSummaryDto? summary,
            [NotNullWhen(true)] out AssetDocumentTabViewModel? tab,
            [NotNullWhen(true)] out AssetDetailsDto? activeDetails)
        {
            summary = workspaceSummary;
            tab = assetTab;
            activeDetails = assetTab.Details;
            return activeDetails is not null && canExecute(assetTab, activeDetails);
        }
    }

    [Fact]
    public async Task OpenOrCreateTextAssetEditDraftAsync_ShouldRecreateDraft_WhenStaleUnappliedDraftMissingAndCurrentBaselineIsFileReplacement()
    {
        const string replacementFilePath = @"C:\Imports\large-atlas.json";
        var workspaceSummary = new WorkspaceSummaryDto(
            WorkspaceId: new WorkspaceId("workspace-session"),
            DisplayName: "Workspace",
            SourcePath: @"C:\Workspace\Source",
            Mode: WorkspaceMode.SinglePane,
            ActivePaneId: PaneId.New(),
            BundleCount: 1,
            AssetCount: 1,
            OpenedAtUtc: DateTimeOffset.UtcNow,
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: @"C:\Workspace\Data");
        var currentDetails = CreateTextAssetDetails(
            textEditDraftId: null,
            textEditDraftMode: null,
            textEditDraftRevision: null,
            fileReplacementSourcePath: replacementFilePath);
        var assetTab = new AssetDocumentTabViewModel(
            currentDetails.BundleId,
            currentDetails.AssetId,
            currentDetails.Name,
            InspectorInformationTextSetFactory.Create(new TestLocalizationService()));
        assetTab.SetEditDraftReference(new AssetDocumentEditDraftReference(
            "draft-stale",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            "source-old",
            "old text draft"));
        assetTab.ApplyDetails(currentDetails);

        var operationGateway = new RecordingWorkspaceAssetOperationGateway
        {
            OpenDraftHandler = _ => Task.FromResult(Result<AssetTextEditDraftDto>.Failure(
                ApplicationErrors.AssetPreviewFullContentUnavailable("draft-stale"))),
            CreateDraftHandler = command => Task.FromResult(Result<AssetTextEditDraftDto>.Success(
                CreateDeferredFileReplacementDraft(command, replacementFilePath)))
        };
        var inspectionGateway = new TestWorkspaceAssetInspectionGateway
        {
            GetAssetDetailsHandler = _ => Task.FromResult(Result<AssetDetailsDto>.Success(currentDetails)),
            LoadPreviewTextLinesHandler = query => Task.FromResult(Result<AssetPreviewTextLineRangeDto>.Success(
                new AssetPreviewTextLineRangeDto(
                    query.Reference,
                    ["replacement"],
                    StartLineIndex: 0,
                    LineCount: 1,
                    TotalLineCount: 1,
                    HasMore: false)))
        };
        var coordinator = new AssetEditorWindowTextEditActionCoordinator(
            new TestWorkspaceShellHost(workspaceSummary),
            new TestLocalizationService(),
            new BundleExplorerOperationFeedbackBridge(
                new TestAppLogService(),
                new TestAppNotificationService(),
                new TestLocalizationService()),
            operationGateway,
            inspectionGateway,
            new TestBackgroundTaskRunner(),
            () => assetTab,
            TryGetAssetCommandContext,
            _ => { });

        var draft = await coordinator.OpenOrCreateTextAssetEditDraftAsync();

        Assert.NotNull(draft);
        Assert.Equal("draft-stale", operationGateway.LastOpenCommand?.DraftId);
        Assert.NotNull(operationGateway.LastCreateCommand);
        Assert.Equal(currentDetails.AssetId, operationGateway.LastCreateCommand?.AssetId);
        Assert.Null(assetTab.EditDraftReference);
        Assert.Equal(AssetPreviewFullContentReferenceKind.FileReplacement, draft.Reference.Kind);
        Assert.Equal(replacementFilePath, draft.Reference.SourcePath);

        bool TryGetAssetCommandContext(
            Func<AssetDocumentTabViewModel, AssetDetailsDto, bool> canExecute,
            [NotNullWhen(true)] out WorkspaceSummaryDto? summary,
            [NotNullWhen(true)] out AssetDocumentTabViewModel? tab,
            [NotNullWhen(true)] out AssetDetailsDto? activeDetails)
        {
            summary = workspaceSummary;
            tab = assetTab;
            activeDetails = assetTab.Details;
            return activeDetails is not null && canExecute(assetTab, activeDetails);
        }
    }

    private static AssetTextEditDraftDto CreateDraft(OpenTextAssetEditDraftCommand command)
    {
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.EditDraft,
            "text/plain; charset=utf-8",
            7,
            command.WorkspaceKey,
            DraftId: command.DraftId,
            DraftMode: command.DraftMode,
            DraftRevision: command.DraftRevision ?? 1);

        return new AssetTextEditDraftDto(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            "story.atlas",
            AssetType.TextAsset,
            AssetType.TextAsset.ToString(),
            command.DraftId,
            command.DraftMode,
            reference,
            DateTimeOffset.UtcNow,
            CanApply: true,
            Summary: "TextAsset",
            CanEditInline: false,
            IsAppliedBaseline: true,
            SourceFingerprint: "source-current",
            SourceDescription: "story.atlas");
    }

    private static AssetTextEditDraftDto CreateDeferredFileReplacementDraft(
        CreateTextAssetEditDraftCommand command,
        string replacementFilePath)
    {
        var reference = new AssetPreviewFullContentReferenceDto(
            AssetPreviewFullContentReferenceKind.FileReplacement,
            "text/plain; charset=utf-8",
            12 * 1024 * 1024,
            command.WorkspaceKey,
            SourcePath: replacementFilePath);

        return new AssetTextEditDraftDto(
            command.WorkspaceId,
            command.BundleId,
            command.AssetId,
            "story.atlas",
            AssetType.TextAsset,
            AssetType.TextAsset.ToString(),
            "draft-created",
            WorkspaceTextEditOperationRecorder.TextDraftMode,
            reference,
            DateTimeOffset.UtcNow,
            CanApply: true,
            Summary: "TextAsset",
            CanEditInline: true,
            IsAppliedBaseline: false,
            SourceFingerprint: "replacement-source",
            SourceDescription: "story.atlas",
            IsMaterialized: false);
    }

    private static AssetDetailsDto CreateTextAssetDetails(
        string? textEditDraftId,
        string? textEditDraftMode,
        int? textEditDraftRevision,
        string? fileReplacementSourcePath = null) =>
        new(
            WorkspaceId: new WorkspaceId("workspace-session"),
            BundleId: new BundleId("bundle-a"),
            AssetId: new AssetId("asset-text"),
            Name: "story.atlas",
            AssetPath: "Assets/story.atlas",
            ContainerPath: "CAB",
            AssetType: AssetType.TextAsset,
            SizeInBytes: 1024,
            PreferredDisplaySizeInBytes: null,
            PathId: 1001,
            IsModified: textEditDraftId is not null || !string.IsNullOrWhiteSpace(fileReplacementSourcePath),
            CanExtract: true,
            CanReplaceFromFile: true,
            CanReplaceFromWorkspace: true,
            WorkspaceReplacementSourceBundleDisplayName: null,
            WorkspaceReplacementSourceAssetName: null,
            WorkspaceReplacementSourceAssetType: null,
            FileReplacementSourceFileName: string.IsNullOrWhiteSpace(fileReplacementSourcePath)
                ? null
                : Path.GetFileName(fileReplacementSourcePath),
            FileReplacementSourcePath: fileReplacementSourcePath,
            FileReplacementSourceAssetType: string.IsNullOrWhiteSpace(fileReplacementSourcePath)
                ? null
                : AssetType.TextAsset,
            LastExtractedAtUtc: null,
            Dependencies: [],
            AssetTypeDisplayName: AssetType.TextAsset.ToString(),
            TextEditDraftId: textEditDraftId,
            TextEditDraftMode: textEditDraftMode,
            TextEditAppliedAtUtc: DateTimeOffset.UtcNow,
            TextEditDraftRevision: textEditDraftRevision);

    private sealed class TestWorkspaceShellHost : IBundleExplorerWorkspaceShellHost
    {
        public TestWorkspaceShellHost(WorkspaceSummaryDto workspaceSummary)
        {
            WorkspaceSummary = workspaceSummary;
        }

        public WorkspaceSummaryDto? WorkspaceSummary { get; }

        public WorkspaceNavigationRequest? ActiveWorkspaceNavigationRequest => null;
    }

    private sealed class RecordingWorkspaceAssetOperationGateway : IWorkspaceAssetOperationGateway
    {
        public OpenTextAssetEditDraftCommand? LastOpenCommand { get; private set; }

        public CreateTextAssetEditDraftCommand? LastCreateCommand { get; private set; }

        public Func<OpenTextAssetEditDraftCommand, Task<Result<AssetTextEditDraftDto>>> OpenDraftHandler { get; init; } =
            _ => throw new NotSupportedException();

        public Func<CreateTextAssetEditDraftCommand, Task<Result<AssetTextEditDraftDto>>> CreateDraftHandler { get; init; } =
            _ => throw new InvalidOperationException("Current applied text edit should open an existing draft.");

        public Task<Result<AssetTextEditDraftDto>> CreateTextAssetEditDraftAsync(
            CreateTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default)
        {
            LastCreateCommand = command;
            return CreateDraftHandler(command);
        }

        public Task<Result<AssetTextEditDraftDto>> OpenTextAssetEditDraftAsync(
            OpenTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default)
        {
            LastOpenCommand = command;
            return OpenDraftHandler(command);
        }

        public Task<Result<AssetExtractionResultDto>> ExtractAssetAsync(
            ExtractAssetCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<SpineAssetGroupExtractionResultDto>> ExtractSpineAssetGroupAsync(
            ExtractSpineAssetGroupCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<FileReplacementCandidateDto>> GetFileReplacementCandidateAsync(
            GetFileReplacementCandidateQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<WorkspaceReplacementCandidateDto>>> GetWorkspaceReplacementCandidatesAsync(
            GetWorkspaceReplacementCandidatesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetFileReplacementResultDto>> ReplaceAssetFromFileAsync(
            ReplaceAssetFromFileCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementResultDto>> ReplaceAssetFromWorkspaceAsync(
            ReplaceAssetFromWorkspaceCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementUndoResultDto>> UndoAssetReplacementAsync(
            UndoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetReplacementRedoResultDto>> RedoAssetReplacementAsync(
            RedoAssetReplacementCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetSpriteEditAppliedResultDto>> ApplySpriteEditAsync(
            ApplySpriteEditCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<SpineReplacementSourceMatchDto>>> MatchSpineReplacementSourcesAsync(
            MatchSpineReplacementSourcesQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<SpineSkeletonFormatCompatibilityDto>> CheckSpineSkeletonReplacementCompatibilityAsync(
            CheckSpineSkeletonReplacementCompatibilityQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditDraftUpdatedResultDto>> UpdateTextAssetEditDraftAsync(
            UpdateTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditAppliedResultDto>> ApplyTextAssetEditDraftAsync(
            ApplyTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetTextEditDraftDiscardedResultDto>> DiscardTextAssetEditDraftAsync(
            DiscardTextAssetEditDraftCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<WorkspaceOperationRecordDeletedResultDto>> DeleteWorkspaceOperationRecordAsync(
            DeleteWorkspaceOperationRecordCommand command,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
