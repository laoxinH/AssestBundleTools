namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一个可在 AB 导出清单写出后执行的插件目录项。
/// </summary>
public sealed record PostExportPluginCatalogEntryDto(
    string Id,
    string DisplayName,
    string Version,
    string Description,
    ResourceProcessorStatus Status,
    bool IsReadOnly,
    string? ManifestFilePath,
    string ExecutablePath,
    string? WorkingDirectoryPath,
    IReadOnlyList<string> ProcessArguments);
