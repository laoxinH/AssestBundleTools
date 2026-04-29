namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 页面兼容属性桥接只关心“哪类页面状态发生变化”，
/// 避免继续依赖跨子 ViewModel 的 PropertyName 字符串转发。
/// </summary>
internal sealed class BundleExplorerPageStateChangedEventArgs<TChange> : EventArgs
    where TChange : struct, Enum
{
    public BundleExplorerPageStateChangedEventArgs(TChange changeFlags)
    {
        ChangeFlags = changeFlags;
    }

    public TChange ChangeFlags { get; }
}

[Flags]
internal enum BundleExplorerWorkspaceShellPageStateChange
{
    None = 0,
    WorkspaceAvailability = 1 << 0,
    Presentation = 1 << 1,
    StatusChips = 1 << 2
}

[Flags]
internal enum BundleExplorerBundlePanePageStateChange
{
    None = 0,
    SearchInput = 1 << 0,
    SearchChrome = 1 << 1,
    PaneChrome = 1 << 2,
    CollectionPresentation = 1 << 3,
    InteractionState = 1 << 4,
    SelectionSummary = 1 << 5
}

[Flags]
internal enum BundleExplorerDocumentWorkspacePageStateChange
{
    None = 0,
    ActiveBundleContext = 1 << 0,
    AssetDocumentHost = 1 << 1,
    HostSynchronization = 1 << 2
}

[Flags]
internal enum BundleExplorerAssetWorkflowPageStateChange
{
    None = 0,
    Inspector = 1 << 0,
    AssetCommandState = 1 << 1
}
