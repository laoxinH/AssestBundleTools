using AssestBundleTools.Application.Common.Diagnostics;
using AssestBundleTools.Presentation.Tests.Testing;
using AssestBundleTools.Presentation.WinUI.Services;

namespace AssestBundleTools.Presentation.Tests.Services;

public sealed class AppLogServiceTests
{
    [Fact]
    public void AppendWarning_ShouldUpdateLogCenter_AndPersistToActiveWorkspace()
    {
        var store = new RecordingWorkspaceDiagnosticLogStore();
        var service = new AppLogService(
            new TestLocalizationService(),
            store,
            () => @"D:\WorkspaceData");

        service.AppendWarning("drag diagnostic");

        Assert.Equal("drag diagnostic", service.LogPanel.Entries[0].Message);
        Assert.Equal(WorkspaceDiagnosticLogLevel.Warning, service.LogPanel.Entries[0].Level);
        var write = Assert.Single(store.Writes);
        Assert.Equal(@"D:\WorkspaceData", write.WorkspaceDataRootPath);
        Assert.Equal(WorkspaceDiagnosticLogLevel.Warning, write.Entry.Level);
        Assert.Equal("drag diagnostic", write.Entry.Message);
    }

    [Fact]
    public void AppendInfo_ShouldRemainInMemory_WhenNoWorkspaceIsActive()
    {
        var store = new RecordingWorkspaceDiagnosticLogStore();
        var service = new AppLogService(
            new TestLocalizationService(),
            store,
            () => null);

        service.AppendInfo("startup diagnostic");

        Assert.Equal("startup diagnostic", service.LogPanel.Entries[0].Message);
        Assert.Equal(WorkspaceDiagnosticLogLevel.Information, service.LogPanel.Entries[0].Level);
        Assert.Empty(store.Writes);
    }

    private sealed class RecordingWorkspaceDiagnosticLogStore : IWorkspaceDiagnosticLogStore
    {
        public List<WorkspaceDiagnosticLogWrite> Writes { get; } = [];

        public void Append(string workspaceDataRootPath, WorkspaceDiagnosticLogEntryDto entry) =>
            Writes.Add(new WorkspaceDiagnosticLogWrite(workspaceDataRootPath, entry));
    }

    private sealed record WorkspaceDiagnosticLogWrite(
        string WorkspaceDataRootPath,
        WorkspaceDiagnosticLogEntryDto Entry);
}
