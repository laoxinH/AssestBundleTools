using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class WorkspaceProcessCoordinatorServiceTests
{
    [Fact]
    public void TryBeginPendingWorkspaceRegistration_ShouldCreateExternalWorkspaceReservation()
    {
        using var coordinator = new WorkspaceProcessCoordinatorService(new TestLocalizationService());
        var navigationRequest = PresentationTestDataFactory.CreateNavigationRequest("PendingWorkspace");
        var mutexName = CreateMutexName(navigationRequest);

        Assert.True(coordinator.TryBeginPendingWorkspaceRegistration(navigationRequest));
        Assert.True(TryOpenWorkspaceMutex(mutexName, out var mutex));

        mutex.Dispose();
    }

    [Fact]
    public void RegisterWorkspace_ShouldPromotePendingRegistration_AndKeepWorkspaceReservationActive()
    {
        using var coordinator = new WorkspaceProcessCoordinatorService(new TestLocalizationService());
        var navigationRequest = PresentationTestDataFactory.CreateNavigationRequest("PromotedWorkspace");
        var mutexName = CreateMutexName(navigationRequest);

        Assert.True(coordinator.TryBeginPendingWorkspaceRegistration(navigationRequest));

        coordinator.RegisterWorkspace(navigationRequest);
        coordinator.ClearPendingWorkspaceRegistration();

        Assert.True(TryOpenWorkspaceMutex(mutexName, out var mutex));
        Assert.False(coordinator.TryActivateExistingWorkspace(navigationRequest));

        mutex.Dispose();
    }

    [Fact]
    public void ClearPendingWorkspaceRegistration_ShouldStopActivationRedirection()
    {
        using var coordinator = new WorkspaceProcessCoordinatorService(new TestLocalizationService());
        var navigationRequest = PresentationTestDataFactory.CreateNavigationRequest("ClearedPendingWorkspace");
        var mutexName = CreateMutexName(navigationRequest);

        Assert.True(coordinator.TryBeginPendingWorkspaceRegistration(navigationRequest));

        coordinator.ClearPendingWorkspaceRegistration();

        Assert.False(TryOpenWorkspaceMutex(mutexName, out var mutex));
        Assert.False(coordinator.TryActivateExistingWorkspace(navigationRequest));
    }

    private static string CreateMutexName(WorkspaceNavigationRequest navigationRequest)
    {
        var workspaceKey = AssestBundleTools.Application.Workspace.Services.WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            navigationRequest.SourcePath,
            navigationRequest.WorkspaceDataRootPath);
        return $@"Local\AssestBundleTools.Workspace.{workspaceKey}";
    }

    private static bool TryOpenWorkspaceMutex(string mutexName, out Mutex mutex)
    {
        if (Mutex.TryOpenExisting(mutexName, out var existingMutex))
        {
            mutex = existingMutex;
            return true;
        }

        mutex = null!;
        return false;
    }
}
