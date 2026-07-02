namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一个可供工作区导出选择的资源处理器目录项。
/// </summary>
public sealed record ResourceProcessorCatalogEntryDto(
    string Id,
    string DisplayName,
    string Version,
    string Description,
    ResourceProcessorKind Kind,
    IReadOnlyList<string> ResourceKinds,
    ResourceProcessorStatus Status,
    bool IsBuiltIn,
    bool IsReadOnly,
    string? ManifestFilePath,
    string? ExecutablePath,
    string? WorkingDirectoryPath,
    int TimeoutMs,
    IReadOnlyList<string> ProcessArguments);
