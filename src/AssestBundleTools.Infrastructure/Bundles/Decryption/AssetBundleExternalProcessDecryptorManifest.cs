namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 表示外部进程型解密器的清单文件结构。
/// </summary>
internal sealed class AssetBundleExternalProcessDecryptorManifest
{
    public int SchemaVersion { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool SupportsAutoDetection { get; init; }

    public bool IsFallback { get; init; }

    public string ExecutablePath { get; init; } = string.Empty;

    public string? WorkingDirectoryPath { get; init; }

    public int TimeoutMs { get; init; } = 15000;

    public IReadOnlyList<string> ProbeArguments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> DecryptArguments { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> EncryptArguments { get; init; } = Array.Empty<string>();
}
