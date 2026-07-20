using System.Text.Json;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Infrastructure.Bundles.Processing;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Bundles.PostExport;

public sealed class FileSystemPostExportPluginCatalogStore : IPostExportPluginCatalogStore
{
    private readonly string _userManagedManifestDirectoryPath;
    private readonly IReadOnlyList<string> _manifestDirectoryPaths;

    public FileSystemPostExportPluginCatalogStore(IEnumerable<string>? manifestDirectories = null)
    {
        _manifestDirectoryPaths = (manifestDirectories ?? PostExportPluginDiscoveryPaths.GetDefaultManifestDirectories())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _userManagedManifestDirectoryPath = _manifestDirectoryPaths.FirstOrDefault()
                                            ?? PostExportPluginDiscoveryPaths.GetUserManagedManifestDirectory();
    }

    public IReadOnlyList<PostExportPluginCatalogEntryDto> Load()
    {
        var entries = new List<PostExportPluginCatalogEntryDto>();
        foreach (var directoryPath in _manifestDirectoryPaths)
        {
            if (!Directory.Exists(directoryPath))
            {
                continue;
            }

            foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                var manifest = ReadManifest(filePath);
                if (manifest is null || !IsValid(manifest))
                {
                    continue;
                }

                entries.Add(MapToEntry(manifest, Path.GetFullPath(filePath)));
            }
        }

        return entries
            .GroupBy(static entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void SaveUserManagedPlugin(PostExportPluginDefinitionDto definition, string? previousPluginId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Directory.CreateDirectory(_userManagedManifestDirectoryPath);
        var previous = string.IsNullOrWhiteSpace(previousPluginId)
            ? null
            : Load().FirstOrDefault(entry =>
                string.Equals(entry.Id, previousPluginId.Trim(), StringComparison.OrdinalIgnoreCase));
        var targetPath = previous is not null &&
                         !string.IsNullOrWhiteSpace(previous.ManifestFilePath) &&
                         IsUserManagedManifestPath(previous.ManifestFilePath)
            ? previous.ManifestFilePath!
            : BuildManifestFilePath(definition.Id);

        if (previous is not null &&
            !string.Equals(previous.Id, definition.Id.Trim(), StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(previous.ManifestFilePath))
        {
            JsonWorkspaceFileStorageHelper.DeleteFileIfExists(previous.ManifestFilePath);
            targetPath = BuildManifestFilePath(definition.Id);
        }

        var manifest = new PostExportPluginManifest
        {
            Id = definition.Id.Trim(),
            DisplayName = definition.DisplayName.Trim(),
            Version = string.IsNullOrWhiteSpace(definition.Version) ? "1.0.0" : definition.Version.Trim(),
            Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim(),
            Kind = "postExport",
            ExecutablePath = definition.ExecutablePath.Trim(),
            WorkingDirectoryPath = string.IsNullOrWhiteSpace(definition.WorkingDirectoryPath)
                ? null
                : definition.WorkingDirectoryPath.Trim(),
            ProcessArguments = NormalizeArguments(definition.ProcessArguments)
        };
        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            targetPath,
            manifest,
            ResourceProcessorJsonSerialization.SerializerOptions);
    }

    public void DeleteUserManagedPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        var entry = Load().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, pluginId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entry?.ManifestFilePath is not null && IsUserManagedManifestPath(entry.ManifestFilePath))
        {
            JsonWorkspaceFileStorageHelper.DeleteFileIfExists(entry.ManifestFilePath);
        }
    }

    private PostExportPluginCatalogEntryDto MapToEntry(PostExportPluginManifest manifest, string manifestPath)
    {
        var executablePath = manifest.ExecutablePath.Trim();
        var status = string.Equals(manifest.Kind, "postExport", StringComparison.OrdinalIgnoreCase) &&
                     File.Exists(ResolvePath(executablePath, manifestPath))
            ? ResourceProcessorStatus.Available
            : ResourceProcessorStatus.Unavailable;
        return new PostExportPluginCatalogEntryDto(
            manifest.Id.Trim(),
            manifest.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(manifest.Version) ? "1.0.0" : manifest.Version.Trim(),
            manifest.Description ?? string.Empty,
            status,
            IsReadOnly: !IsUserManagedManifestPath(manifestPath),
            ManifestFilePath: manifestPath,
            ExecutablePath: executablePath,
            WorkingDirectoryPath: string.IsNullOrWhiteSpace(manifest.WorkingDirectoryPath)
                ? null
                : manifest.WorkingDirectoryPath.Trim(),
            ProcessArguments: NormalizeArguments(manifest.ProcessArguments));
    }

    private static PostExportPluginManifest? ReadManifest(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<PostExportPluginManifest>(
                stream,
                ResourceProcessorJsonSerialization.SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static bool IsValid(PostExportPluginManifest manifest) =>
        !string.IsNullOrWhiteSpace(manifest.Id) &&
        !string.IsNullOrWhiteSpace(manifest.DisplayName) &&
        !string.IsNullOrWhiteSpace(manifest.ExecutablePath) &&
        manifest.ProcessArguments.Any(static argument =>
            argument.Contains("{manifestPath}", StringComparison.Ordinal));

    private static IReadOnlyList<string> NormalizeArguments(IReadOnlyList<string> arguments) =>
        arguments
            .Where(static argument => !string.IsNullOrWhiteSpace(argument))
            .Select(static argument => argument.Trim())
            .ToArray();

    private static string ResolvePath(string path, string manifestPath) =>
        Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(Path.GetDirectoryName(manifestPath) ?? Environment.CurrentDirectory, path));

    private bool IsUserManagedManifestPath(string filePath)
    {
        var root = Path.GetFullPath(_userManagedManifestDirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return Path.GetFullPath(filePath).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private string BuildManifestFilePath(string pluginId)
    {
        var fileName = new string(pluginId.Trim()
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character)
            .ToArray());
        return Path.Combine(_userManagedManifestDirectoryPath, $"{fileName}.json");
    }
}
