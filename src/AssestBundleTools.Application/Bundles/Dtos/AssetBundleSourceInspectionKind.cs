namespace AssestBundleTools.Application.Bundles.Dtos;

/// <summary>
/// 表示导入来源在“解密前直检”阶段的识别结果。
/// </summary>
public enum AssetBundleSourceInspectionKind
{
    Bundle = 0,
    Undetermined = 1,
    Rejected = 2
}
