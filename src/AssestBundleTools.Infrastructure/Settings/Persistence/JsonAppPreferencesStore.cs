using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssestBundleTools.Infrastructure.Settings.Persistence;

/// <summary>
/// 基于 JSON 文件的应用偏好设置持久化实现。
/// </summary>
public sealed class JsonAppPreferencesStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly JsonAppPreferencesStoragePaths _storagePaths;

    public JsonAppPreferencesStore(string? rootDirectoryPath = null)
    {
        _storagePaths = string.IsNullOrWhiteSpace(rootDirectoryPath)
            ? JsonAppPreferencesStoragePaths.CreateDefault()
            : new JsonAppPreferencesStoragePaths(rootDirectoryPath);
    }

    public AppPreferencesSnapshot Load()
    {
        if (!File.Exists(_storagePaths.PreferencesFilePath))
        {
            return AppPreferencesSnapshot.Default;
        }

        try
        {
            using var stream = File.OpenRead(_storagePaths.PreferencesFilePath);
            var snapshot = JsonSerializer.Deserialize<AppPreferencesSnapshot>(stream, SerializerOptions);
            return snapshot ?? AppPreferencesSnapshot.Default;
        }
        catch (IOException)
        {
            return AppPreferencesSnapshot.Default;
        }
        catch (JsonException)
        {
            return AppPreferencesSnapshot.Default;
        }
    }

    public void Save(AppPreferencesSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Directory.CreateDirectory(_storagePaths.RootDirectoryPath);

        var temporaryFilePath = $"{_storagePaths.PreferencesFilePath}.tmp";
        var jsonText = JsonSerializer.Serialize(snapshot, SerializerOptions);
        File.WriteAllText(temporaryFilePath, jsonText);
        File.Move(temporaryFilePath, _storagePaths.PreferencesFilePath, overwrite: true);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }
}
