namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 负责解析工作区导入执行时的默认并发选项。
/// </summary>
public interface IWorkspaceImportExecutionOptionsResolver
{
    WorkspaceImportExecutionOptions ResolveOptions();
}
