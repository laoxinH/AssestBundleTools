namespace AssestBundleTools.Domain.Workspace;

/// <summary>
/// 表示一次工作区会话的稳定标识。
/// </summary>
public readonly record struct WorkspaceId
{
    public WorkspaceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static WorkspaceId New() => new(Guid.NewGuid().ToString("N"));

    public static bool TryParse(string? value, out WorkspaceId workspaceId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            workspaceId = default;
            return false;
        }

        workspaceId = new WorkspaceId(value);
        return true;
    }

    public override string ToString() => Value;
}
