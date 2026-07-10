namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一个可编辑的解密器定义。
/// </summary>
public sealed record AssetBundleDecryptorDefinitionDto(
    string Id,
    string DisplayName,
    string Version,
    string? Description,
    bool SupportsAutoDetection,
    bool IsFallback,
    string ExecutablePath,
    string? WorkingDirectoryPath,
    int TimeoutMs,
    IReadOnlyList<string> ProbeArguments,
    IReadOnlyList<string> DecryptArguments,
    IReadOnlyList<string>? EncryptArguments = null);
