namespace AssestBundleTools.Presentation.WinUI.ViewModels.Home;

/// <summary>
/// 工作台“最近操作”列表项。
/// 该模型刻意保持轻量，方便未来直接由日志摘要服务或工作区事件流映射得到。
/// </summary>
public sealed class HomeRecentActivityItemViewModel
{
    public HomeRecentActivityItemViewModel(string time, string summary, string detail)
    {
        Time = time;
        Summary = summary;
        Detail = detail;
    }

    public string Time { get; }

    public string Summary { get; }

    public string Detail { get; }
}
