using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示解密器输出的可读取资源包结果。
/// </summary>
public sealed record AssetBundleDecryptionResultDto(
    AssetBundleDecryptorDescriptorDto Decryptor,
    string ResolvedBundlePath,
    bool ProducedDerivedFile,
    WorkspaceFileFingerprintDto BundleFingerprint,
    LocalizedMessageDto? DiagnosticMessage,
    IReadOnlyDictionary<string, string>? CodecParameters = null);
