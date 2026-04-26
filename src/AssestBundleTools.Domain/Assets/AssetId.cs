namespace AssestBundleTools.Domain.Assets;

/// <summary>
/// 表示工作区或 Bundle 内资源对象的稳定标识。
/// </summary>
public readonly record struct AssetId
{
    public AssetId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static bool TryParse(string? value, out AssetId assetId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            assetId = default;
            return false;
        }

        assetId = new AssetId(value);
        return true;
    }

    public override string ToString() => Value;
}
