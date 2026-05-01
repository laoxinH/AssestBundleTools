using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal static class ExternalProcessAssetBundleDecryptorSupport
{
    public static TPayload? ReadJsonFile<TPayload>(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<TPayload>(stream, AssetBundleDecryptorJsonSerialization.SerializerOptions);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static string ResolvePath(string path, string baseDirectoryPath)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectoryPath, path));
    }

    public static string ResolveExecutablePath(string executablePath, string baseDirectoryPath)
    {
        if (Path.IsPathRooted(executablePath))
        {
            return Path.GetFullPath(executablePath);
        }

        if (executablePath.Contains(Path.DirectorySeparatorChar) ||
            executablePath.Contains(Path.AltDirectorySeparatorChar))
        {
            return Path.GetFullPath(Path.Combine(baseDirectoryPath, executablePath));
        }

        return executablePath;
    }

    public static string SanitizeFileComponent(string value)
    {
        var sanitizedChars = value
            .Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)
            .ToArray();
        return new string(sanitizedChars);
    }

    public static string SanitizeDirectoryComponent(string value)
    {
        var sanitizedValue = SanitizeFileComponent(value);
        return string.IsNullOrWhiteSpace(sanitizedValue)
            ? "unknown"
            : sanitizedValue.Trim();
    }

    public static string ComputeStableSourcePathKey(string sourcePath)
    {
        var normalizedPath = Path.GetFullPath(sourcePath).Trim().ToUpperInvariant();
        var bytes = Encoding.UTF8.GetBytes(normalizedPath);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static bool PathsEqual(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    public static void DeleteFileIfExists(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception)
        {
            // 结果文件清理失败不应中断主流程。
        }
    }

    public static void TryTerminateProcess(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // 外部进程终止失败不应覆盖原始错误。
        }
    }
}

internal enum ExternalProcessExecutionFailureKind
{
    None = 0,
    ProcessExited = 1,
    TimedOut = 2,
    Exception = 3
}

internal readonly record struct ExternalProcessExecutionResult(
    bool IsSuccess,
    string? Detail,
    ExternalProcessExecutionFailureKind FailureKind,
    int ExitCode);
