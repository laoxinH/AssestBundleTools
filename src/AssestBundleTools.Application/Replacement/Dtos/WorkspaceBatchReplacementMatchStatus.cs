namespace AssestBundleTools.Application.Replacement.Dtos;

/// <summary>
/// 表示批量替换计划中单个来源文件的匹配状态。
/// </summary>
public enum WorkspaceBatchReplacementMatchStatus
{
    Changed = 0,
    Unchanged = 1,
    Missing = 2,
    TargetMissing = 3,
    TypeMismatch = 4,
    HashUnavailable = 5,
    ManifestInvalid = 6,
    HighConfidence = 7,
    MediumConfidence = 8,
    LowConfidence = 9,
    Ambiguous = 10,
    Rejected = 11
}
