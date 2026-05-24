using System.Globalization;
using AssestBundleTools.Application.Common.Abstractions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;

namespace AssestBundleTools.Application.Editing.Commands;

/// <summary>
/// 将 Sprite 编辑工作面的当前几何应用为工作区可恢复操作记录。
/// </summary>
public sealed class ApplySpriteEditHandler : ICommandHandler<ApplySpriteEditCommand, Result<AssetSpriteEditAppliedResultDto>>
{
    private readonly IWorkspaceSessionRegistry _workspaceSessionRegistry;
    private readonly IWorkspaceSpriteEditSessionRegistry _spriteEditSessionRegistry;
    private readonly WorkspaceSpriteEditOperationRecorder? _operationRecorder;

    public ApplySpriteEditHandler(
        IWorkspaceSessionRegistry workspaceSessionRegistry,
        IWorkspaceSpriteEditSessionRegistry spriteEditSessionRegistry,
        WorkspaceSpriteEditOperationRecorder? operationRecorder = null)
    {
        _workspaceSessionRegistry = workspaceSessionRegistry ?? throw new ArgumentNullException(nameof(workspaceSessionRegistry));
        _spriteEditSessionRegistry = spriteEditSessionRegistry ?? throw new ArgumentNullException(nameof(spriteEditSessionRegistry));
        _operationRecorder = operationRecorder;
    }

    public Task<Result<AssetSpriteEditAppliedResultDto>> HandleAsync(
        ApplySpriteEditCommand command,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Cancelled());
        }

        if (string.IsNullOrWhiteSpace(command.WorkspaceId.Value))
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(ApplicationErrors.WorkspaceIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.BundleId.Value))
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(ApplicationErrors.BundleIdRequired()));
        }

        if (string.IsNullOrWhiteSpace(command.AssetId.Value))
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(ApplicationErrors.AssetIdRequired()));
        }

        if (!_workspaceSessionRegistry.ContainsWorkspace(command.WorkspaceId))
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(ApplicationErrors.WorkspaceNotFound(command.WorkspaceId)));
        }

        if (!_workspaceSessionRegistry.TryGetAssetDetails(command.WorkspaceId, command.BundleId, command.AssetId, out var targetDetails) ||
            targetDetails is null)
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        if (targetDetails.AssetType != AssetType.Sprite)
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(
                ApplicationErrors.AssetTypeMismatch(AssetType.Sprite, targetDetails.AssetType)));
        }

        var geometry = NormalizeGeometry(command.Geometry);
        if (!IsValidGeometry(geometry))
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(
                ApplicationErrors.AssetEditDraftInvalidGeometry(command.AssetId)));
        }

        var draftId = NormalizeOptional(command.DraftId) ?? CreateDraftId();
        var draftMode = NormalizeOptional(command.DraftMode) ?? WorkspaceSpriteEditOperationRecorder.SpriteGeometryDraftMode;
        if (!_spriteEditSessionRegistry.TryApplySpriteEdit(
                command.WorkspaceId,
                command.BundleId,
                command.AssetId,
                draftId,
                draftMode,
                geometry,
                out var editResult) ||
            editResult is null)
        {
            return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Failure(ApplicationErrors.AssetNotFound(command.AssetId)));
        }

        _operationRecorder?.RecordSpriteEdit(command, targetDetails, editResult);

        return Task.FromResult(Result<AssetSpriteEditAppliedResultDto>.Success(editResult));
    }

    private static WorkspaceSpriteEditGeometryDto NormalizeGeometry(
        WorkspaceSpriteEditGeometryDto geometry) =>
        new(
            NormalizeFinite(geometry.CanvasWidth),
            NormalizeFinite(geometry.CanvasHeight),
            NormalizeFinite(geometry.RegionX),
            NormalizeFinite(geometry.RegionY),
            NormalizeFinite(geometry.RegionWidth),
            NormalizeFinite(geometry.RegionHeight),
            NormalizeFinite(geometry.PivotX),
            NormalizeFinite(geometry.PivotY));

    private static bool IsValidGeometry(WorkspaceSpriteEditGeometryDto geometry) =>
        geometry.CanvasWidth > 0d &&
        geometry.CanvasHeight > 0d &&
        geometry.RegionX >= 0d &&
        geometry.RegionY >= 0d &&
        geometry.RegionWidth > 0d &&
        geometry.RegionHeight > 0d &&
        geometry.PivotX >= geometry.RegionX &&
        geometry.PivotX <= geometry.RegionX + geometry.RegionWidth &&
        geometry.PivotY >= geometry.RegionY &&
        geometry.PivotY <= geometry.RegionY + geometry.RegionHeight;

    private static string CreateDraftId()
    {
        var ticks = DateTimeOffset.UtcNow.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        return $"draft_sprite_{ticks}_{suffix}";
    }

    private static double NormalizeFinite(double value) =>
        double.IsFinite(value) ? value : 0d;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
