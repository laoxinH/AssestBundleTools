namespace AssestBundleTools.Presentation.WinUI.Services;

public interface IOnlineComponentInstallPromptCoordinator
{
    Task<bool> EnsureInstalledAsync(string componentId);
}
