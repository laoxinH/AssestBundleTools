namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 负责把本地路径交给系统壳层进行定位或打开。
/// </summary>
public interface IPathNavigationService
{
    bool TryOpenContainingFolder(string path);
}
