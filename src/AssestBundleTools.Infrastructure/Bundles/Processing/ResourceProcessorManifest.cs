namespace AssestBundleTools.Infrastructure.Bundles.Processing;

/// <summary>
/// 表示外部进程型导出资源处理器的清单文件结构。
/// </summary>
internal sealed class ResourceProcessorManifest
{
    public int SchemaVersion { get; init; }

    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Kind { get; init; } = string.Empty;

    public IReadOnlyList<string> ResourceKinds { get; init; } = Array.Empty<string>();

    public string ExecutablePath { get; init; } = string.Empty;

    public string? WorkingDirectoryPath { get; init; }

    public int TimeoutMs { get; init; } = 15000;

    public IReadOnlyList<string> ProcessArguments { get; init; } = Array.Empty<string>();
}
