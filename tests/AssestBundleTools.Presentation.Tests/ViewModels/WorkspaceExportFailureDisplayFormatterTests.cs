using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

namespace AssestBundleTools.Presentation.Tests.ViewModels;

public sealed class WorkspaceExportFailureDisplayFormatterTests
{
    [Fact]
    public void TryFormat_ShouldMapExistingOutputDiagnostic_ToFriendlyText()
    {
        var formatter = new WorkspaceExportFailureDisplayFormatter(new TestLocalizationService());
        var error = ApplicationErrors.BundleExportOutputBlocked(
            @"D:\Exports\Alpha.ab",
            "Save.Output.FileAlreadyExists");

        var formatted = formatter.TryFormat(error, out var message);

        Assert.True(formatted);
        Assert.Equal(ResourceKeys.WorkspaceExportDialogOutputFileAlreadyExists, message);
        Assert.DoesNotContain("Save.Output.FileAlreadyExists", message);
    }

    [Fact]
    public void TryFormat_ShouldHideUnknownOutputDiagnosticCode()
    {
        var formatter = new WorkspaceExportFailureDisplayFormatter(new TestLocalizationService());
        var error = ApplicationErrors.BundleExportOutputBlocked(
            @"D:\Exports\Alpha.ab",
            "Save.Output.FutureDiagnostic");

        var formatted = formatter.TryFormat(error, out var message);

        Assert.True(formatted);
        Assert.Contains("Error.Bundle.Save.OutputBlocked", message);
        Assert.Contains(@"D:\Exports\Alpha.ab", message);
        Assert.DoesNotContain("Save.Output.FutureDiagnostic", message);
    }

    [Fact]
    public void Format_ShouldMapAudioContainerMismatch_ToFriendlyText()
    {
        var formatter = new WorkspaceExportFailureDisplayFormatter(new TestLocalizationService());
        var failedBundle = new WorkspaceSaveBundleExecutionResultDto(
            BundleId: new("bundle-alpha"),
            BundleDisplayName: "Alpha.ab",
            OutputFilePath: @"D:\Exports\Alpha.ab",
            Status: WorkspaceSaveBundleExecutionStatusDto.Failed,
            ChangeCount: 1,
            ModifiedAssetCount: 1,
            WorkspaceReplacementCount: 0,
            FileReplacementCount: 1,
            DiagnosticCode: "Bundle.Save.WriteFailed",
            DiagnosticDetail: "Save.BundleWrite.AudioPayloadContainerMismatch; Original=Wav; Replacement=Fsb5");

        var message = formatter.Format(failedBundle);

        Assert.Contains(ResourceKeys.WorkspaceExportDialogAudioPayloadContainerMismatch, message);
        Assert.DoesNotContain("Save.BundleWrite.AudioPayloadContainerMismatch", message);
    }
}
