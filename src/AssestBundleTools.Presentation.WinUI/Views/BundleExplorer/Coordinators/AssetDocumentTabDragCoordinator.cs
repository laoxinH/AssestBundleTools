using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal interface IAssetDocumentTabDragHost
{
    bool CanStartAssetDocumentTabDrag(AssetDocumentTabViewModel assetTab);

    bool CanAcceptAssetDocumentTabDrag(AssetDocumentTabDragSession session);

    Task<AssetDocumentTabViewModel?> DetachAssetDocumentTabForDragAsync(AssetDocumentTabViewModel assetTab);

    Task<bool> AttachAssetDocumentTabFromDragAsync(AssetDocumentTabViewModel assetTab);

    Task CompleteAssetDocumentTabDragMoveAsync();
}

internal sealed class AssetDocumentTabDragSession
{
    public AssetDocumentTabDragSession(
        string token,
        IAssetDocumentTabDragHost sourceHost,
        AssetDocumentTabViewModel assetTab)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Token = token;
        SourceHost = sourceHost ?? throw new ArgumentNullException(nameof(sourceHost));
        AssetTab = assetTab ?? throw new ArgumentNullException(nameof(assetTab));
    }

    public string Token { get; }

    public IAssetDocumentTabDragHost SourceHost { get; }

    public AssetDocumentTabViewModel AssetTab { get; }
}

internal static class AssetDocumentTabDragCoordinator
{
    private const string DataFormat = "AssestBundleTools.AssetDocumentTabDragToken";
    private static AssetDocumentTabDragSession? _currentSession;

    public static void StartDrag(
        IAssetDocumentTabDragHost sourceHost,
        TabViewTabDragStartingEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sourceHost);
        ArgumentNullException.ThrowIfNull(args);

        if (!TryResolveAssetTab(args.Item, args.Tab, out var assetTab) ||
            !sourceHost.CanStartAssetDocumentTabDrag(assetTab))
        {
            args.Cancel = true;
            return;
        }

        _currentSession = new AssetDocumentTabDragSession(
            Guid.NewGuid().ToString("N"),
            sourceHost,
            assetTab);
        args.Data.RequestedOperation = DataPackageOperation.Move;
        args.Data.SetData(DataFormat, _currentSession.Token);
    }

    public static void HandleDragOver(
        IAssetDocumentTabDragHost targetHost,
        DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(args);

        if (!TryGetCurrentSession(args, targetHost, out _))
        {
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Move;
        args.Handled = true;
    }

    public static async Task<bool> HandleDropAsync(
        IAssetDocumentTabDragHost targetHost,
        DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(args);

        if (!TryGetCurrentSession(args, targetHost, out var session))
        {
            return false;
        }

        args.AcceptedOperation = DataPackageOperation.Move;
        args.Handled = true;
        var moved = await MoveAssetDocumentTabAsync(session, targetHost);
        ClearCurrentSession(session);
        return moved;
    }

    public static void CompleteDrag()
    {
        _currentSession = null;
    }

    public static bool TryResolveAssetTab(
        object? item,
        TabViewItem? tab,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentTabViewModel? assetTab)
    {
        assetTab = item as AssetDocumentTabViewModel ??
                   tab?.DataContext as AssetDocumentTabViewModel;
        return assetTab is not null;
    }

    private static async Task<bool> MoveAssetDocumentTabAsync(
        AssetDocumentTabDragSession session,
        IAssetDocumentTabDragHost targetHost)
    {
        var sourceHost = session.SourceHost;
        var movedAssetTab = await sourceHost.DetachAssetDocumentTabForDragAsync(session.AssetTab);
        if (movedAssetTab is null)
        {
            return false;
        }

        if (await targetHost.AttachAssetDocumentTabFromDragAsync(movedAssetTab))
        {
            await sourceHost.CompleteAssetDocumentTabDragMoveAsync();
            return true;
        }

        await sourceHost.AttachAssetDocumentTabFromDragAsync(movedAssetTab);
        return false;
    }

    private static bool TryGetCurrentSession(
        DragEventArgs args,
        IAssetDocumentTabDragHost targetHost,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentTabDragSession? session)
    {
        session = _currentSession;
        return session is not null &&
               !ReferenceEquals(session.SourceHost, targetHost) &&
               args.DataView.Contains(DataFormat) &&
               targetHost.CanAcceptAssetDocumentTabDrag(session);
    }

    private static void ClearCurrentSession(AssetDocumentTabDragSession session)
    {
        if (ReferenceEquals(_currentSession, session))
        {
            _currentSession = null;
        }
    }
}
