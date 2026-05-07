using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Queries;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 聚合编辑草稿恢复候选查询能力。
/// </summary>
public interface IAssetEditDraftRestorePlanGateway
{
    Task<Result<AssetEditDraftRestorePlanDto>> GetRestorePlanAsync(
        GetAssetEditDraftRestorePlanQuery query,
        CancellationToken cancellationToken = default);
}
