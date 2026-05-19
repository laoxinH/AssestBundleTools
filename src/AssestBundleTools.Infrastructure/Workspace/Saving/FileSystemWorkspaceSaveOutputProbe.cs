using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

public sealed class FileSystemWorkspaceSaveOutputProbe : IWorkspaceSaveOutputProbe
{
    private const string DiagnosticFileAlreadyExists = "Save.Output.FileAlreadyExists";
    private const string DiagnosticTargetIsDirectory = "Save.Output.TargetIsDirectory";
    private const string DiagnosticDirectoryBlockedByFile = "Save.Output.DirectoryBlockedByFile";
    private const string DiagnosticInvalidPath = "Save.Output.InvalidPath";
    private const string DiagnosticProbeFailed = "Save.Output.ProbeFailed";

    public WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            return WorkspaceSaveOutputProbeResult.Blocked(
                WorkspaceSaveOutputReadiness.InvalidPath,
                DiagnosticInvalidPath);
        }

        try
        {
            var fullOutputPath = Path.GetFullPath(request.OutputFilePath.Trim());
            if (Directory.Exists(fullOutputPath))
            {
                return WorkspaceSaveOutputProbeResult.Blocked(
                    WorkspaceSaveOutputReadiness.TargetIsDirectory,
                    DiagnosticTargetIsDirectory);
            }

            if (File.Exists(fullOutputPath))
            {
                return WorkspaceSaveOutputProbeResult.Blocked(
                    WorkspaceSaveOutputReadiness.FileAlreadyExists,
                    DiagnosticFileAlreadyExists);
            }

            if (IsAnyMissingDirectorySegmentBlockedByFile(Path.GetDirectoryName(fullOutputPath)))
            {
                return WorkspaceSaveOutputProbeResult.Blocked(
                    WorkspaceSaveOutputReadiness.DirectoryBlockedByFile,
                    DiagnosticDirectoryBlockedByFile);
            }

            return WorkspaceSaveOutputProbeResult.Ready();
        }
        catch (ArgumentException)
        {
            return WorkspaceSaveOutputProbeResult.Blocked(
                WorkspaceSaveOutputReadiness.InvalidPath,
                DiagnosticInvalidPath);
        }
        catch (NotSupportedException)
        {
            return WorkspaceSaveOutputProbeResult.Blocked(
                WorkspaceSaveOutputReadiness.InvalidPath,
                DiagnosticInvalidPath);
        }
        catch (PathTooLongException)
        {
            return WorkspaceSaveOutputProbeResult.Blocked(
                WorkspaceSaveOutputReadiness.InvalidPath,
                DiagnosticInvalidPath);
        }
        catch (UnauthorizedAccessException)
        {
            return WorkspaceSaveOutputProbeResult.Blocked(
                WorkspaceSaveOutputReadiness.ProbeFailed,
                DiagnosticProbeFailed);
        }
        catch (IOException)
        {
            return WorkspaceSaveOutputProbeResult.Blocked(
                WorkspaceSaveOutputReadiness.ProbeFailed,
                DiagnosticProbeFailed);
        }
    }

    private static bool IsAnyMissingDirectorySegmentBlockedByFile(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        var currentPath = directoryPath;
        while (!string.IsNullOrWhiteSpace(currentPath) && !Directory.Exists(currentPath))
        {
            if (File.Exists(currentPath))
            {
                return true;
            }

            var parentPath = Path.GetDirectoryName(currentPath);
            if (string.Equals(parentPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            currentPath = parentPath;
        }

        return false;
    }
}
