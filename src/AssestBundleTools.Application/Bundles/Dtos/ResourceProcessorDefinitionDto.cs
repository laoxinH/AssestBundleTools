namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示一个可编辑的导出资源处理器定义。
/// </summary>
public sealed record ResourceProcessorDefinitionDto(
    string Id,
    string DisplayName,
    string Version,
    string? Description,
    IReadOnlyList<string> ResourceKinds,
    string ExecutablePath,
    string? WorkingDirectoryPath,
    int TimeoutMs,
    IReadOnlyList<string> ProcessArguments);
