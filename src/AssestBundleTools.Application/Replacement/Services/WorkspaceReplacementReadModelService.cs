using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 从工作区变更暂存中读取替换事实流，并物化为当前有效替换列表。
/// </summary>
public sealed class WorkspaceReplacementReadModelService : IWorkspaceReplacementReadModelService
{
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;
    private readonly WorkspaceReplacementStateMaterializer _materializer;

    public WorkspaceReplacementReadModelService(
        IWorkspaceDataStoreFactory workspaceDataStoreFactory,
        WorkspaceReplacementStateMaterializer? materializer = null)
    {
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
        _materializer = materializer ?? new WorkspaceReplacementStateMaterializer();
    }

    public Result<WorkspaceReplacementReadModelDto> GetEffectiveReplacements(
        string workspaceKey,
        string workspaceDataRootPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return Result<WorkspaceReplacementReadModelDto>.Failure(ApplicationErrors.WorkspaceCatalogKeyRequired());
        }

        if (string.IsNullOrWhiteSpace(workspaceDataRootPath))
        {
            return Result<WorkspaceReplacementReadModelDto>.Failure(ApplicationErrors.WorkspaceOpenDataRootPathRequired());
        }

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var store = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(workspaceDataRootPath.Trim());
        var changeSet = store.Load(normalizedWorkspaceKey) ?? WorkspaceChangeSetDto.Empty(normalizedWorkspaceKey);
        var materializedState = _materializer.Materialize(changeSet);

        return Result<WorkspaceReplacementReadModelDto>.Success(
            new WorkspaceReplacementReadModelDto(
                normalizedWorkspaceKey,
                materializedState.EffectiveReplacements));
    }
}
