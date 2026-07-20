using System.Diagnostics;
using System.Text.RegularExpressions;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Saving.Dtos;
using AssestBundleTools.Application.Saving.Services;

namespace AssestBundleTools.Infrastructure.Bundles.PostExport;

public sealed class FileSystemWorkspacePostExportPluginLauncher : IWorkspacePostExportPluginLauncher
{
    private static readonly IReadOnlySet<string> SupportedPlaceholders = new HashSet<string>(StringComparer.Ordinal)
    {
        "{manifestPath}",
        "{outputDirectory}",
        "{workspaceId}",
        "{workspaceKey}",
        "{manifestDirectory}"
    };

    public Task<Result<WorkspacePostExportPluginLaunchResultDto>> LaunchAsync(
        WorkspacePostExportPluginLaunchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Result<WorkspacePostExportPluginLaunchResultDto>.Cancelled());
        }

        try
        {
            var manifestPath = Path.GetFullPath(request.ManifestPath);
            if (!File.Exists(manifestPath))
            {
                return Failure(request, "ManifestMissing");
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolvePluginPath(request.Plugin.ExecutablePath, request.Plugin.ManifestFilePath),
                    WorkingDirectory = ResolveWorkingDirectory(request.Plugin, request.OutputDirectory),
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in ExpandArguments(request, manifestPath))
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            if (!process.Start())
            {
                return Failure(request, "ProcessStartReturnedFalse");
            }

            return Task.FromResult(Result<WorkspacePostExportPluginLaunchResultDto>.Success(
                new WorkspacePostExportPluginLaunchResultDto(
                    request.Plugin.Id,
                    request.Plugin.DisplayName,
                    WorkspacePostExportPluginLaunchStatusDto.Launched)));
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return Failure(request, exception.GetType().Name);
        }
    }

    private static IEnumerable<string> ExpandArguments(
        WorkspacePostExportPluginLaunchRequestDto request,
        string manifestPath)
    {
        var outputDirectory = Path.GetFullPath(request.OutputDirectory);
        var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? outputDirectory;
        foreach (var argument in request.Plugin.ProcessArguments)
        {
            var placeholders = Regex.Matches(argument, "\\{[A-Za-z][A-Za-z0-9]*\\}")
                .Select(static match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (placeholders.Any(placeholder => !SupportedPlaceholders.Contains(placeholder)))
            {
                throw new ArgumentException("Unsupported placeholder.", nameof(request));
            }

            yield return argument
                .Replace("{manifestPath}", manifestPath, StringComparison.Ordinal)
                .Replace("{outputDirectory}", outputDirectory, StringComparison.Ordinal)
                .Replace("{workspaceId}", request.WorkspaceId.Value, StringComparison.Ordinal)
                .Replace("{workspaceKey}", request.WorkspaceKey ?? string.Empty, StringComparison.Ordinal)
                .Replace("{manifestDirectory}", manifestDirectory, StringComparison.Ordinal);
        }
    }

    private static string ResolvePluginPath(string path, string? manifestFilePath)
    {
        var trimmedPath = path.Trim();
        if (Path.IsPathRooted(trimmedPath))
        {
            return Path.GetFullPath(trimmedPath);
        }

        var manifestDirectory = string.IsNullOrWhiteSpace(manifestFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(manifestFilePath)) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(manifestDirectory, trimmedPath));
    }

    private static string ResolveWorkingDirectory(
        Application.Bundles.Dtos.PostExportPluginCatalogEntryDto plugin,
        string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(plugin.WorkingDirectoryPath))
        {
            return Path.GetFullPath(outputDirectory);
        }

        if (Path.IsPathRooted(plugin.WorkingDirectoryPath))
        {
            return Path.GetFullPath(plugin.WorkingDirectoryPath);
        }

        var manifestDirectory = string.IsNullOrWhiteSpace(plugin.ManifestFilePath)
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(plugin.ManifestFilePath)) ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(manifestDirectory, plugin.WorkingDirectoryPath));
    }

    private static Task<Result<WorkspacePostExportPluginLaunchResultDto>> Failure(
        WorkspacePostExportPluginLaunchRequestDto request,
        string detail) =>
        Task.FromResult(Result<WorkspacePostExportPluginLaunchResultDto>.Failure(
            ApplicationErrors.PostExportPluginLaunchFailed(request.Plugin.Id, detail)));
}
