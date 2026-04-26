using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 表示“不需要解密，直接读取原文件”的默认回退解密器。
/// </summary>
public sealed class PassThroughAssetBundleDecryptor : IAssetBundleDecryptor
{
    public static AssetBundleDecryptorDescriptorDto DefaultDescriptor { get; } = new(
        Id: AssetBundleDecryptorIds.BuiltInPassThrough,
        DisplayName: "Pass Through",
        Version: "1.0.0",
        Kind: AssetBundleDecryptorKind.BuiltIn,
        SupportsAutoDetection: false,
        IsFallback: true,
        Description: "Do not transform the bundle source; pass it directly to the reader.");

    public AssetBundleDecryptorDescriptorDto Descriptor => DefaultDescriptor;

    public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
        AssetBundleDecryptorProbeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var fileExists = File.Exists(request.SourcePath);
        return Task.FromResult(new AssetBundleDecryptorProbeResultDto(
            IsSuccessful: fileExists,
            IsMatch: fileExists,
            Confidence: fileExists ? 1 : 0,
            Message: fileExists ? "Pass-through fallback available." : "Source file does not exist."));
    }

    public Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
        AssetBundleDecryptionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var result = new AssetBundleDecryptionResultDto(
            Decryptor: Descriptor,
            ResolvedBundlePath: Path.GetFullPath(request.SourcePath),
            ProducedDerivedFile: false,
            BundleFingerprint: AssetBundleFileFingerprintFactory.Create(request.SourcePath),
            Message: "Source bundle was passed through without decryption.");

        return Task.FromResult(Result<AssetBundleDecryptionResultDto>.Success(result));
    }
}
