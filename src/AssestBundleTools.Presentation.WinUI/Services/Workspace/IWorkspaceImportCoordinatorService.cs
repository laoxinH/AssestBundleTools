namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 统一协调工作区导入入口，负责把文件选择、活动工作区校验和轻量反馈收口到同一处。
/// </summary>
public interface IWorkspaceImportCoordinatorService
{
    /// <summary>
    /// 是否存在可承载导入请求的活动工作区。
    /// </summary>
    bool HasActiveWorkspace { get; }

    /// <summary>
    /// 从“资源包文件导入”入口收集输入，并登记到当前活动工作区。
    /// </summary>
    Task<bool> RequestBundleFileImportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 从“资源包目录导入”入口收集输入，并登记到当前活动工作区。
    /// </summary>
    Task<bool> RequestBundleDirectoryImportAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 将拖拽进入工作区的文件系统路径收口成统一导入请求，并登记到当前活动工作区。
    /// </summary>
    Task<bool> RequestDragDropImportAsync(
        IReadOnlyList<string> droppedPaths,
        CancellationToken cancellationToken = default);
}
