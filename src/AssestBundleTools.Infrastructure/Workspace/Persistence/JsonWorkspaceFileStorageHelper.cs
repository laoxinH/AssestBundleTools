using System.Text.Json;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 封装工作区相关文件的基础读写细节。
/// </summary>
/// <remarks>
/// 这样每个 Store 只关注“文件放在哪里、数据结构是什么”，
/// 而不会重复实现原子写入、目录创建和异常兜底逻辑。
/// </remarks>
internal static class JsonWorkspaceFileStorageHelper
{
    public static T? TryReadJson<T>(string filePath, JsonSerializerOptions serializerOptions)
        where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T>(stream, serializerOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static byte[]? TryReadBytes(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(filePath);
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void WriteJsonAtomically<T>(string filePath, T value, JsonSerializerOptions serializerOptions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var temporaryFilePath = CreateTemporaryFilePath(filePath);
        var jsonText = JsonSerializer.Serialize(value, serializerOptions);
        File.WriteAllText(temporaryFilePath, jsonText);
        File.Move(temporaryFilePath, filePath, overwrite: true);
    }

    public static void WriteBytesAtomically(string filePath, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var temporaryFilePath = CreateTemporaryFilePath(filePath);
        File.WriteAllBytes(temporaryFilePath, content);
        File.Move(temporaryFilePath, filePath, overwrite: true);
    }

    public static void DeleteFileIfExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        File.Delete(filePath);
    }

    public static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        Directory.Delete(directoryPath, recursive: true);
    }

    private static string CreateTemporaryFilePath(string filePath) =>
        $"{filePath}.{Guid.NewGuid():N}.tmp";
}
