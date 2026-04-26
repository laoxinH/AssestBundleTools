namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示工作区导入处理中对单个资源包缓存做出的判定。
/// </summary>
public enum WorkspaceImportCacheDecisionKind
{
    CacheMiss = 0,
    Reused = 1,
    SourceFingerprintChanged = 2,
    DecryptorChanged = 3,
    DecryptorVersionChanged = 4,
    ToolVersionChanged = 5,
    ParserVersionChanged = 6,
    SourceRemoved = 7
}
