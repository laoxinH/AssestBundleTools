using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal interface IAssetDocumentTabDragHost
{
    bool CanStartAssetDocumentTabDrag(AssetDocumentTabItemViewModel documentTab);

    bool CanAcceptAssetDocumentTabDrag(AssetDocumentTabDragSession session);

    Task<AssetDocumentTabItemViewModel?> DetachAssetDocumentTabForDragAsync(AssetDocumentTabItemViewModel documentTab);

    Task<bool> AttachAssetDocumentTabFromDragAsync(AssetDocumentTabItemViewModel documentTab);

    Task CompleteAssetDocumentTabDragMoveAsync();
}

internal sealed class AssetDocumentTabDragSession
{
    public AssetDocumentTabDragSession(
        string token,
        IAssetDocumentTabDragHost sourceHost,
        AssetDocumentTabItemViewModel documentTab)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Token = token;
        SourceHost = sourceHost ?? throw new ArgumentNullException(nameof(sourceHost));
        DocumentTab = documentTab ?? throw new ArgumentNullException(nameof(documentTab));
    }

    public string Token { get; }

    public IAssetDocumentTabDragHost SourceHost { get; }

    public AssetDocumentTabItemViewModel DocumentTab { get; }
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

        if (!TryResolveDocumentTab(args.Item, args.Tab, out var documentTab) ||
            !sourceHost.CanStartAssetDocumentTabDrag(documentTab))
        {
            args.Cancel = true;
            return;
        }

        _currentSession = new AssetDocumentTabDragSession(
            Guid.NewGuid().ToString("N"),
            sourceHost,
            documentTab);
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

    public static bool CanShowDropPreview(IAssetDocumentTabDragHost targetHost, DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(args);

        return TryGetCurrentSession(args, targetHost, out _);
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

    public static bool TryResolveDocumentTab(
        object? item,
        TabViewItem? tab,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentTabItemViewModel? documentTab)
    {
        documentTab = item as AssetDocumentTabItemViewModel ??
                      tab?.DataContext as AssetDocumentTabItemViewModel;
        return documentTab is not null;
    }

    private static async Task<bool> MoveAssetDocumentTabAsync(
        AssetDocumentTabDragSession session,
        IAssetDocumentTabDragHost targetHost)
    {
        var sourceHost = session.SourceHost;
        var movedDocumentTab = await sourceHost.DetachAssetDocumentTabForDragAsync(session.DocumentTab);
        if (movedDocumentTab is null)
        {
            return false;
        }

        if (await targetHost.AttachAssetDocumentTabFromDragAsync(movedDocumentTab))
        {
            await sourceHost.CompleteAssetDocumentTabDragMoveAsync();
            return true;
        }

        await sourceHost.AttachAssetDocumentTabFromDragAsync(movedDocumentTab);
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
