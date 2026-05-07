using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Editing.Services;

/// <summary>
/// 从工作区变更暂存层读取编辑草稿描述元数据。
/// </summary>
public sealed class WorkspaceChangeSetAssetEditDraftStore : IAssetEditDraftStore
{
    private readonly IWorkspaceCatalogStore _workspaceCatalogStore;
    private readonly IWorkspaceDataStoreFactory _workspaceDataStoreFactory;

    public WorkspaceChangeSetAssetEditDraftStore(
        IWorkspaceCatalogStore workspaceCatalogStore,
        IWorkspaceDataStoreFactory workspaceDataStoreFactory)
    {
        _workspaceCatalogStore = workspaceCatalogStore ?? throw new ArgumentNullException(nameof(workspaceCatalogStore));
        _workspaceDataStoreFactory = workspaceDataStoreFactory ?? throw new ArgumentNullException(nameof(workspaceDataStoreFactory));
    }

    public AssetEditDraftDescriptorDto? Load(AssetEditDraftLookupKey lookupKey)
    {
        ArgumentNullException.ThrowIfNull(lookupKey);

        var workspaceKey = NormalizeRequired(lookupKey.WorkspaceKey);
        var draftId = NormalizeRequired(lookupKey.DraftId);
        var draftMode = NormalizeRequired(lookupKey.DraftMode);
        if (string.IsNullOrEmpty(workspaceKey) ||
            string.IsNullOrEmpty(draftId) ||
            string.IsNullOrEmpty(draftMode))
        {
            return null;
        }

        var changeSet = LoadWorkspaceChangeSet(workspaceKey);
        if (changeSet is null ||
            !string.Equals(NormalizeRequired(changeSet.WorkspaceKey), workspaceKey, StringComparison.Ordinal))
        {
            return null;
        }

        var draftDescriptor = (changeSet.EditDrafts ?? Array.Empty<WorkspaceAssetEditDraftDescriptorDto>())
            .FirstOrDefault(descriptor =>
                string.Equals(NormalizeRequired(descriptor.DraftId), draftId, StringComparison.Ordinal) &&
                string.Equals(NormalizeRequired(descriptor.DraftMode), draftMode, StringComparison.Ordinal));
        if (draftDescriptor is null)
        {
            return null;
        }

        return new AssetEditDraftDescriptorDto(
            WorkspaceKey: workspaceKey,
            DraftId: NormalizeRequired(draftDescriptor.DraftId),
            DraftMode: NormalizeRequired(draftDescriptor.DraftMode),
            SourceFingerprint: NormalizeOptional(draftDescriptor.SourceFingerprint),
            UpdatedAtUtc: draftDescriptor.UpdatedAtUtc,
            ExpiresAtUtc: draftDescriptor.ExpiresAtUtc,
            SourceDescription: NormalizeOptional(draftDescriptor.SourceDescription),
            Summary: NormalizeOptional(draftDescriptor.Summary));
    }

    private WorkspaceChangeSetDto? LoadWorkspaceChangeSet(string workspaceKey)
    {
        var workspaceDataRootPath = ResolveWorkspaceDataRootPath(workspaceKey);
        var changeStore = _workspaceDataStoreFactory.CreateWorkspaceChangeStore(workspaceDataRootPath);
        return changeStore.Load(workspaceKey);
    }

    private string? ResolveWorkspaceDataRootPath(string workspaceKey)
    {
        var catalogSnapshot = _workspaceCatalogStore.Load();
        var workspaceEntry = catalogSnapshot.RecentWorkspaces.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal));
        return NormalizeOptional(workspaceEntry?.WorkspaceDataRootPath);
    }

    private static string NormalizeRequired(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
}
