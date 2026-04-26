namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区导入过程中当前已识别的几个主要问题类别。
/// </summary>
public enum WorkspaceImportIssueKind
{
    CandidateScreenedOut = 0,
    DecryptionFailed = 1,
    ReaderRejected = 2,
    ReadFailed = 3
}
