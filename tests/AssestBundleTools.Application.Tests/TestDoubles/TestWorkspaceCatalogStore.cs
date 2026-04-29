using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;

namespace AssestBundleTools.Application.Tests.TestDoubles;

internal sealed class TestWorkspaceCatalogStore : IWorkspaceCatalogStore
{
    public TestWorkspaceCatalogStore(WorkspaceCatalogSnapshotDto snapshot)
    {
        Snapshot = snapshot;
    }

    public WorkspaceCatalogSnapshotDto Snapshot { get; private set; }

    public int SaveCallCount { get; private set; }

    public WorkspaceCatalogSnapshotDto Load() => Snapshot;

    public void Save(WorkspaceCatalogSnapshotDto snapshot)
    {
        Snapshot = snapshot;
        SaveCallCount++;
    }
}
