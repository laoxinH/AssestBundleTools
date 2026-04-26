namespace AssestBundleTools.Domain.Bundles;

/// <summary>
/// 表示工作区内 Bundle 的稳定标识。
/// </summary>
public readonly record struct BundleId
{
    public BundleId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static bool TryParse(string? value, out BundleId bundleId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            bundleId = default;
            return false;
        }

        bundleId = new BundleId(value);
        return true;
    }

    public override string ToString() => Value;
}
