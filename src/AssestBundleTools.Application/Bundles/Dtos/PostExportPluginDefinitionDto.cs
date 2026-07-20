namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一个用户可管理的导出后插件定义。
/// </summary>
public sealed record PostExportPluginDefinitionDto(
    string Id,
    string DisplayName,
    string Version,
    string? Description,
    string ExecutablePath,
    string? WorkingDirectoryPath,
    IReadOnlyList<string> ProcessArguments);
