using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using AssestBundleTools.Application.Extensions.Dtos;

namespace AssestBundleTools.Infrastructure.Extensions.Installation;

internal static class VerifiedZipComponentInstaller
{
    public static async Task DownloadAsync(
        HttpClient httpClient,
        OnlineComponentRegistration registration,
        ResolvedOnlineComponentPackage package,
        string archivePath,
        IProgress<OnlineComponentInstallProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        if (package.SizeInBytes is > 0 && package.SizeInBytes > registration.MaximumDownloadSizeInBytes)
        {
            throw new InvalidDataException("Online component package exceeds the download size limit.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, package.DownloadUri);
        request.Headers.UserAgent.ParseAdd("AssestBundleTools/1.0");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri is not { } finalUri ||
            !registration.PackageSource.IsDownloadUriAllowed(finalUri))
        {
            throw new InvalidDataException("Online component download redirected outside the trusted source.");
        }

        var contentLength = response.Content.Headers.ContentLength ?? package.SizeInBytes;
        if (contentLength is > 0 && contentLength > registration.MaximumDownloadSizeInBytes)
        {
            throw new InvalidDataException("Online component response exceeds the download size limit.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            archivePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        long received = 0;
        try
        {
            while (true)
            {
                var read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                received += read;
                if (received > registration.MaximumDownloadSizeInBytes)
                {
                    throw new InvalidDataException("Online component download exceeded the size limit.");
                }

                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                progress?.Report(new OnlineComponentInstallProgressDto(
                    OnlineComponentInstallStage.Downloading,
                    received,
                    contentLength));
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        await output.FlushAsync(cancellationToken);
        progress?.Report(new OnlineComponentInstallProgressDto(OnlineComponentInstallStage.Verifying));
        var actualHash = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        if (!string.Equals(actualHash, package.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Online component package SHA-256 verification failed.");
        }
    }

    public static void Extract(
        OnlineComponentRegistration registration,
        string archivePath,
        string extractionDirectoryPath)
    {
        Directory.CreateDirectory(extractionDirectoryPath);
        var extractionRoot = EnsureTrailingSeparator(Path.GetFullPath(extractionDirectoryPath));
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > registration.MaximumArchiveEntryCount)
        {
            throw new InvalidDataException("Online component archive has too many entries.");
        }

        long extractedSize = 0;
        foreach (var entry in archive.Entries)
        {
            RejectSymbolicLink(entry);
            extractedSize = checked(extractedSize + entry.Length);
            if (extractedSize > registration.MaximumExtractedSizeInBytes)
            {
                throw new InvalidDataException("Online component archive exceeds the extraction size limit.");
            }

            var normalizedEntryName = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedEntryName))
            {
                throw new InvalidDataException("Online component archive contains an absolute path.");
            }

            var destinationPath = Path.GetFullPath(Path.Combine(extractionRoot, normalizedEntryName));
            if (!destinationPath.StartsWith(extractionRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Online component archive contains a path outside the extraction root.");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            entry.ExtractToFile(destinationPath, overwrite: false);
        }

        var requiredFilePath = Path.Combine(extractionDirectoryPath, registration.RequiredFileRelativePath);
        if (!File.Exists(requiredFilePath))
        {
            throw new InvalidDataException("Online component archive does not contain the required executable.");
        }
    }

    public static void Commit(OnlineComponentRegistration registration, string extractionDirectoryPath)
    {
        var targetDirectoryPath = Path.GetFullPath(registration.InstallDirectoryPath);
        var targetParentDirectoryPath = Path.GetDirectoryName(targetDirectoryPath)
            ?? throw new InvalidOperationException("Online component install directory has no parent.");
        Directory.CreateDirectory(targetParentDirectoryPath);
        var backupDirectoryPath = $"{targetDirectoryPath}.backup-{Guid.NewGuid():N}";
        var movedExistingDirectory = false;
        try
        {
            if (Directory.Exists(targetDirectoryPath))
            {
                Directory.Move(targetDirectoryPath, backupDirectoryPath);
                movedExistingDirectory = true;
            }

            Directory.Move(extractionDirectoryPath, targetDirectoryPath);
            TryDeleteDirectory(backupDirectoryPath);
        }
        catch
        {
            if (!Directory.Exists(targetDirectoryPath) && movedExistingDirectory && Directory.Exists(backupDirectoryPath))
            {
                Directory.Move(backupDirectoryPath, targetDirectoryPath);
            }

            throw;
        }
    }

    public static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    public static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            DeleteDirectoryIfExists(directoryPath);
        }
        catch
        {
            // 新版本已经原子切换成功时，旧备份清理失败只保留为后续诊断痕迹。
        }
    }

    private static void RejectSymbolicLink(ZipArchiveEntry entry)
    {
        const int UnixFileTypeMask = 0xF000;
        const int UnixSymbolicLink = 0xA000;
        var unixMode = (entry.ExternalAttributes >> 16) & 0xFFFF;
        if ((unixMode & UnixFileTypeMask) == UnixSymbolicLink)
        {
            throw new InvalidDataException("Online component archive contains a symbolic link.");
        }
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
}
