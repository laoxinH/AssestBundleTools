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

    public static async Task ExecuteAsync_ShouldRunBuiltInFmodWrapper_AndProduceFsbResult()
    {
        using var scope = new TemporaryDirectoryScope();
        Directory.CreateDirectory(scope.DirectoryPath);
        var inputPath = Path.Combine(scope.DirectoryPath, "input.wav");
        var fsbankPath = CreateScript(
            scope.DirectoryPath,
            """
            @echo off
            >"output.fsb" echo FSB5-test-payload
            """);
        await File.WriteAllBytesAsync(inputPath, CreateTinyWavePayload());
        var wrapperPath = ResolveBuiltInFmodWrapperPath();
        if (string.IsNullOrWhiteSpace(wrapperPath) || !File.Exists(wrapperPath))
        {
            Console.WriteLine("[SKIP] Built-in FMOD wrapper has not been built yet.");
            return;
        }

        var executor = new FileSystemResourceProcessorExecutor();
        var result = await executor.ExecuteAsync(CreateAudioWorkItem(scope.DirectoryPath, wrapperPath, fsbankPath, inputPath));

        TestAssert.True(result.IsSuccess, "Built-in FMOD wrapper should produce a processor result JSON.");
        TestAssert.Equal(WorkspaceResourceProcessorStatusDto.Succeeded, result.Value!.Status, "Wrapper result should be successful.");
        TestAssert.True(File.Exists(result.Value.OutputPath), "Wrapper should produce an FSB payload at the catalog output path.");
        var outputBytes = await File.ReadAllBytesAsync(result.Value.OutputPath!);
        TestAssert.Equal((byte)'F', outputBytes[0], "Wrapper output should be the FSB payload produced by fsbankcl.");
        TestAssert.Equal("audio/x-fsb", result.Value.ContentType, "Wrapper result should identify FSB payloads.");
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

    private static WorkspaceResourceProcessorWorkItemDto CreateAudioWorkItem(
        string rootDirectory,
        string wrapperPath,
        string fsbankPath,
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
            AssetId: new AssetId("asset-audio"),
            AssetName: "HeroVoice",
            AssetType: AssetType.AudioClip,
            ResourceKind: "audio",
            InputPayloadPath: inputPath,
            StagingDirectoryPath: Path.Combine(rootDirectory, "staging", Guid.NewGuid().ToString("N")),
            Processor: new ResourceProcessorCatalogEntryDto(
                Id: "resource.processor.audio.fmod-fsb5",
                DisplayName: "resource.processor.audio.fmod-fsb5",
                Version: "1.0.0",
                Description: string.Empty,
                Kind: ResourceProcessorKind.ExternalProcess,
                ResourceKinds: ["Audio"],
                Status: ResourceProcessorStatus.Available,
                IsBuiltIn: true,
                IsReadOnly: true,
                ManifestFilePath: null,
                ExecutablePath: wrapperPath,
                WorkingDirectoryPath: null,
                TimeoutMs: 15000,
                ProcessArguments:
                [
                    "--input",
                    "{inputPath}",
                    "--output",
                    "{outputPath}",
                    "--result",
                    "{resultPath}",
                    "--fsbank",
                    fsbankPath
                ]));
    }

    private static string CreateScript(string directoryPath, string content)
    {
        Directory.CreateDirectory(directoryPath);
        var scriptPath = Path.Combine(directoryPath, $"processor-{Guid.NewGuid():N}.cmd");
        File.WriteAllText(scriptPath, content.Replace("\r\n", "\n").Replace("\n", "\r\n"), Encoding.ASCII);
        return scriptPath;
    }

    private static string? ResolveBuiltInFmodWrapperPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "AssestBundleTools.Audio.FmodProcessor.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "AssestBundleTools.Audio.FmodProcessor", "bin", "Debug", "net8.0", "AssestBundleTools.Audio.FmodProcessor.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static byte[] CreateTinyWavePayload()
    {
        var payload = new byte[44];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(payload, 0);
        BitConverter.GetBytes(36).CopyTo(payload, 4);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(payload, 8);
        BitConverter.GetBytes(16).CopyTo(payload, 16);
        BitConverter.GetBytes((short)1).CopyTo(payload, 20);
        BitConverter.GetBytes((short)1).CopyTo(payload, 22);
        BitConverter.GetBytes(8000).CopyTo(payload, 24);
        BitConverter.GetBytes(16000).CopyTo(payload, 28);
        BitConverter.GetBytes((short)2).CopyTo(payload, 32);
        BitConverter.GetBytes((short)16).CopyTo(payload, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(payload, 36);
        BitConverter.GetBytes(0).CopyTo(payload, 40);
        return payload;
    }
}
