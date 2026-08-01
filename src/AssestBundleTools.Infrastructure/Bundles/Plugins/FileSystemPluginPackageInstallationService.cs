using System.IO.Compression;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Bundles.PostExport;
using AssestBundleTools.Infrastructure.Bundles.Processing;

namespace AssestBundleTools.Infrastructure.Bundles.Plugins;

public sealed class FileSystemPluginPackageInstallationService : IPluginPackageInstallationService
{
    private const int MaximumEntryCount = 4096;
    private const long MaximumExpandedBytes = 512L * 1024 * 1024;

    private readonly IAssetBundleDecryptorCatalogStore _decryptorCatalogStore;
    private readonly IResourceProcessorCatalogStore _resourceProcessorCatalogStore;
    private readonly IPostExportPluginCatalogStore _postExportPluginCatalogStore;
    private readonly IReadOnlyDictionary<PluginPackageKindDto, string> _installRoots;
    private readonly string _stagingRoot;

    public FileSystemPluginPackageInstallationService(
        IAssetBundleDecryptorCatalogStore decryptorCatalogStore,
        IResourceProcessorCatalogStore resourceProcessorCatalogStore,
        IPostExportPluginCatalogStore postExportPluginCatalogStore,
        string? decryptorInstallRoot = null,
        string? resourceProcessorInstallRoot = null,
        string? postExportInstallRoot = null,
        string? stagingRoot = null)
    {
        _decryptorCatalogStore = decryptorCatalogStore ?? throw new ArgumentNullException(nameof(decryptorCatalogStore));
        _resourceProcessorCatalogStore = resourceProcessorCatalogStore ?? throw new ArgumentNullException(nameof(resourceProcessorCatalogStore));
        _postExportPluginCatalogStore = postExportPluginCatalogStore ?? throw new ArgumentNullException(nameof(postExportPluginCatalogStore));
        _installRoots = new Dictionary<PluginPackageKindDto, string>
        {
            [PluginPackageKindDto.AssetBundleCodec] = Path.GetFullPath(
                decryptorInstallRoot ?? AssetBundleDecryptorDiscoveryPaths.GetUserManagedManifestDirectory()),
            [PluginPackageKindDto.ResourceProcessor] = Path.GetFullPath(
                resourceProcessorInstallRoot ?? ResourceProcessorDiscoveryPaths.GetUserManagedManifestDirectory()),
            [PluginPackageKindDto.PostExport] = Path.GetFullPath(
                postExportInstallRoot ?? PostExportPluginDiscoveryPaths.GetUserManagedManifestDirectory())
        };
        _stagingRoot = Path.GetFullPath(stagingRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AssestBundleTools",
            "plugin-import-staging"));
    }

    public async Task<Result<PluginPackageImportResultDto>> ImportAsync(
        string sourcePath,
        bool replaceExisting,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        if (!Directory.Exists(normalizedSourcePath) && !File.Exists(normalizedSourcePath))
        {
            return Failure(ApplicationErrors.PluginPackageSourceInvalid("NotFound"));
        }

        if (File.Exists(normalizedSourcePath) &&
            !string.Equals(Path.GetExtension(normalizedSourcePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            return Failure(ApplicationErrors.PluginPackageSourceInvalid("ArchiveExtension"));
        }

        Directory.CreateDirectory(_stagingRoot);
        var preparedDirectoryPath = Path.Combine(_stagingRoot, $"prepare-{Guid.NewGuid():N}");
        Directory.CreateDirectory(preparedDirectoryPath);
        try
        {
            var prepareResult = Directory.Exists(normalizedSourcePath)
                ? await CopyDirectoryPackageAsync(normalizedSourcePath, preparedDirectoryPath, cancellationToken)
                : await ExtractZipPackageAsync(normalizedSourcePath, preparedDirectoryPath, cancellationToken);
            if (prepareResult.IsFailure)
            {
                return Failure(prepareResult.Errors);
            }

            var inspection = PluginPackageManifestInspector.Inspect(preparedDirectoryPath);
            if (!inspection.IsSuccess || inspection.Value is null)
            {
                return Failure(inspection.Errors);
            }

            var descriptor = inspection.Value;
            var installRoot = _installRoots[descriptor.Kind];
            var targetDirectoryPath = Path.Combine(installRoot, SanitizeDirectoryName(descriptor.Id));
            var existingEntry = FindExistingEntry(descriptor);
            if (existingEntry.IsReadOnly)
            {
                return Failure(ApplicationErrors.PluginPackageReadOnlyConflict(descriptor.Id));
            }

            if (!replaceExisting && (existingEntry.Exists || Directory.Exists(targetDirectoryPath)))
            {
                return Result<PluginPackageImportResultDto>.Success(CreateResult(
                    descriptor,
                    PluginPackageImportStatusDto.OverwriteConfirmationRequired,
                    targetDirectoryPath));
            }

            var installResult = InstallPreparedPackage(
                preparedDirectoryPath,
                descriptor,
                installRoot,
                targetDirectoryPath,
                existingEntry.ManifestPath,
                cancellationToken);
            if (installResult.IsFailure)
            {
                return Failure(installResult.Errors);
            }

            return Result<PluginPackageImportResultDto>.Success(CreateResult(
                descriptor,
                PluginPackageImportStatusDto.Installed,
                targetDirectoryPath));
        }
        catch (OperationCanceledException)
        {
            return Result<PluginPackageImportResultDto>.Cancelled();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return Failure(ApplicationErrors.PluginPackageInstallFailed(exception.GetType().Name));
        }
        finally
        {
            TryDeleteDirectory(preparedDirectoryPath);
        }
    }

    private static async Task<Result> CopyDirectoryPackageAsync(
        string sourceDirectoryPath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken)
    {
        var sourceRoot = Path.GetFullPath(sourceDirectoryPath);
        var files = new List<FileInfo>();
        long totalBytes = 0;
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var attributes = File.GetAttributes(entryPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return Result.Failure(ApplicationErrors.PluginPackageSourceInvalid("ReparsePoint"));
            }

            if ((attributes & FileAttributes.Directory) != 0)
            {
                continue;
            }

            var file = new FileInfo(entryPath);
            files.Add(file);
            totalBytes += file.Length;
            if (files.Count > MaximumEntryCount || totalBytes > MaximumExpandedBytes)
            {
                return Result.Failure(ApplicationErrors.PluginPackageSourceInvalid("PackageLimit"));
            }
        }

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(sourceRoot, file.FullName);
            var destinationPath = ResolveSafeDestination(destinationDirectoryPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await using var source = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            await using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await source.CopyToAsync(destination, cancellationToken);
        }

        return Result.Success();
    }

    private static async Task<Result> ExtractZipPackageAsync(
        string archivePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumEntryCount)
        {
            return Result.Failure(ApplicationErrors.PluginPackageSourceInvalid("ArchiveEntryLimit"));
        }

        long totalBytes = 0;
        var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            totalBytes += entry.Length;
            if (totalBytes > MaximumExpandedBytes)
            {
                return Result.Failure(ApplicationErrors.PluginPackageSourceInvalid("ArchiveSizeLimit"));
            }

            if (IsSymbolicLink(entry))
            {
                return Result.Failure(ApplicationErrors.PluginPackageSourceInvalid("ArchiveSymbolicLink"));
            }

            var targetPath = ResolveSafeDestination(destinationDirectoryPath, entry.FullName);
            if (!targetPaths.Add(targetPath))
            {
                return Result.Failure(ApplicationErrors.PluginPackageSourceInvalid("ArchiveDuplicatePath"));
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var source = entry.Open();
            await using var destination = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await source.CopyToAsync(destination, cancellationToken);
        }

        return Result.Success();
    }

    private Result InstallPreparedPackage(
        string preparedDirectoryPath,
        PluginPackageDescriptor descriptor,
        string installRoot,
        string targetDirectoryPath,
        string? existingManifestPath,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(installRoot);
        var incomingDirectoryPath = Path.Combine(installRoot, $".import-{Guid.NewGuid():N}");
        // 备份不能放在会被 catalog 递归扫描的安装根目录，否则覆盖验证会重新读到旧 manifest。
        var backupDirectoryPath = Path.Combine(_stagingRoot, $"backup-{Guid.NewGuid():N}");
        string? previousManifestBackupPath = null;
        var targetWasPromoted = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Move(preparedDirectoryPath, incomingDirectoryPath);
            if (Directory.Exists(targetDirectoryPath))
            {
                Directory.Move(targetDirectoryPath, backupDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(existingManifestPath) &&
                File.Exists(existingManifestPath) &&
                !IsWithin(existingManifestPath, targetDirectoryPath))
            {
                previousManifestBackupPath = Path.Combine(_stagingRoot, $"manifest-{Guid.NewGuid():N}.json");
                File.Move(existingManifestPath, previousManifestBackupPath);
            }

            Directory.Move(incomingDirectoryPath, targetDirectoryPath);
            targetWasPromoted = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (!VerifyInstalled(descriptor, targetDirectoryPath))
            {
                throw new InvalidDataException("CatalogVerificationFailed");
            }

            TryDeleteDirectory(backupDirectoryPath);
            TryDeleteFile(previousManifestBackupPath);
            return Result.Success();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or OperationCanceledException)
        {
            if (targetWasPromoted)
            {
                TryDeleteDirectory(targetDirectoryPath);
            }

            if (Directory.Exists(backupDirectoryPath) && !Directory.Exists(targetDirectoryPath))
            {
                Directory.Move(backupDirectoryPath, targetDirectoryPath);
            }

            if (!string.IsNullOrWhiteSpace(previousManifestBackupPath) &&
                File.Exists(previousManifestBackupPath) &&
                !string.IsNullOrWhiteSpace(existingManifestPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(existingManifestPath)!);
                File.Move(previousManifestBackupPath, existingManifestPath);
            }

            if (exception is OperationCanceledException)
            {
                throw;
            }

            return Result.Failure(ApplicationErrors.PluginPackageInstallFailed(exception.Message));
        }
        finally
        {
            TryDeleteDirectory(incomingDirectoryPath);
            TryDeleteDirectory(backupDirectoryPath);
            TryDeleteFile(previousManifestBackupPath);
        }
    }

    private ExistingPluginEntry FindExistingEntry(PluginPackageDescriptor descriptor)
    {
        return descriptor.Kind switch
        {
            PluginPackageKindDto.AssetBundleCodec => _decryptorCatalogStore.Load()
                .Where(entry => string.Equals(entry.Definition.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase))
                .Select(entry => new ExistingPluginEntry(true, entry.IsReadOnly, entry.ManifestFilePath))
                .FirstOrDefault(),
            PluginPackageKindDto.ResourceProcessor => _resourceProcessorCatalogStore.Load()
                .Where(entry => string.Equals(entry.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase))
                .Select(entry => new ExistingPluginEntry(true, entry.IsReadOnly, entry.ManifestFilePath))
                .FirstOrDefault(),
            PluginPackageKindDto.PostExport => _postExportPluginCatalogStore.Load()
                .Where(entry => string.Equals(entry.Id, descriptor.Id, StringComparison.OrdinalIgnoreCase))
                .Select(entry => new ExistingPluginEntry(true, entry.IsReadOnly, entry.ManifestFilePath))
                .FirstOrDefault(),
            _ => default
        };
    }

    private bool VerifyInstalled(PluginPackageDescriptor descriptor, string targetDirectoryPath)
    {
        var entry = FindExistingEntry(descriptor);
        return entry.Exists &&
               !entry.IsReadOnly &&
               !string.IsNullOrWhiteSpace(entry.ManifestPath) &&
               IsWithin(entry.ManifestPath, targetDirectoryPath);
    }

    private static PluginPackageImportResultDto CreateResult(
        PluginPackageDescriptor descriptor,
        PluginPackageImportStatusDto status,
        string installDirectoryPath) =>
        new(
            status,
            descriptor.Kind,
            descriptor.Id,
            descriptor.DisplayName,
            descriptor.Version,
            installDirectoryPath);

    private static string ResolveSafeDestination(string rootPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException("PackagePathInvalid");
        }

        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var destinationPath = Path.GetFullPath(Path.Combine(rootPath, normalizedRelativePath));
        if (!IsWithin(destinationPath, rootPath))
        {
            throw new InvalidDataException("PackagePathOutOfScope");
        }

        return destinationPath;
    }

    private static bool IsSymbolicLink(ZipArchiveEntry entry)
    {
        const int UnixFileTypeMask = 0xF000;
        const int UnixSymbolicLink = 0xA000;
        return ((entry.ExternalAttributes >> 16) & UnixFileTypeMask) == UnixSymbolicLink;
    }

    private static bool IsWithin(string path, string rootPath)
    {
        var root = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(path);
        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeDirectoryName(string id)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var value = new string(id.Trim()
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray())
            .Trim()
            .TrimEnd('.');
        return string.IsNullOrWhiteSpace(value) ? "plugin" : value;
    }

    private static void TryDeleteDirectory(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception)
        {
            // 清理失败不覆盖原始导入结果。
        }
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // 清理失败不覆盖原始导入结果。
        }
    }

    private static Result<PluginPackageImportResultDto> Failure(
        params AssestBundleTools.Domain.Common.Errors.AppError[] errors) =>
        Result<PluginPackageImportResultDto>.Failure(errors);

    private static Result<PluginPackageImportResultDto> Failure(
        IReadOnlyList<AssestBundleTools.Domain.Common.Errors.AppError> errors) =>
        Result<PluginPackageImportResultDto>.Failure(errors);

    private readonly record struct ExistingPluginEntry(
        bool Exists,
        bool IsReadOnly,
        string? ManifestPath);
}
