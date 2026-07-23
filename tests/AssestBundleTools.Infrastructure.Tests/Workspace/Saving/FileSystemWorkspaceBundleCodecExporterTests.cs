using System.Text;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static partial class FileSystemWorkspaceBundleExporterTests
{
    public static async Task ExportAsync_ShouldApplySourceCodec_AfterStandardObjectWrite()
    {
        if (!File.Exists(RealSampleBundlePath) || !Environment.Is64BitProcess)
        {
            return;
        }

        using var scope = new TemporaryDirectoryScope();
        var sourcePath = Path.Combine(scope.DirectoryPath, "cache", Path.GetFileName(RealSampleBundlePath));
        var outputPath = Path.Combine(scope.DirectoryPath, "exports", Path.GetFileName(RealSampleBundlePath));
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.Copy(RealSampleBundlePath, sourcePath);
        var probe = ProbeFirstWritableTextAsset(sourcePath);
        if (probe is null)
        {
            return;
        }

        var payloadPath = Path.Combine(scope.DirectoryPath, "payload.txt");
        var replacementText = $"abt-codec-stage-{Guid.NewGuid():N}";
        await File.WriteAllBytesAsync(payloadPath, Encoding.UTF8.GetBytes(replacementText));
        var codec = new RecordingAssetBundleCodec(
            probe.SerializedFileName,
            probe.PathId);
        var sourceCodecParameters = new Dictionary<string, string>
        {
            ["crosscore.outerPrefixBase64"] = "VW5pdHlGUwA="
        };
        var exporter = new FileSystemWorkspaceBundleExporter(new SingleDecryptorRegistry(codec));

        var result = await exporter.ExportAsync(new WorkspaceBundleExportRequestDto(
            WorkspaceId: new WorkspaceId("workspace"),
            WorkspaceDisplayName: "Workspace",
            BundleId: new BundleId("bundle"),
            BundleDisplayName: Path.GetFileName(sourcePath),
            OutputFilePath: outputPath,
            SourceArtifactPath: sourcePath,
            BundleCacheKey: "bundle-cache-key",
            ChangeItems: Array.Empty<WorkspaceChangeItemDto>(),
            AssetWriteItems:
            [
                new WorkspaceBundleAssetWriteItemDto(
                    BundleId: new BundleId("bundle"),
                    AssetId: new AssetId("asset-text"),
                    AssetName: probe.AssetName,
                    AssetType: AssetType.TextAsset,
                    AssetTypeDisplayName: nameof(AssetType.TextAsset),
                    SerializedFileName: probe.SerializedFileName,
                    PathId: probe.PathId,
                    SourceKind: "file",
                    SourceFilePath: payloadPath)
            ],
            ProcessorSelections: Array.Empty<WorkspaceExportProcessorSelectionDto>(),
            ProcessorResults: Array.Empty<WorkspaceResourceProcessorExecutionResultDto>(),
            OperationId: "operation",
            WorkspaceKey: "workspace-key",
            WorkspaceDataRootPath: scope.DirectoryPath,
            SourceCodecId: codec.Descriptor.Id,
            SourceCodecVersion: codec.Descriptor.Version,
            SourceCodecParameters: sourceCodecParameters));

        TestAssert.True(result.IsSuccess, "Bundle export should apply an available source codec.");
        TestAssert.Equal(1, codec.ExecutionCount, "Source codec should run exactly once per bundle output.");
        TestAssert.Equal(
            replacementText,
            codec.ObservedTextAsset,
            "Source codec should receive the standard bundle after object changes are written.");
        TestAssert.Equal(codec.Descriptor.Id, result.Value!.OutputCodecId, "Export result should record output codec id.");
        TestAssert.Equal(codec.Descriptor.Version, result.Value.OutputCodecVersion, "Export result should record output codec version.");
        TestAssert.Equal(
            sourceCodecParameters["crosscore.outerPrefixBase64"],
            codec.ObservedParameters!["crosscore.outerPrefixBase64"],
            "Export should pass persisted codec parameters to the source encryptor unchanged.");
        TestAssert.True(
            File.ReadAllBytes(outputPath).AsSpan().EndsWith(RecordingAssetBundleCodec.Marker),
            "Final hash and promotion should use the codec output rather than the standard temporary bundle.");
    }

    private sealed class RecordingAssetBundleCodec : IAssetBundleDecryptor, IAssetBundleEncryptor
    {
        public static ReadOnlySpan<byte> Marker => "ABT-CODEC"u8;

        private readonly string _serializedFileName;
        private readonly long _pathId;

        public RecordingAssetBundleCodec(string serializedFileName, long pathId)
        {
            _serializedFileName = serializedFileName;
            _pathId = pathId;
        }

        public AssetBundleDecryptorDescriptorDto Descriptor { get; } = new(
            Id: "codec.test",
            DisplayName: "codec.test",
            Version: "1.0.0",
            Kind: AssetBundleDecryptorKind.BuiltIn,
            SupportsAutoDetection: false,
            IsFallback: false,
            Description: null,
            SupportsEncryption: true);

        public int ExecutionCount { get; private set; }

        public string? ObservedTextAsset { get; private set; }

        public IReadOnlyDictionary<string, string>? ObservedParameters { get; private set; }

        public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
            AssetBundleDecryptorProbeRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
            AssetBundleDecryptionRequestDto request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<AssetBundleEncryptionResultDto>> EncryptAsync(
            AssetBundleEncryptionRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            ObservedTextAsset = ReadTextAsset(request.SourcePath, _serializedFileName, _pathId);
            ObservedParameters = request.Parameters;
            File.Copy(request.SourcePath, request.OutputPath, overwrite: true);
            using (var stream = new FileStream(request.OutputPath, FileMode.Append, FileAccess.Write, FileShare.None))
            {
                stream.Write(Marker);
            }

            var fileInfo = new FileInfo(request.OutputPath);
            return Task.FromResult(Result<AssetBundleEncryptionResultDto>.Success(
                new AssetBundleEncryptionResultDto(
                    Encryptor: Descriptor,
                    ResolvedBundlePath: request.OutputPath,
                    BundleFingerprint: new WorkspaceFileFingerprintDto(
                        request.OutputPath,
                        fileInfo.Length,
                        new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero),
                        "codec-test"),
                    DiagnosticMessage: null)));
        }
    }

    private sealed class SingleDecryptorRegistry : IAssetBundleDecryptorRegistry
    {
        private readonly IAssetBundleDecryptor _decryptor;

        public SingleDecryptorRegistry(IAssetBundleDecryptor decryptor)
        {
            _decryptor = decryptor;
        }

        public IReadOnlyList<IAssetBundleDecryptor> GetAll() => [_decryptor];

        public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
        {
            decryptor = _decryptor;
            return string.Equals(
                decryptorId,
                _decryptor.Descriptor.Id,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
