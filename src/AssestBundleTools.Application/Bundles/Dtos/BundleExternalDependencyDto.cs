namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示资源所属 Bundle 的外部依赖摘要。
/// </summary>
/// <remarks>
/// 当前阶段先保留稳定的引用路径语义，
/// 供工作台检查器展示“这个 Bundle 还依赖哪些外部序列化文件/资源包”。
/// </remarks>
public sealed record BundleExternalDependencyDto(
    string ReferencePath,
    string DisplayName,
    BundleExternalDependencyKind Kind,
    string? ReferenceDisplayName = null,
    string? ResolvedBundleDisplayName = null);
