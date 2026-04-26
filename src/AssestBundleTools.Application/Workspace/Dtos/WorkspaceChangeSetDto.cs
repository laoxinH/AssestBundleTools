namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个工作区当前持久化的变更暂存集合。
/// </summary>
public sealed record WorkspaceChangeSetDto(
    string WorkspaceKey,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<WorkspaceChangeRecordDto> Changes)
{
    public static WorkspaceChangeSetDto Empty(string workspaceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        return new WorkspaceChangeSetDto(workspaceKey, DateTimeOffset.UtcNow, Array.Empty<WorkspaceChangeRecordDto>());
    }
}
