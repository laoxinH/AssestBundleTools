namespace AssestBundleTools.Application.Saving.Dtos;

/// <summary>
/// 表示保存 / 重建计划中某个目标输出路径的预检状态。
/// </summary>
public enum WorkspaceSaveOutputReadiness
{
    NotProbed = 0,
    Ready = 1,
    FileAlreadyExists = 2,
    TargetIsDirectory = 3,
    DirectoryBlockedByFile = 4,
    InvalidPath = 5,
    ProbeFailed = 6
}
