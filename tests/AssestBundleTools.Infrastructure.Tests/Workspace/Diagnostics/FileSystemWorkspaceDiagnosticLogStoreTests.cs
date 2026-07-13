using AssestBundleTools.Application.Common.Diagnostics;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Diagnostics;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Diagnostics;

internal static class FileSystemWorkspaceDiagnosticLogStoreTests
{
    public static void Append_ShouldWriteTimestampedSingleLineEntries_UnderWorkspaceRoot()
    {
        using var scope = new TemporaryDirectoryScope();
        var store = new FileSystemWorkspaceDiagnosticLogStore();

        store.Append(
            scope.DirectoryPath,
            new WorkspaceDiagnosticLogEntryDto(
                new DateTimeOffset(2026, 7, 13, 10, 20, 30, 456, TimeSpan.FromHours(8)),
                WorkspaceDiagnosticLogLevel.Warning,
                "DragEnter\npayload"));
        store.Append(
            scope.DirectoryPath,
            new WorkspaceDiagnosticLogEntryDto(
                new DateTimeOffset(2026, 7, 13, 10, 20, 31, TimeSpan.FromHours(8)),
                WorkspaceDiagnosticLogLevel.Information,
                "Drop accepted"));

        var logFilePath = Path.Combine(
            scope.DirectoryPath,
            FileSystemWorkspaceDiagnosticLogStore.RelativeLogFilePath);
        TestAssert.True(File.Exists(logFilePath), "Workspace diagnostic log should be created on first append.");
        var lines = File.ReadAllLines(logFilePath);
        TestAssert.Equal(2, lines.Length, "Each diagnostic entry should occupy exactly one line.");
        TestAssert.True(
            lines[0].Contains("[Warning] DragEnter\\npayload", StringComparison.Ordinal),
            "Diagnostic messages should preserve embedded newlines as escaped text.");
        TestAssert.True(
            lines[1].Contains("[Information] Drop accepted", StringComparison.Ordinal),
            "Subsequent diagnostic entries should append without replacing earlier entries.");
    }
}
