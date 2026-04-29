namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示需要回写到工作区元数据中的页面级浏览状态。
/// </summary>
public sealed record WorkspaceBrowserStateSaveRequestDto(
    string WorkspaceKey,
    string? WorkspaceDataRootPath,
    string DisplayName,
    string SourcePath,
    string? AssetBundleDecryptorId,
    WorkspaceBrowserStateSnapshotDto BrowserState);
