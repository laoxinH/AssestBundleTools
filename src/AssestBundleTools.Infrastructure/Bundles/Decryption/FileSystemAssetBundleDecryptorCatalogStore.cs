using System.Text.Json;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Infrastructure.Bundles.Plugins;
using AssestBundleTools.Infrastructure.Workspace.Persistence;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 基于文件系统 manifest 的解密器目录管理实现。
/// </summary>
/// <remarks>
/// 当前仅允许管理用户目录下的外部进程型解密器；
/// 应用目录中的解密器与内置回退解密器只读展示，不允许在界面中直接覆盖。
/// </remarks>
public sealed class FileSystemAssetBundleDecryptorCatalogStore : IAssetBundleDecryptorCatalogStore
{
    private readonly string _userManagedManifestDirectoryPath;
    private readonly IReadOnlyList<string> _manifestDirectoryPaths;

    public FileSystemAssetBundleDecryptorCatalogStore(IEnumerable<string>? manifestDirectories = null)
    {
        _manifestDirectoryPaths = ResolveManifestDirectories(manifestDirectories).ToArray();
        _userManagedManifestDirectoryPath = _manifestDirectoryPaths.FirstOrDefault()
                                            ?? AssetBundleDecryptorDiscoveryPaths.GetUserManagedManifestDirectory();
    }

