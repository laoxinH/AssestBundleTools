using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;

namespace AssestBundleTools.Presentation.WinUI.Views.BundleExplorer;

internal interface IAssetDocumentGroupDragHost
{
    bool CanStartAssetDocumentGroupDrag();

    bool CanAcceptAssetDocumentGroupDrag(AssetDocumentGroupDragSession session);

    Task<AssetDocumentGroupTransfer?> DetachAssetDocumentGroupForDragAsync();

    Task<bool> AttachAssetDocumentGroupFromDragAsync(AssetDocumentGroupTransfer transfer);

    Task CompleteAssetDocumentGroupDragMoveAsync();

    Task OpenAssetDocumentGroupInNewWindowFromDragAsync();
}

internal sealed class AssetDocumentGroupDragSession
{
    public AssetDocumentGroupDragSession(string token, IAssetDocumentGroupDragHost sourceHost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        Token = token;
        SourceHost = sourceHost ?? throw new ArgumentNullException(nameof(sourceHost));
    }

    public string Token { get; }

    public IAssetDocumentGroupDragHost SourceHost { get; }
}

internal static class AssetDocumentGroupDragCoordinator
{
    private const string DataFormat = "AssestBundleTools.AssetDocumentGroupDragToken";
    private static AssetDocumentGroupDragSession? _currentSession;

    public static void StartDrag(IAssetDocumentGroupDragHost sourceHost, DragStartingEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sourceHost);
        ArgumentNullException.ThrowIfNull(args);

        if (!sourceHost.CanStartAssetDocumentGroupDrag())
        {
            args.Cancel = true;
            return;
        }

        _currentSession = new AssetDocumentGroupDragSession(Guid.NewGuid().ToString("N"), sourceHost);
        args.Data.RequestedOperation = DataPackageOperation.Move;
        args.Data.SetData(DataFormat, _currentSession.Token);
    }

    public static void HandleDragOver(IAssetDocumentGroupDragHost targetHost, DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(args);

        if (TryGetSameHostSession(args, targetHost, out _))
        {
            args.AcceptedOperation = DataPackageOperation.Move;
            args.Handled = true;
            return;
        }

        if (!TryGetCurrentSession(args, targetHost, out _))
        {
            return;
        }

        args.AcceptedOperation = DataPackageOperation.Move;
        args.Handled = true;
    }

    public static bool CanShowDropPreview(IAssetDocumentGroupDragHost targetHost, DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(args);

        return TryGetCurrentSession(args, targetHost, out _);
    }

    public static async Task<bool> HandleDropAsync(IAssetDocumentGroupDragHost targetHost, DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(targetHost);
        ArgumentNullException.ThrowIfNull(args);

        if (TryGetSameHostSession(args, targetHost, out var sameHostSession))
        {
            args.AcceptedOperation = DataPackageOperation.Move;
            args.Handled = true;
            ClearCurrentSession(sameHostSession);
            return true;
        }

        if (!TryGetCurrentSession(args, targetHost, out var session))
        {
            return false;
        }

        args.AcceptedOperation = DataPackageOperation.Move;
        args.Handled = true;
        var moved = await MoveAssetDocumentGroupAsync(session, targetHost);
        ClearCurrentSession(session);
        return moved;
    }

    public static async Task HandleDropCompletedAsync(
        IAssetDocumentGroupDragHost sourceHost,
        DropCompletedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(sourceHost);
        ArgumentNullException.ThrowIfNull(args);

        var session = _currentSession;
        if (session is null || !ReferenceEquals(session.SourceHost, sourceHost))
        {
            return;
        }

        try
        {
            if (args.DropResult == DataPackageOperation.None)
            {
                await sourceHost.OpenAssetDocumentGroupInNewWindowFromDragAsync();
            }
        }
        finally
        {
            ClearCurrentSession(session);
        }
    }

    public static void CompleteDrag()
    {
        _currentSession = null;
    }

    private static async Task<bool> MoveAssetDocumentGroupAsync(
        AssetDocumentGroupDragSession session,
        IAssetDocumentGroupDragHost targetHost)
    {
        var sourceHost = session.SourceHost;
        var transfer = await sourceHost.DetachAssetDocumentGroupForDragAsync();
        if (transfer is null)
        {
            return false;
        }

        if (await targetHost.AttachAssetDocumentGroupFromDragAsync(transfer))
        {
            await sourceHost.CompleteAssetDocumentGroupDragMoveAsync();
            return true;
        }

        await sourceHost.AttachAssetDocumentGroupFromDragAsync(transfer);
        return false;
    }

    private static bool TryGetCurrentSession(
        DragEventArgs args,
        IAssetDocumentGroupDragHost targetHost,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentGroupDragSession? session)
    {
        session = _currentSession;
        return session is not null &&
               !ReferenceEquals(session.SourceHost, targetHost) &&
               args.DataView.Contains(DataFormat) &&
               targetHost.CanAcceptAssetDocumentGroupDrag(session);
    }

    private static bool TryGetSameHostSession(
        DragEventArgs args,
        IAssetDocumentGroupDragHost targetHost,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssetDocumentGroupDragSession? session)
    {
        session = _currentSession;
        return session is not null &&
               ReferenceEquals(session.SourceHost, targetHost) &&
               args.DataView.Contains(DataFormat);
    }

    private static void ClearCurrentSession(AssetDocumentGroupDragSession session)
    {
        if (ReferenceEquals(_currentSession, session))
        {
            _currentSession = null;
        }
    }
}
