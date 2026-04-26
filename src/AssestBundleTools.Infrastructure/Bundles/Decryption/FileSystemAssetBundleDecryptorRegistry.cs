using System.Text.Json;
using AssestBundleTools.Application.Bundles.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

/// <summary>
/// 从文件系统清单发现外部解密器，并与内置解密器合并为统一注册表。
/// </summary>
public sealed class FileSystemAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
{
    private readonly IReadOnlyList<IAssetBundleDecryptor> _decryptors;
    private readonly Dictionary<string, IAssetBundleDecryptor> _decryptorsById;

    public FileSystemAssetBundleDecryptorRegistry(IEnumerable<string>? manifestDirectories = null)
    {
        var decryptors = new List<IAssetBundleDecryptor>
        {
            new PassThroughAssetBundleDecryptor()
        };

        foreach (var manifestDirectoryPath in ResolveManifestDirectories(manifestDirectories))
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

                decryptors.Add(new ExternalProcessAssetBundleDecryptor(
                    manifest,
                    Path.GetDirectoryName(manifestFilePath)!));
            }
        }

        _decryptors = decryptors
            .GroupBy(static decryptor => decryptor.Descriptor.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static decryptor => decryptor.Descriptor.IsFallback)
            .ThenBy(static decryptor => decryptor.Descriptor.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _decryptorsById = _decryptors.ToDictionary(
            static decryptor => decryptor.Descriptor.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IAssetBundleDecryptor> GetAll() => _decryptors;

    public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
    {
        if (string.IsNullOrWhiteSpace(decryptorId))
        {
            decryptor = null!;
            return false;
        }

        return _decryptorsById.TryGetValue(decryptorId.Trim(), out decryptor!);
    }

    private static IEnumerable<string> ResolveManifestDirectories(IEnumerable<string>? manifestDirectories)
    {
        return (manifestDirectories ?? AssetBundleDecryptorDiscoveryPaths.GetDefaultManifestDirectories())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Select(static path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase);
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

    private static bool IsValid(AssetBundleExternalProcessDecryptorManifest manifest)
    {
        return !string.IsNullOrWhiteSpace(manifest.Id) &&
               !string.IsNullOrWhiteSpace(manifest.DisplayName) &&
               !string.IsNullOrWhiteSpace(manifest.Version) &&
               !string.IsNullOrWhiteSpace(manifest.ExecutablePath) &&
               manifest.TimeoutMs > 0 &&
               manifest.DecryptArguments.Count > 0;
    }
}
