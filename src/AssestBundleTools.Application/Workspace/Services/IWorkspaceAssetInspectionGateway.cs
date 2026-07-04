using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Services;

/// <summary>
/// 聚合资产详情、预览与文档页签装载相关能力。
/// </summary>
public interface IWorkspaceAssetInspectionGateway
{
    Task<Result<AssetDetailsDto>> GetAssetDetailsAsync(
        GetAssetDetailsQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetPreviewDto>> GetAssetPreviewAsync(
        GetAssetPreviewQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetPreviewTextChunkDto>> LoadPreviewTextChunkAsync(
        LoadPreviewTextChunkQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<SpineAssetGroupDetailsDto>> LoadSpineAssetGroupDetailsAsync(
        LoadSpineAssetGroupDetailsQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<AssetPaneContextDto>> OpenAssetInPaneAsync(
        OpenAssetInPaneCommand command,
        CancellationToken cancellationToken = default);
}
