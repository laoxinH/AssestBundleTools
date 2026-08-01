using AssestBundleTools.Application.Bundles.Commands;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Tests.Testing;

namespace AssestBundleTools.Application.Tests.Bundles.Commands;

internal static class PluginPackageImportHandlerTests
{
    public static async Task Import_ShouldRejectBlankSourceWithoutCallingInstallationService()
    {
        var service = new RecordingInstallationService();
        var handler = new ImportPluginPackageHandler(service);

        var result = await handler.HandleAsync(new ImportPluginPackageCommand(" "));

        TestAssert.True(result.IsFailure, "Blank package source should fail validation.");
        TestAssert.Equal("Plugin.Package.SourceInvalid", TestAssert.Single(result.Errors).Code, "Validation should use the package source error contract.");
        TestAssert.Equal(0, service.CallCount, "Validation failure must not call Infrastructure.");
    }

    public static async Task Import_ShouldForwardReplacementChoiceAndReturnInstallationResult()
    {
        var expected = new PluginPackageImportResultDto(
            PluginPackageImportStatusDto.Installed,
            PluginPackageKindDto.ResourceProcessor,
            "plugin.test",
            "Plugin Test",
            "2.0.0",
            "C:\\Plugins\\plugin.test");
        var service = new RecordingInstallationService(Result<PluginPackageImportResultDto>.Success(expected));
        var handler = new ImportPluginPackageHandler(service);

        var result = await handler.HandleAsync(new ImportPluginPackageCommand("  C:\\Package.zip  ", true));

        TestAssert.True(result.IsSuccess, "Valid package import should return the installation result.");
        TestAssert.Equal(expected, result.Value, "Handler should preserve the structured installation result.");
        TestAssert.Equal("C:\\Package.zip", service.SourcePath, "Handler should trim the selected source path.");
        TestAssert.True(service.ReplaceExisting, "Handler should forward the confirmed replacement choice.");
    }

    private sealed class RecordingInstallationService : IPluginPackageInstallationService
    {
        private readonly Result<PluginPackageImportResultDto> _result;

        public RecordingInstallationService(Result<PluginPackageImportResultDto>? result = null)
        {
            _result = result ?? Result<PluginPackageImportResultDto>.Failure(
                AssestBundleTools.Application.Common.Errors.ApplicationErrors.PluginPackageInstallFailed("UnexpectedCall"));
        }

        public int CallCount { get; private set; }

        public string? SourcePath { get; private set; }

        public bool ReplaceExisting { get; private set; }

        public Task<Result<PluginPackageImportResultDto>> ImportAsync(
            string sourcePath,
            bool replaceExisting,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            SourcePath = sourcePath;
            ReplaceExisting = replaceExisting;
            return Task.FromResult(_result);
        }
    }
}
