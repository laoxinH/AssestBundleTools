namespace AssestBundleTools.Application.Extraction.Dtos;

/// <summary>
/// 控制 Spine skeleton 提取文件名的扩展名，不改变输出载荷。
/// </summary>
public enum SpineSkeletonExportExtensionMode
{
    None = 0,
    UniformSkel = 1,
    ContentAware = 2
}
