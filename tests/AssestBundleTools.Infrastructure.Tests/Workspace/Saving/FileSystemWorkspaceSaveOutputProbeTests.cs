using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Infrastructure.Tests.Testing;
using AssestBundleTools.Infrastructure.Workspace.Saving;

namespace AssestBundleTools.Infrastructure.Tests.Workspace.Saving;

internal static class FileSystemWorkspaceSaveOutputProbeTests
{
    public static void Probe_ShouldReportReady_WhenTargetDoesNotExist()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var probe = new FileSystemWorkspaceSaveOutputProbe();

            var result = probe.Probe(new(Path.Combine(root, "Outputs", "bundle.ab")));

            TestAssert.Equal(WorkspaceSaveOutputReadiness.Ready, result.Readiness, "New output path should be ready.");
            TestAssert.False(result.IsWriteBlocked, "New output path should not be blocked.");
            TestAssert.Equal(null, result.DiagnosticCode, "Ready output should not carry a diagnostic code.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    public static void Probe_ShouldReportExistingFile_AsWriteBlocker()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var outputPath = Path.Combine(root, "Outputs", "bundle.ab");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, "existing");
            var probe = new FileSystemWorkspaceSaveOutputProbe();

            var result = probe.Probe(new(outputPath));

            TestAssert.Equal(WorkspaceSaveOutputReadiness.FileAlreadyExists, result.Readiness, "Existing output file should be explicit.");
            TestAssert.True(result.IsWriteBlocked, "Existing output file should block real writing.");
            TestAssert.Equal("Save.Output.FileAlreadyExists", result.DiagnosticCode, "Existing output file should carry the stable diagnostic code.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    public static void Probe_ShouldReportDirectorySegmentBlockedByFile()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var blockerPath = Path.Combine(root, "Outputs");
            File.WriteAllText(blockerPath, "not a directory");
            var probe = new FileSystemWorkspaceSaveOutputProbe();

            var result = probe.Probe(new(Path.Combine(blockerPath, "bundle.ab")));

            TestAssert.Equal(WorkspaceSaveOutputReadiness.DirectoryBlockedByFile, result.Readiness, "File in directory chain should be explicit.");
            TestAssert.True(result.IsWriteBlocked, "File in directory chain should block real writing.");
            TestAssert.Equal("Save.Output.DirectoryBlockedByFile", result.DiagnosticCode, "Directory blocker should carry the stable diagnostic code.");
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "AssestBundleTools.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.GetFullPath(root);
    }

    private static void DeleteTemporaryRoot(string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "AssestBundleTools.Tests"));
        if (!normalizedRoot.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) ||
            !Directory.Exists(normalizedRoot))
        {
            return;
        }

        Directory.Delete(normalizedRoot, recursive: true);
    }
}
