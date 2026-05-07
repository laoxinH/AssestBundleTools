using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Queries;

namespace AssestBundleTools.Application.Editing.Services;

public sealed class AssetEditDraftRestorePlanGateway : IAssetEditDraftRestorePlanGateway
{
    private readonly GetAssetEditDraftRestorePlanHandler _getRestorePlanHandler;

    public AssetEditDraftRestorePlanGateway(GetAssetEditDraftRestorePlanHandler getRestorePlanHandler)
    {
        _getRestorePlanHandler = getRestorePlanHandler ?? throw new ArgumentNullException(nameof(getRestorePlanHandler));
    }

    public Task<Result<AssetEditDraftRestorePlanDto>> GetRestorePlanAsync(
        GetAssetEditDraftRestorePlanQuery query,
        CancellationToken cancellationToken = default) =>
        _getRestorePlanHandler.HandleAsync(query, cancellationToken);
}
