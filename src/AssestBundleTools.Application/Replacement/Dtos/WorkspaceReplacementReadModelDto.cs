namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 面向后续统一导出 / 保存阶段的工作区替换读模型。
/// </summary>
public sealed record WorkspaceReplacementReadModelDto(
    string WorkspaceKey,
    IReadOnlyList<WorkspaceEffectiveReplacementDto> EffectiveReplacements);
