using System.Text.Json;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Bundles.Processing;

/// <summary>
/// 基于文件系统 manifest 的导出资源处理器目录管理实现。
/// </summary>
public sealed class FileSystemResourceProcessorCatalogStore : IResourceProcessorCatalogStore
{
    private static readonly IReadOnlyList<string> DefaultProcessArguments =
    [
        "--input",
        "{inputPath}",
        "--output",
        "{outputPath}",
        "--result",
        "{resultPath}",
        "--resource-kind",
        "{resourceKind}",
        "--workspace",
        "{workspaceDataRootPath}"
    ];

    private readonly string _userManagedManifestDirectoryPath;
    private readonly IReadOnlyList<string> _manifestDirectoryPaths;

    public FileSystemResourceProcessorCatalogStore(IEnumerable<string>? manifestDirectories = null)
    {
        _manifestDirectoryPaths = ResolveManifestDirectories(manifestDirectories).ToArray();
        _userManagedManifestDirectoryPath = _manifestDirectoryPaths.FirstOrDefault()
                                            ?? ResourceProcessorDiscoveryPaths.GetUserManagedManifestDirectory();
    }

    public IReadOnlyList<ResourceProcessorCatalogEntryDto> Load()
    {
        var entries = new List<ResourceProcessorCatalogEntryDto>
        {
            CreateBuiltInSlot("resource.processor.texture2d.cli", "Texture2D"),
            CreateBuiltInSlot("resource.processor.audio.cli", "Audio"),
            CreateBuiltInSlot("resource.processor.atlas.cli", "Atlas"),
            CreateBuiltInSlot("resource.processor.skel.cli", "Skel")
        };

        foreach (var manifestDirectoryPath in _manifestDirectoryPaths)
        {
            if (!Directory.Exists(manifestDirectoryPath))
            {
                continue;
            }

            foreach (var manifestFilePath in Directory.EnumerateFiles(manifestDirectoryPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                var manifest = ReadManifest(manifestFilePath);
                if (manifest is null || !IsValid(manifest))
                {
                    continue;
                }

                entries.Add(MapToCatalogEntry(
                    manifest,
                    isReadOnly: !IsUserManagedManifestPath(manifestFilePath),
                    manifestFilePath: Path.GetFullPath(manifestFilePath)));
            }
        }

        return entries
            .GroupBy(static entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderByDescending(static entry => entry.IsBuiltIn)
            .ThenBy(static entry => entry.ResourceKinds.FirstOrDefault() ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void SaveUserManagedProcessor(
        ResourceProcessorDefinitionDto definition,
        string? previousProcessorId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Directory.CreateDirectory(_userManagedManifestDirectoryPath);

        var existingEntries = Load();
        var previousEntry = string.IsNullOrWhiteSpace(previousProcessorId)
            ? null
            : existingEntries.FirstOrDefault(entry =>
                string.Equals(entry.Id, previousProcessorId.Trim(), StringComparison.OrdinalIgnoreCase));

        var targetFilePath = previousEntry is not null &&
                             !string.IsNullOrWhiteSpace(previousEntry.ManifestFilePath) &&
                             IsUserManagedManifestPath(previousEntry.ManifestFilePath)
            ? previousEntry.ManifestFilePath!
            : BuildUserManagedManifestFilePath(definition.Id);

        if (!string.IsNullOrWhiteSpace(previousProcessorId) &&
            !string.Equals(previousProcessorId.Trim(), definition.Id.Trim(), StringComparison.OrdinalIgnoreCase) &&
            previousEntry is not null &&
            !string.IsNullOrWhiteSpace(previousEntry.ManifestFilePath) &&
            File.Exists(previousEntry.ManifestFilePath))
        {
            JsonWorkspaceFileStorageHelper.DeleteFileIfExists(previousEntry.ManifestFilePath);
            targetFilePath = BuildUserManagedManifestFilePath(definition.Id);
        }

        var manifest = new ResourceProcessorManifest
        {
            Id = definition.Id.Trim(),
            DisplayName = definition.DisplayName.Trim(),
            Version = string.IsNullOrWhiteSpace(definition.Version) ? "1.0.0" : definition.Version.Trim(),
            Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim(),
            Kind = "resourceProcessor",
            ResourceKinds = NormalizeDistinctValues(definition.ResourceKinds),
            ExecutablePath = definition.ExecutablePath.Trim(),
            WorkingDirectoryPath = string.IsNullOrWhiteSpace(definition.WorkingDirectoryPath)
                ? null
                : definition.WorkingDirectoryPath.Trim(),
            TimeoutMs = definition.TimeoutMs,
            ProcessArguments = NormalizeOrderedValues(definition.ProcessArguments)
        };

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            targetFilePath,
            manifest,
            ResourceProcessorJsonSerialization.SerializerOptions);
    }

    public void DeleteUserManagedProcessor(string processorId)
    {
        if (string.IsNullOrWhiteSpace(processorId))
        {
            return;
        }

        var existingEntry = Load().FirstOrDefault(entry =>
            string.Equals(entry.Id, processorId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existingEntry is null ||
            string.IsNullOrWhiteSpace(existingEntry.ManifestFilePath) ||
            !IsUserManagedManifestPath(existingEntry.ManifestFilePath))
        {
            return;
        }

        JsonWorkspaceFileStorageHelper.DeleteFileIfExists(existingEntry.ManifestFilePath);
    }

    private static IReadOnlyList<string> ResolveManifestDirectories(IEnumerable<string>? manifestDirectories)
    {
        return (manifestDirectories ?? ResourceProcessorDiscoveryPaths.GetDefaultManifestDirectories())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ResourceProcessorCatalogEntryDto CreateBuiltInSlot(
        string id,
        string resourceKind)
    {
        return new ResourceProcessorCatalogEntryDto(
            id,
            id,
            Version: "1.0.0",
            Description: string.Empty,
            ResourceProcessorKind.ExternalProcess,
            [resourceKind],
            ResourceProcessorStatus.Available,
            IsBuiltIn: true,
            IsReadOnly: true,
            ManifestFilePath: null,
            ExecutablePath: null,
            WorkingDirectoryPath: null,
            TimeoutMs: 15000,
            ProcessArguments: DefaultProcessArguments);
    }

    private static ResourceProcessorManifest? ReadManifest(string manifestFilePath)
    {
        try
        { 
            using var stream = File.OpenRead(manifestFilePath);
            return JsonSerializer.Deserialize<ResourceProcessorManifest>(
                stream,
                ResourceProcessorJsonSerialization.SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ResourceProcessorCatalogEntryDto MapToCatalogEntry(
        ResourceProcessorManifest manifest,
        bool isReadOnly,
        string manifestFilePath)
    {
        return new ResourceProcessorCatalogEntryDto(
            manifest.Id.Trim(),
            manifest.DisplayName.Trim(),
            string.IsNullOrWhiteSpace(manifest.Version) ? "1.0.0" : manifest.Version.Trim(),
            manifest.Description ?? string.Empty,
            ResourceProcessorKind.ExternalProcess,
            NormalizeDistinctValues(manifest.ResourceKinds),
            ResolveStatus(manifest),
            IsBuiltIn: false,
            IsReadOnly: isReadOnly,
            ManifestFilePath: manifestFilePath,
            ExecutablePath: manifest.ExecutablePath.Trim(),
            WorkingDirectoryPath: string.IsNullOrWhiteSpace(manifest.WorkingDirectoryPath)
                ? null
                : manifest.WorkingDirectoryPath.Trim(),
            TimeoutMs: manifest.TimeoutMs,
            ProcessArguments: NormalizeOrderedValues(manifest.ProcessArguments));
    }

    private static ResourceProcessorStatus ResolveStatus(ResourceProcessorManifest manifest)
    {
        return string.Equals(manifest.Kind, "resourceProcessor", StringComparison.OrdinalIgnoreCase)
            ? ResourceProcessorStatus.Available
            : ResourceProcessorStatus.Warning;
    }

    private static bool IsValid(ResourceProcessorManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(manifest.Id) &&
               !string.IsNullOrWhiteSpace(manifest.DisplayName) &&
               !string.IsNullOrWhiteSpace(manifest.Version) &&
               manifest.ResourceKinds.Count > 0 &&
               manifest.ResourceKinds.Any(static resourceKind => !string.IsNullOrWhiteSpace(resourceKind)) &&
               !string.IsNullOrWhiteSpace(manifest.ExecutablePath) &&
               manifest.TimeoutMs > 0 &&
               manifest.ProcessArguments.Count > 0;
    }

    private static IReadOnlyList<string> NormalizeOrderedValues(IReadOnlyList<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeDistinctValues(IReadOnlyList<string> values)
    {
        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool IsUserManagedManifestPath(string manifestFilePath)
    {
        var normalizedManifestFilePath = Path.GetFullPath(manifestFilePath);
        var normalizedUserManagedDirectoryPath = Path.GetFullPath(_userManagedManifestDirectoryPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedManifestFilePath.StartsWith(
            normalizedUserManagedDirectoryPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private string BuildUserManagedManifestFilePath(string processorId)
    {
        var fileName = $"{SanitizeFileComponent(processorId.Trim())}.json";
        return Path.Combine(_userManagedManifestDirectoryPath, fileName);
    }

    private static string SanitizeFileComponent(string value)
    {
        var sanitizedChars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }
}
