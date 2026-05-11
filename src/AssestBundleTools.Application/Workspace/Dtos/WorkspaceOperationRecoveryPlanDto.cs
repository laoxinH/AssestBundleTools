namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区操作记录恢复检查的结构化结果。
/// </summary>
public sealed record WorkspaceOperationRecoveryPlanDto(
    string WorkspaceKey,
    IReadOnlyList<WorkspaceOperationRecoveryItemDto> Items)
{
    public bool HasRecoverableItems => Items.Count > 0;

    public static WorkspaceOperationRecoveryPlanDto Empty(string workspaceKey) =>
        new(workspaceKey, Array.Empty<WorkspaceOperationRecoveryItemDto>());
}
