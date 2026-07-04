using System.Collections.Concurrent;
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
    private static readonly ConcurrentDictionary<string, object> FileWriteLocks = new(StringComparer.OrdinalIgnoreCase);

    public static T? TryReadJson<T>(string filePath, JsonSerializerOptions serializerOptions)
        where T : class
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            lock (GetFileWriteLock(filePath))
            {
                using var stream = OpenSharedReadStream(filePath);
                return JsonSerializer.Deserialize<T>(stream, serializerOptions);
            }
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
            lock (GetFileWriteLock(filePath))
            {
                using var stream = OpenSharedReadStream(filePath);
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
        catch (IOException)
        {
            return null;
        }
    }

    public static void WriteJsonAtomically<T>(string filePath, T value, JsonSerializerOptions serializerOptions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        lock (GetFileWriteLock(filePath))
        {
            var temporaryFilePath = CreateTemporaryFilePath(filePath);
            try
            {
                var jsonText = JsonSerializer.Serialize(value, serializerOptions);
                File.WriteAllText(temporaryFilePath, jsonText);
                PublishTemporaryFile(temporaryFilePath, filePath);
            }
            finally
            {
                DeleteTemporaryFileIfExists(temporaryFilePath);
            }
        }
    }

    public static void WriteBytesAtomically(string filePath, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        lock (GetFileWriteLock(filePath))
        {
            var temporaryFilePath = CreateTemporaryFilePath(filePath);
            try
            {
                File.WriteAllBytes(temporaryFilePath, content);
                PublishTemporaryFile(temporaryFilePath, filePath);
            }
            finally
            {
                DeleteTemporaryFileIfExists(temporaryFilePath);
            }
        }
    }

    public static void DeleteFileIfExists(string filePath)
    {
        lock (GetFileWriteLock(filePath))
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            File.Delete(filePath);
        }
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

    public static FileStream OpenSharedReadStream(string filePath) =>
        new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

    private static object GetFileWriteLock(string filePath) =>
        FileWriteLocks.GetOrAdd(Path.GetFullPath(filePath), static _ => new object());

    private static void PublishTemporaryFile(string temporaryFilePath, string filePath)
    {
        try
        {
            File.Move(temporaryFilePath, filePath, overwrite: true);
            return;
        }
        catch (UnauthorizedAccessException) when (File.Exists(filePath))
        {
        }
        catch (IOException) when (File.Exists(filePath))
        {
        }

        using var sourceStream = new FileStream(
            temporaryFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        using var targetStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        sourceStream.CopyTo(targetStream);
    }

    private static void DeleteTemporaryFileIfExists(string temporaryFilePath)
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
