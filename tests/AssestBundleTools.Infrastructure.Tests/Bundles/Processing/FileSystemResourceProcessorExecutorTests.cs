using System.Text;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Bundles.Processing;
using AssestBundleTools.Infrastructure.Tests.Testing;

namespace AssestBundleTools.Infrastructure.Tests.Bundles.Processing;

internal static class FileSystemResourceProcessorExecutorTests
{
    public static async Task ExecuteAsync_ShouldRunCli_AndParseResultJson()
    {
        using var scope = new TemporaryDirectoryScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        var inputPath = Path.Combine(scope.DirectoryPath, "input.png");
        await File.WriteAllTextAsync(inputPath, "payload");
        var scriptPath = CreateScript(
            scope.DirectoryPath,
            """
            @echo off
            set "OUT=%~4"
            set "RES=%~6"
            echo processed>"%OUT%"
            >"%RES%" echo {"schemaVersion":1,"status":"succeeded","outputPath":"%OUT:\=/%","contentType":"image/png","diagnostics":[{"code":"Processor.Test","severity":"info","message":"ok"}]}
            """);
        var executor = new FileSystemResourceProcessorExecutor();

        var result = await executor.ExecuteAsync(CreateWorkItem(scope.DirectoryPath, scriptPath, inputPath));

        TestAssert.True(result.IsSuccess, "Executor should succeed when CLI writes output and result JSON.");
        TestAssert.Equal(WorkspaceResourceProcessorStatusDto.Succeeded, result.Value!.Status, "Result status should come from result JSON.");
        TestAssert.True(File.Exists(result.Value.OutputPath), "Output path from result JSON should exist.");
        TestAssert.Equal("image/png", result.Value.ContentType, "Content type should be parsed from result JSON.");
        var diagnostic = TestAssert.Single(result.Value.Diagnostics);
        TestAssert.Equal("Processor.Test", diagnostic.Code, "Diagnostics should be parsed from result JSON.");
    }

    public static async Task ExecuteAsync_ShouldRejectOutputPathOutsideStaging()
    {
        using var scope = new TemporaryDirectoryScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        var inputPath = Path.Combine(scope.DirectoryPath, "input.png");
        var outsidePath = Path.Combine(scope.DirectoryPath, "outside.png");
        await File.WriteAllTextAsync(inputPath, "payload");
        await File.WriteAllTextAsync(outsidePath, "outside");
        var scriptPath = CreateScript(
            scope.DirectoryPath,
            $$"""
            @echo off
            set "RES=%~6"
            >"%RES%" echo {"schemaVersion":1,"status":"succeeded","outputPath":"{{outsidePath.Replace("\\", "/")}}"}
            """);
        var executor = new FileSystemResourceProcessorExecutor();

        var result = await executor.ExecuteAsync(CreateWorkItem(scope.DirectoryPath, scriptPath, inputPath));

        TestAssert.True(result.IsFailure, "Executor should reject result output paths outside the work item staging directory.");
        TestAssert.Equal(
            "Bundle.Save.ResourceProcessor.ResultInvalid",
            TestAssert.Single(result.Errors).Code,
            "Failure should use the stable invalid-result error code.");
    }

    private static WorkspaceResourceProcessorWorkItemDto CreateWorkItem(
        string rootDirectory,
        string scriptPath,
        string inputPath)
    {
        return new WorkspaceResourceProcessorWorkItemDto(
            WorkspaceId: new WorkspaceId("workspace"),
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: rootDirectory,
            OperationId: "operation",
            BundleId: new BundleId("bundle"),
            BundleDisplayName: "bundle.ab",
            BundleCacheKey: "bundle-cache",
            AssetId: new AssetId("asset"),
            AssetName: "HeroTexture",
            AssetType: AssetType.Texture2D,
            ResourceKind: "texture2d",
            InputPayloadPath: inputPath,
            StagingDirectoryPath: Path.Combine(rootDirectory, "staging", Guid.NewGuid().ToString("N")),
            Processor: new ResourceProcessorCatalogEntryDto(
                Id: "processor.texture",
                DisplayName: "Processor",
                Version: "1.0.0",
                Description: string.Empty,
                Kind: ResourceProcessorKind.ExternalProcess,
                ResourceKinds: ["Texture2D"],
                Status: ResourceProcessorStatus.Available,
                IsBuiltIn: false,
                IsReadOnly: false,
                ManifestFilePath: Path.Combine(rootDirectory, "processor.json"),
                ExecutablePath: Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                WorkingDirectoryPath: null,
                TimeoutMs: 15000,
                ProcessArguments:
                [
                    "/c",
                    scriptPath,
                    "--input",
                    "{inputPath}",
                    "--output",
                    "{outputPath}",
                    "--result",
                    "{resultPath}",
                    "--resource-kind",
                    "{resourceKind}"
                ]));
    }

    private static string CreateScript(string directoryPath, string content)
    {
        Directory.CreateDirectory(directoryPath);
        var scriptPath = Path.Combine(directoryPath, $"processor-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(scriptPath, content.Replace("\r\n", "\n").Replace("\n", "\r\n"), Encoding.ASCII);
        return scriptPath;
    }
}
