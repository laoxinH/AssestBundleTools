namespace AssestBundleTools.Application.Common.Diagnostics;

public interface IWorkspaceDiagnosticLogStore
{
    void Append(string workspaceDataRootPath, WorkspaceDiagnosticLogEntryDto entry);
}
