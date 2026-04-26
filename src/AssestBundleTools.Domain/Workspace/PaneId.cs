namespace AssestBundleTools.Domain.Workspace;

/// <summary>
/// 表示工作区中的逻辑浏览窗格标识。
/// </summary>
public readonly record struct PaneId
{
    public PaneId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static PaneId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
