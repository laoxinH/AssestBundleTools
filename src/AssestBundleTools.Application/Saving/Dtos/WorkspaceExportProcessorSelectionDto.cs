namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示导出保存阶段针对某类资源选择的预处理器。
/// </summary>
public sealed record WorkspaceExportProcessorSelectionDto(
    string ResourceKind,
    string? ProcessorId);
