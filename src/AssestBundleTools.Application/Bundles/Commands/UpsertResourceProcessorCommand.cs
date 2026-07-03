using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Application.Bundles.Commands;

/// <summary>
/// 新增或更新一个用户可管理的导出资源处理器定义。
/// </summary>
public sealed record UpsertResourceProcessorCommand(
    ResourceProcessorDefinitionDto Definition,
    string? PreviousProcessorId = null);
