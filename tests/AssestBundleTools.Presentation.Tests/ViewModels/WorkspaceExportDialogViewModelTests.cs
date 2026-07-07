using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Dialogs;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceExportDialogViewModelTests
{
    [Fact]
    public void ResourceProcessorOptions_ShouldDisplayManifestProcessors_WithoutGenericPlaceholderName()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: null,
            resourceProcessorCatalogEntries:
            [
                CreateProcessor(
                    id: "export.texture.png-processor",
                    displayName: "Texture PNG Processor",
                    description: "Processes Texture2D PNG payloads before bundle export.",
                    resourceKinds: ["Texture2D"],
                    isBuiltIn: false),
                CreateProcessor(
                    id: "resource.processor.texture2d.cli",
                    displayName: "resource.processor.texture2d.cli",
                    description: string.Empty,
                    resourceKinds: ["Texture2D"],
                    isBuiltIn: true)
            ]);

        var textureSlot = viewModel.ResourceProcessors.Single(static slot => slot.Id == "texture2d");
        var processorNames = textureSlot.Processors.Select(static processor => processor.DisplayName).ToArray();

        Assert.Contains("Texture PNG Processor", processorNames);
        Assert.Contains(ResourceKeys.WorkspaceExportDialogProcessorNone, processorNames);
        Assert.DoesNotContain("WorkspaceExportDialog.ProcessorCliPlaceholder", processorNames);
        Assert.Contains(
            ResourceKeys.PluginManagementResourceProcessorDisplayNameFormat + ":" + ResourceKeys.WorkspaceExportDialogResourceProcessorTexture2D,
            processorNames);

        var manifestProcessor = textureSlot.Processors.Single(static processor => processor.Id == "export.texture.png-processor");
        Assert.Equal("Processes Texture2D PNG payloads before bundle export.", manifestProcessor.Description);
    }

    [Fact]
    public void CreateSubmission_ShouldPreserveBundleAndProcessorSelections()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: new WorkspaceSavePreviewDto(
                WorkspaceId: new("workspace-export"),
                WorkspaceDisplayName: "Workspace Export",
                OutputDirectory: @"D:\Workspace\exports",
                PlannedBundleCount: 2,
                TotalChangeCount: 2,
                ModifiedAssetCount: 2,
                ExtractedAssetCount: 0,
                WorkspaceReplacementCount: 0,
                FileReplacementCount: 2,
                BundlePlans:
                [
                    CreateBundlePlan("bundle-alpha", "Alpha.ab"),
                    CreateBundlePlan("bundle-beta", "Beta.ab")
                ]),
            resourceProcessorCatalogEntries:
            [
                CreateProcessor(
                    id: "resource.processor.texture.png",
                    displayName: "Texture PNG Processor",
                    description: "Processes Texture2D payloads.",
                    resourceKinds: ["Texture2D"],
                    isBuiltIn: false)
            ]);

        viewModel.ExportToWorkspaceDirectory = true;
        viewModel.OverwriteExistingFiles = true;
        viewModel.Bundles.Single(bundle => bundle.BundleId.Value == "bundle-beta").IsSelected = false;
        var textureSlot = viewModel.ResourceProcessors.Single(static slot => slot.Id == "texture2d");
        textureSlot.SelectedProcessor = textureSlot.Processors.Single(static processor => processor.Id == "resource.processor.texture.png");
        var audioSlot = viewModel.ResourceProcessors.Single(static slot => slot.Id == "audio");
        audioSlot.IsEnabled = false;

        var submission = viewModel.CreateSubmission();

        Assert.True(submission.ExportToWorkspaceDirectory);
        Assert.Equal(WorkspaceBundleExportCompressionModeDto.PreserveOriginal, submission.CompressionMode);
        Assert.Equal(WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting, submission.OutputConflictPolicy);
        Assert.Equal(WorkspaceBundleExportAudioWriteModeDto.MatchOriginalContainer, submission.AudioWriteMode);
        Assert.Equal("bundle-alpha", Assert.Single(submission.SelectedBundleIds).Value);
        Assert.Equal("resource.processor.texture.png", submission.ProcessorSelections.Single(selection => selection.ResourceKind == "texture2d").ProcessorId);
        Assert.Null(submission.ProcessorSelections.Single(selection => selection.ResourceKind == "audio").ProcessorId);
        Assert.True(submission.ExportSettings.ExportToWorkspaceDirectory);
        Assert.True(submission.ExportSettings.OverwriteExistingFiles);
        Assert.Equal(
            "resource.processor.texture.png",
            submission.ExportSettings.ProcessorSettings!.Single(setting => setting.ResourceKind == "texture2d").ProcessorId);
        Assert.False(submission.ExportSettings.ProcessorSettings!.Single(setting => setting.ResourceKind == "audio").IsEnabled);
        Assert.Contains("LZMA", viewModel.Bundles.Single(bundle => bundle.BundleId.Value == "bundle-alpha").SummaryText);
        Assert.DoesNotContain("extracted", viewModel.Bundles.Single(bundle => bundle.BundleId.Value == "bundle-alpha").SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_ShouldDefaultToRecentChangedBundlesOnly()
    {
        var latestChangedAtUtc = new DateTimeOffset(2026, 7, 7, 8, 0, 0, TimeSpan.Zero);
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: CreateChangeSummary(
                ("bundle-recent", latestChangedAtUtc),
                ("bundle-previous", latestChangedAtUtc.AddDays(-2))),
            savePreview: new WorkspaceSavePreviewDto(
                WorkspaceId: new("workspace-export"),
                WorkspaceDisplayName: "Workspace Export",
                OutputDirectory: @"D:\Workspace\exports",
                PlannedBundleCount: 3,
                TotalChangeCount: 3,
                ModifiedAssetCount: 3,
                ExtractedAssetCount: 0,
                WorkspaceReplacementCount: 0,
                FileReplacementCount: 3,
                BundlePlans:
                [
                    CreateBundlePlan("bundle-recent", "Recent.ab"),
                    CreateBundlePlan("bundle-previous", "Previous.ab"),
                    CreateBundlePlan("bundle-undated", "Undated.ab")
                ]));

        Assert.True(viewModel.Bundles.Single(bundle => bundle.BundleId.Value == "bundle-recent").IsSelected);
        Assert.False(viewModel.Bundles.Single(bundle => bundle.BundleId.Value == "bundle-previous").IsSelected);
        Assert.False(viewModel.Bundles.Single(bundle => bundle.BundleId.Value == "bundle-undated").IsSelected);
        Assert.Equal("bundle-recent", Assert.Single(viewModel.CreateSubmission().SelectedBundleIds).Value);
    }

    [Fact]
    public void Constructor_ShouldRestoreWorkspaceExportSettings()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: new WorkspaceSavePreviewDto(
                WorkspaceId: new("workspace-export"),
                WorkspaceDisplayName: "Workspace Export",
                OutputDirectory: @"D:\Workspace\exports",
                PlannedBundleCount: 1,
                TotalChangeCount: 1,
                ModifiedAssetCount: 1,
                ExtractedAssetCount: 0,
                WorkspaceReplacementCount: 0,
                FileReplacementCount: 1,
                BundlePlans:
                [
                    CreateBundlePlan("bundle-alpha", "Alpha.ab")
                ]),
            resourceProcessorCatalogEntries:
            [
                CreateProcessor(
                    id: "resource.processor.texture.png",
                    displayName: "Texture PNG Processor",
                    description: "Processes Texture2D payloads.",
                    resourceKinds: ["Texture2D"],
                    isBuiltIn: false)
            ],
            exportSettings: new WorkspaceExportSettingsSnapshotDto(
                ExportToWorkspaceDirectory: true,
                OverwriteExistingFiles: true,
                CompressionMode: WorkspaceBundleExportCompressionModeDto.Lz4,
                AudioWriteMode: WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer,
                ProcessorSettings:
                [
                    new WorkspaceExportProcessorSettingSnapshotDto("Texture2D", true, "resource.processor.texture.png"),
                    new WorkspaceExportProcessorSettingSnapshotDto("audio", false, null)
                ]));

        var textureSlot = viewModel.ResourceProcessors.Single(static slot => slot.Id == "texture2d");
        var audioSlot = viewModel.ResourceProcessors.Single(static slot => slot.Id == "audio");

        Assert.True(viewModel.ExportToWorkspaceDirectory);
        Assert.True(viewModel.OverwriteExistingFiles);
        Assert.True(viewModel.ForceAudioPayloadWrite);
        Assert.Equal(WorkspaceBundleExportCompressionModeDto.Lz4, viewModel.SelectedCompressionOption?.Mode);
        Assert.Equal("resource.processor.texture.png", textureSlot.SelectedProcessor?.Id);
        Assert.False(audioSlot.IsEnabled);

        var submission = viewModel.CreateSubmission();

        Assert.Equal(WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting, submission.OutputConflictPolicy);
        Assert.Equal(WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer, submission.AudioWriteMode);
        Assert.Equal(WorkspaceBundleExportCompressionModeDto.Lz4, submission.ExportSettings.CompressionMode);
    }

    [Fact]
    public void CreateSubmission_ShouldUseFailIfExists_WhenOverwriteIsDisabled()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: new WorkspaceSavePreviewDto(
                WorkspaceId: new("workspace-export"),
                WorkspaceDisplayName: "Workspace Export",
                OutputDirectory: @"D:\Workspace\exports",
                PlannedBundleCount: 1,
                TotalChangeCount: 1,
                ModifiedAssetCount: 1,
                ExtractedAssetCount: 0,
                WorkspaceReplacementCount: 0,
                FileReplacementCount: 1,
                BundlePlans:
                [
                    CreateBundlePlan("bundle-alpha", "Alpha.ab")
                ]));

        var submission = viewModel.CreateSubmission();

        Assert.Equal(WorkspaceBundleExportOutputConflictPolicyDto.FailIfExists, submission.OutputConflictPolicy);
        Assert.False(submission.ExportSettings.OverwriteExistingFiles);
    }

    [Fact]
    public void CreateSubmission_ShouldUseForcedAudioWriteMode_WhenForceAudioPayloadWriteIsEnabled()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: null);

        Assert.Equal(string.Empty, viewModel.ForceAudioWarningText);

        viewModel.ForceAudioPayloadWrite = true;

        var submission = viewModel.CreateSubmission();

        Assert.Equal(WorkspaceBundleExportAudioWriteModeDto.ForcePayloadContainer, submission.AudioWriteMode);
        Assert.Equal(ResourceKeys.WorkspaceExportDialogForceAudioPayloadWriteWarning, viewModel.ForceAudioWarningText);
    }

    [Fact]
    public void ApplyExecutionResult_ShouldExposeFirstFailedBundleDiagnostic()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: null);

        viewModel.ApplyExecutionResult(new WorkspaceSaveExecutionResultDto(
            WorkspaceId: new("workspace-export"),
            WorkspaceDisplayName: "Workspace Export",
            OutputDirectory: @"D:\Workspace\exports",
            PlannedBundleCount: 1,
            TotalChangeCount: 1,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Status: WorkspaceSaveExecutionStatusDto.Failed,
            SucceededBundleCount: 0,
            FailedBundleCount: 1,
            WrittenBundleCount: 0,
            BundleResults:
            [
                new WorkspaceSaveBundleExecutionResultDto(
                    BundleId: new BundleId("bundle-alpha"),
                    BundleDisplayName: "Alpha.ab",
                    OutputFilePath: @"D:\Workspace\exports\Alpha.ab",
                    Status: WorkspaceSaveBundleExecutionStatusDto.Failed,
                    ChangeCount: 1,
                    ModifiedAssetCount: 1,
                    WorkspaceReplacementCount: 0,
                    FileReplacementCount: 1,
                    DiagnosticCode: "Bundle.Save.WriteFailed",
                    DiagnosticDetail: "asset=Texture; format=DXT5(12); exception=NotSupportedException")
            ]));

        Assert.Contains(ResourceKeys.WorkspaceExportDialogExecutionCompletedWithDiagnosticsFormat, viewModel.ValidationText);
        Assert.Contains("Alpha.ab", viewModel.ValidationText);
        Assert.Contains(ResourceKeys.WorkspaceExportDialogOutputUnknownFailureFormat, viewModel.ValidationText);
        Assert.Contains("format=DXT5(12)", viewModel.ValidationText);
    }

    [Fact]
    public void ApplyExecutionResult_ShouldFormatExistingOutputDiagnosticAsFriendlyText()
    {
        var viewModel = new WorkspaceExportDialogViewModel(
            new TestLocalizationService(),
            changeSummary: null,
            savePreview: null);

        viewModel.ApplyExecutionResult(new WorkspaceSaveExecutionResultDto(
            WorkspaceId: new("workspace-export"),
            WorkspaceDisplayName: "Workspace Export",
            OutputDirectory: @"D:\Workspace\exports",
            PlannedBundleCount: 1,
            TotalChangeCount: 1,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            Status: WorkspaceSaveExecutionStatusDto.Failed,
            SucceededBundleCount: 0,
            FailedBundleCount: 1,
            WrittenBundleCount: 0,
            BundleResults:
            [
                new WorkspaceSaveBundleExecutionResultDto(
                    BundleId: new BundleId("bundle-alpha"),
                    BundleDisplayName: "Alpha.ab",
                    OutputFilePath: @"D:\Workspace\exports\Alpha.ab",
                    Status: WorkspaceSaveBundleExecutionStatusDto.Failed,
                    ChangeCount: 1,
                    ModifiedAssetCount: 1,
                    WorkspaceReplacementCount: 0,
                    FileReplacementCount: 1,
                    DiagnosticCode: "Bundle.Save.OutputBlocked",
                    DiagnosticDetail: "OutputPath=D:\\Workspace\\exports\\Alpha.ab; DiagnosticCode=Save.Output.FileAlreadyExists")
            ]));

        Assert.Contains(ResourceKeys.WorkspaceExportDialogOutputFileAlreadyExists, viewModel.ValidationText);
        Assert.DoesNotContain("Save.Output.FileAlreadyExists", viewModel.ValidationText);
    }

    private static ResourceProcessorCatalogEntryDto CreateProcessor(
        string id,
        string displayName,
        string description,
        IReadOnlyList<string> resourceKinds,
        bool isBuiltIn)
    {
        return new ResourceProcessorCatalogEntryDto(
            id,
            displayName,
            Version: "1.0.0",
            description,
            ResourceProcessorKind.ExternalProcess,
            resourceKinds,
            ResourceProcessorStatus.Available,
            IsBuiltIn: isBuiltIn,
            IsReadOnly: isBuiltIn,
            ManifestFilePath: isBuiltIn ? null : $@"C:\Plugins\{id}.json",
            ExecutablePath: isBuiltIn ? null : $@"C:\Tools\{id}.exe",
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments: ["--input", "{inputPath}"]);
    }

    private static WorkspaceSaveBundlePlanDto CreateBundlePlan(string bundleId, string displayName) =>
        new(
            BundleId: new BundleId(bundleId),
            BundleDisplayName: displayName,
            OutputFilePath: $@"D:\Workspace\exports\{displayName}",
            ChangeCount: 1,
            ModifiedAssetCount: 1,
            ExtractedAssetCount: 0,
            WorkspaceReplacementCount: 0,
            FileReplacementCount: 1,
            OriginalCompression: "LZMA");

    private static WorkspaceChangeSummaryDto CreateChangeSummary(
        params (string BundleId, DateTimeOffset LastChangedAtUtc)[] changes)
    {
        return new WorkspaceChangeSummaryDto(
            WorkspaceId: new("workspace-export"),
            WorkspaceDisplayName: "Workspace Export",
            SourcePath: @"C:\Workspace",
            BundleCount: changes.Length,
            AssetCount: changes.Length,
            TotalChangeCount: changes.Length,
            ModifiedAssetCount: changes.Length,
            ExtractedAssetCount: 0,
            WorkspaceReplacementCount: 0,
            FileReplacementCount: changes.Length,
            Items: changes
                .Select((change, index) => new WorkspaceChangeItemDto(
                    BundleId: new BundleId(change.BundleId),
                    AssetId: new AssetId($"asset-{index}"),
                    BundleDisplayName: $"{change.BundleId}.ab",
                    AssetName: $"Asset {index}",
                    AssetType: AssetType.Texture2D,
                    IsModified: true,
                    LastExtractedAtUtc: null,
                    WorkspaceReplacementSourceBundleDisplayName: null,
                    WorkspaceReplacementSourceAssetName: null,
                    WorkspaceReplacementAppliedAtUtc: null,
                    FileReplacementSourceFileName: $"asset-{index}.png",
                    FileReplacementSourcePath: $@"C:\Replacements\asset-{index}.png",
                    FileReplacementAppliedAtUtc: change.LastChangedAtUtc,
                    LastChangedAtUtc: change.LastChangedAtUtc))
                .ToArray());
    }
}
