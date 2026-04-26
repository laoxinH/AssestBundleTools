using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 提供应用级别的运行日志访问入口。
/// </summary>
/// <remarks>
/// 详细日志不再附着在单个工作区页面内，而是提升为应用共享状态，
/// 这样“设置与诊断”页面可以集中展示不同页面产生的日志记录。
/// </remarks>
public interface IAppLogService
{
    LogPanelViewModel LogPanel { get; }

    void Clear();

    void AppendInfo(string message);

    void AppendWarning(string message);

    void AppendError(string message);
}
