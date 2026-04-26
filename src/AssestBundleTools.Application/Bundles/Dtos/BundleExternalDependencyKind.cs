namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示 Bundle 外部依赖的来源类型。
/// </summary>
public enum BundleExternalDependencyKind
{
    Unknown = 0,
    ArchiveSerializedFile = 1,
    FilePath = 2
}