    public IReadOnlyList<AssetBundleDecryptorCatalogEntryDto> Load()
    {
        var entries = new List<AssetBundleDecryptorCatalogEntryDto>
        {
            BuiltInArknightsLz4AkDecryptorFactory.CreateCatalogEntry(),
            BuiltInCrossCoreDecryptorFactory.CreateCatalogEntry(),
            CreateBuiltInPassThroughEntry()
        };

        foreach (var manifestDirectoryPath in _manifestDirectoryPaths)
        {
            if (!Directory.Exists(manifestDirectoryPath))
            {
                continue;
            }

            foreach (var manifestFilePath in Directory.EnumerateFiles(manifestDirectoryPath, "*.json", SearchOption.AllDirectories))
            {
                var manifest = ReadManifest(manifestFilePath);
                if (manifest is null || !IsValid(manifest))
                {
                    continue;
                }

                entries.Add(new AssetBundleDecryptorCatalogEntryDto(
                    Definition: MapToDefinition(manifest),
                    Kind: AssetBundleDecryptorKind.ExternalProcess,
                    IsBuiltIn: false,
                    IsReadOnly: !IsUserManagedManifestPath(manifestFilePath),
                    ManifestFilePath: Path.GetFullPath(manifestFilePath)));
            }
        }

        return entries
            .GroupBy(static entry => entry.Definition.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderByDescending(static entry => entry.IsBuiltIn)
            .ThenByDescending(static entry => entry.Definition.IsFallback)
            .ThenBy(static entry => entry.Definition.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void SaveUserManagedDecryptor(
        AssetBundleDecryptorDefinitionDto definition,
        string? previousDecryptorId = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Directory.CreateDirectory(_userManagedManifestDirectoryPath);

        var existingEntries = Load();
        var previousEntry = string.IsNullOrWhiteSpace(previousDecryptorId)
            ? null
            : existingEntries.FirstOrDefault(entry =>
                string.Equals(entry.Definition.Id, previousDecryptorId.Trim(), StringComparison.OrdinalIgnoreCase));

        var targetFilePath = previousEntry is not null &&
                             !string.IsNullOrWhiteSpace(previousEntry.ManifestFilePath) &&
                             IsUserManagedManifestPath(previousEntry.ManifestFilePath)
            ? previousEntry.ManifestFilePath!
            : BuildUserManagedManifestFilePath(definition.Id);

        if (!string.IsNullOrWhiteSpace(previousDecryptorId) &&
            !string.Equals(previousDecryptorId.Trim(), definition.Id.Trim(), StringComparison.OrdinalIgnoreCase) &&
            previousEntry is not null &&
            !string.IsNullOrWhiteSpace(previousEntry.ManifestFilePath) &&
            File.Exists(previousEntry.ManifestFilePath))
        {
            JsonWorkspaceFileStorageHelper.DeleteFileIfExists(previousEntry.ManifestFilePath);
            targetFilePath = BuildUserManagedManifestFilePath(definition.Id);
        }

        var manifest = new AssetBundleExternalProcessDecryptorManifest
        {
            SchemaVersion = 1,
            Kind = "assetBundleCodec",
            Id = definition.Id.Trim(),
            DisplayName = definition.DisplayName.Trim(),
            Version = string.IsNullOrWhiteSpace(definition.Version) ? "1.0.0" : definition.Version.Trim(),
            Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim(),
            SupportsAutoDetection = definition.SupportsAutoDetection,
            IsFallback = definition.IsFallback,
            ExecutablePath = definition.ExecutablePath.Trim(),
            WorkingDirectoryPath = string.IsNullOrWhiteSpace(definition.WorkingDirectoryPath)
                ? null
                : definition.WorkingDirectoryPath.Trim(),
            TimeoutMs = definition.TimeoutMs,
            ProbeArguments = NormalizeArguments(definition.ProbeArguments),
            DecryptArguments = NormalizeArguments(definition.DecryptArguments),
            EncryptArguments = NormalizeArguments(definition.EncryptArguments ?? Array.Empty<string>())
        };

        JsonWorkspaceFileStorageHelper.WriteJsonAtomically(
            targetFilePath,
            manifest,
            AssetBundleDecryptorJsonSerialization.SerializerOptions);
    }

    public void DeleteUserManagedDecryptor(string decryptorId)
    {
        if (string.IsNullOrWhiteSpace(decryptorId))
        {
            return;
        }

        var existingEntry = Load().FirstOrDefault(entry =>
            string.Equals(entry.Definition.Id, decryptorId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existingEntry is null ||
            string.IsNullOrWhiteSpace(existingEntry.ManifestFilePath) ||
            !IsUserManagedManifestPath(existingEntry.ManifestFilePath))
        {
            return;
        }

        PluginPackageInstallationUnit.DeleteManifestOrPackage(
            existingEntry.ManifestFilePath,
            _userManagedManifestDirectoryPath,
            existingEntry.Definition.Id);
    }

    private static IReadOnlyList<string> ResolveManifestDirectories(IEnumerable<string>? manifestDirectories)
    {
        return (manifestDirectories ?? AssetBundleDecryptorDiscoveryPaths.GetDefaultManifestDirectories())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static AssetBundleDecryptorCatalogEntryDto CreateBuiltInPassThroughEntry()
    {
        return new AssetBundleDecryptorCatalogEntryDto(
            Definition: new AssetBundleDecryptorDefinitionDto(
                Id: PassThroughAssetBundleDecryptor.DefaultDescriptor.Id,
                DisplayName: PassThroughAssetBundleDecryptor.DefaultDescriptor.DisplayName,
                Version: PassThroughAssetBundleDecryptor.DefaultDescriptor.Version,
                Description: PassThroughAssetBundleDecryptor.DefaultDescriptor.Description,
                SupportsAutoDetection: PassThroughAssetBundleDecryptor.DefaultDescriptor.SupportsAutoDetection,
                IsFallback: PassThroughAssetBundleDecryptor.DefaultDescriptor.IsFallback,
                ExecutablePath: string.Empty,
                WorkingDirectoryPath: null,
                TimeoutMs: 0,
                ProbeArguments: Array.Empty<string>(),
                DecryptArguments: Array.Empty<string>(),
                EncryptArguments: Array.Empty<string>()),
            Kind: PassThroughAssetBundleDecryptor.DefaultDescriptor.Kind,
            IsBuiltIn: true,
            IsReadOnly: true,
            ManifestFilePath: null);
    }

    private static AssetBundleExternalProcessDecryptorManifest? ReadManifest(string manifestFilePath)
    {
        try
        {
            using var stream = File.OpenRead(manifestFilePath);
            return JsonSerializer.Deserialize<AssetBundleExternalProcessDecryptorManifest>(
                stream,
                AssetBundleDecryptorJsonSerialization.SerializerOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static AssetBundleDecryptorDefinitionDto MapToDefinition(AssetBundleExternalProcessDecryptorManifest manifest)
    {
        return new AssetBundleDecryptorDefinitionDto(
            Id: manifest.Id,
            DisplayName: manifest.DisplayName,
            Version: manifest.Version,
            Description: manifest.Description,
            SupportsAutoDetection: manifest.SupportsAutoDetection,
            IsFallback: manifest.IsFallback,
            ExecutablePath: manifest.ExecutablePath,
            WorkingDirectoryPath: manifest.WorkingDirectoryPath,
            TimeoutMs: manifest.TimeoutMs,
            ProbeArguments: manifest.ProbeArguments.ToArray(),
            DecryptArguments: manifest.DecryptArguments.ToArray(),
            EncryptArguments: manifest.EncryptArguments.ToArray());
    }

    private static IReadOnlyList<string> NormalizeArguments(IReadOnlyList<string> arguments)
    {
        return arguments
            .Where(static argument => !string.IsNullOrWhiteSpace(argument))
            .Select(static argument => argument.Trim())
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

    private string BuildUserManagedManifestFilePath(string decryptorId)
    {
        var fileName = $"{SanitizeFileComponent(decryptorId.Trim())}.json";
        return Path.Combine(_userManagedManifestDirectoryPath, fileName);
    }

    private static bool IsValid(AssetBundleExternalProcessDecryptorManifest manifest)
    {
        return manifest.SchemaVersion == 1 &&
               string.Equals(manifest.Kind, "assetBundleCodec", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(manifest.Id) &&
               !string.IsNullOrWhiteSpace(manifest.DisplayName) &&
               !string.IsNullOrWhiteSpace(manifest.Version) &&
               !string.IsNullOrWhiteSpace(manifest.ExecutablePath) &&
               manifest.TimeoutMs > 0 &&
               manifest.DecryptArguments.Count > 0;
    }

    private static string SanitizeFileComponent(string value)
    {
        var sanitizedChars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }
}
