using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示来源格式回写后的资源包结果。
/// </summary>
public sealed record AssetBundleEncryptionResultDto(
    AssetBundleDecryptorDescriptorDto Encryptor,
    string ResolvedBundlePath,
    WorkspaceFileFingerprintDto BundleFingerprint,
    LocalizedMessageDto? DiagnosticMessage);
