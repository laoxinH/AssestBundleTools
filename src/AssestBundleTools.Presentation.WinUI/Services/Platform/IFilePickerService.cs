using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 封装 WinUI 层文件选择交互，避免 ViewModel 直接依赖窗口和系统选择器 API。
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// 选择当前工作区元数据和缓存建议落盘的位置。
    /// 返回 <see langword="null"/> 表示用户主动取消。
    /// </summary>
    Task<string?> PickWorkspaceDataDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 选择一个或多个待导入的资源包文件。
    /// 返回空集合表示用户主动取消，不应视为错误。
    /// </summary>
    Task<IReadOnlyList<string>> PickBundleFilePathsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 选择待导入资源包所在的目录。
    /// 返回 <see langword="null"/> 表示用户主动取消。
    /// </summary>
    Task<string?> PickBundleImportDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 选择 Spine 序列帧导出的目标目录。
    /// 返回 <see langword="null"/> 表示用户主动取消。
    /// </summary>
    Task<string?> PickSpineFrameSequenceExportDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 选择 Spine 资源组整组提取的目标目录。
    /// 返回 <see langword="null"/> 表示用户主动取消。
    /// </summary>
    Task<string?> PickSpineAssetGroupExtractionDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 为当前资源提取流程选择一个目标保存文件。
    /// 返回 <see langword="null"/> 表示用户主动取消，不应视为错误。
    /// </summary>
    Task<string?> PickAssetExtractionOutputFileAsync(
        AssetType assetType,
        string? assetTypeDisplayName,
        string assetName,
        string? assetPath,
        string? containerPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为资产列表多选批量提取流程选择目标目录。
    /// 返回 <see langword="null"/> 表示用户主动取消，不应视为错误。
    /// </summary>
    Task<string?> PickAssetBatchExtractionOutputDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 为工作区级批量替换选择外部来源目录。
    /// 返回 <see langword="null"/> 表示用户主动取消，不应视为错误。
    /// </summary>
    Task<string?> PickWorkspaceBatchReplacementSourceDirectoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 为当前资源替换流程选择一个外部文件来源。
    /// 返回 <see langword="null"/> 表示用户主动取消，不应视为错误。
    /// </summary>
    Task<string?> PickReplacementSourceFileAsync(
        AssetType targetAssetType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为 Spine 组成文件批量匹配选择一个外部来源目录。
    /// 返回 <see langword="null"/> 表示用户主动取消，不应视为错误。
    /// </summary>
    Task<string?> PickSpineReplacementSourceDirectoryAsync(CancellationToken cancellationToken = default);
}
