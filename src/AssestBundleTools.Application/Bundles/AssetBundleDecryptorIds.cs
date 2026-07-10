namespace AssestBundleTools.Application.Bundles;

/// <summary>
/// 统一收口内置解密器标识，避免不同层各自散落魔法字符串。
/// </summary>
public static class AssetBundleDecryptorIds
{
    /// <summary>
    /// 表示“由工作区导入链路自动探测合适解密器”的内置逻辑标识。
    /// 该标识本身不是一个真实可执行的解密器实现，而是导入阶段的解析策略。
    /// </summary>
    public const string BuiltInAutoDetect = "builtin.auto-detect";

    /// <summary>
    /// 表示“不做任何转换，直接把源文件交给 Reader”的内置回退解密器。
    /// </summary>
    public const string BuiltInPassThrough = "builtin.pass-through";

    /// <summary>
    /// 表示内置的明日方舟 UnityFS LZ4AK bundle codec。
    /// </summary>
    public const string BuiltInArknightsLz4Ak = "builtin.arknights-lz4ak";
}
