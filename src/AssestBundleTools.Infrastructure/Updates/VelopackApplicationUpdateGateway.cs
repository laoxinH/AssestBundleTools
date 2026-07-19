using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Updates.Errors;
using AssestBundleTools.Application.Updates.Models;
using AssestBundleTools.Application.Updates.Services;
using Velopack;
using Velopack.Exceptions;
using Velopack.Locators;
using Velopack.Sources;

namespace AssestBundleTools.Infrastructure.Updates;

public sealed class VelopackApplicationUpdateGateway : IApplicationUpdateGateway
{
    private const string RepositoryUrl = "https://github.com/laoxinH/AssestBundleTools";
    private static readonly TimeSpan CheckCacheLifetime = TimeSpan.FromMinutes(5);
    private readonly object _stateLock = new();
    private readonly UpdateManager _updateManager;
    private readonly string _checkStatePath;
    private readonly string _checkSemaphoreName;
    private UpdateInfo? _availableUpdate;
    private bool _isDownloaded;

    public VelopackApplicationUpdateGateway()
    {
        if (!VelopackLocator.IsCurrentSet)
        {
            VelopackApp.Build()
                .SetAutoApplyOnStartup(false)
                .Run();
        }

        var source = new GithubSource(RepositoryUrl, accessToken: null, prerelease: false);
        _updateManager = new UpdateManager(source);

        RuntimeInfo = new ApplicationUpdateRuntimeInfo(
            ResolveCurrentVersion(_updateManager),
            ResolveInstallMode(_updateManager),
            ResolveArchitecture(),
            _updateManager.IsInstalled);
        var stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AssestBundleTools",
            "updates");
        _checkStatePath = Path.Combine(
            stateDirectory,
            $"check-{RuntimeInfo.Architecture.ToString().ToLowerInvariant()}.json");
        _checkSemaphoreName = $"Local\\AssestBundleTools.UpdateCheck.{RuntimeInfo.Architecture}";
    }

    public ApplicationUpdateRuntimeInfo RuntimeInfo { get; }

    public async Task<Result<ApplicationUpdateInfo?>> CheckForUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInfo.CanSelfUpdate)
        {
            return Result<ApplicationUpdateInfo?>.Success(null);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var checkSemaphore = new Semaphore(1, 1, _checkSemaphoreName);
            var waitResult = await Task.Run(
                () => WaitHandle.WaitAny([checkSemaphore, cancellationToken.WaitHandle]),
                CancellationToken.None);
            if (waitResult != 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                var cachedUpdate = TryReadRecentCheckState();
                if (cachedUpdate is not null)
                {
                    return Result<ApplicationUpdateInfo?>.Success(cachedUpdate.HasUpdate
                        ? cachedUpdate.ToUpdateInfo()
                        : null);
                }

                var update = await RefreshUpdateInfoCoreAsync(cancellationToken);
                var mappedUpdate = update is null ? null : Map(update);
                TrySaveCheckState(mappedUpdate);
                return Result<ApplicationUpdateInfo?>.Success(mappedUpdate);
            }
            finally
            {
                checkSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result<ApplicationUpdateInfo?>.Cancelled();
        }
        catch (Exception ex)
        {
            return Result<ApplicationUpdateInfo?>.Failure(
                MapCheckError(ex));
        }
    }

    public async Task<Result> DownloadUpdateAsync(
        string targetVersion,
        IProgress<ApplicationUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var update = GetMatchingUpdate(targetVersion);
        if (update is null && RuntimeInfo.CanSelfUpdate)
        {
            try
            {
                update = await RefreshUpdateInfoCoreAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Cancelled();
            }
            catch (Exception ex)
            {
                return Result.Failure(ApplicationUpdateErrors.DownloadFailed(ex.Message));
            }
        }

        if (update is null || !string.Equals(
                update.TargetFullRelease.Version.ToString(),
                targetVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(ApplicationUpdateErrors.UpdateStateExpired(targetVersion));
        }

        try
        {
            var totalBytes = GetDownloadSize(update);
            await _updateManager.DownloadUpdatesAsync(
                update,
                percent => progress?.Report(new ApplicationUpdateProgress(
                    totalBytes * percent / 100,
                    totalBytes)),
                cancellationToken);

            lock (_stateLock)
            {
                _isDownloaded = true;
            }

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Result.Cancelled();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex switch
            {
                ChecksumFailedException => ApplicationUpdateErrors.ChecksumMismatch(ex.Message),
                IOException ioException when IsDiskFull(ioException) =>
                    ApplicationUpdateErrors.InsufficientDiskSpace(ex.Message),
                _ => ApplicationUpdateErrors.DownloadFailed(ex.Message)
            });
        }
    }

    public Result ApplyUpdateAndRestart(string targetVersion)
    {
        var update = GetMatchingUpdate(targetVersion);
        lock (_stateLock)
        {
            if (update is null || !_isDownloaded)
            {
                return Result.Failure(ApplicationUpdateErrors.UpdateStateExpired(targetVersion));
            }
        }

        try
        {
            _updateManager.ApplyUpdatesAndRestart(update.TargetFullRelease, restartArgs: []);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex is UnauthorizedAccessException
                ? ApplicationUpdateErrors.DirectoryNotWritable(ex.Message)
                : ApplicationUpdateErrors.UpdaterLaunchFailed(ex.Message));
        }
    }

    private UpdateInfo? GetMatchingUpdate(string targetVersion)
    {
        lock (_stateLock)
        {
            return string.Equals(
                _availableUpdate?.TargetFullRelease.Version.ToString(),
                targetVersion,
                StringComparison.OrdinalIgnoreCase)
                    ? _availableUpdate
                    : null;
        }
    }

    private async Task<UpdateInfo?> RefreshUpdateInfoCoreAsync(CancellationToken cancellationToken)
    {
        var update = await _updateManager.CheckForUpdatesAsync().WaitAsync(cancellationToken);
        lock (_stateLock)
        {
            _availableUpdate = update;
            _isDownloaded = update is not null &&
                _updateManager.UpdatePendingRestart?.Version == update.TargetFullRelease.Version;
        }

        return update;
    }

    private CachedApplicationUpdateCheck? TryReadRecentCheckState()
    {
        try
        {
            if (!File.Exists(_checkStatePath))
            {
                return null;
            }

            var state = JsonSerializer.Deserialize<CachedApplicationUpdateCheck>(
                File.ReadAllText(_checkStatePath));
            if (state is null ||
                state.Architecture != RuntimeInfo.Architecture ||
                !string.Equals(state.CurrentVersion, RuntimeInfo.CurrentVersion, StringComparison.OrdinalIgnoreCase) ||
                DateTimeOffset.UtcNow - state.CheckedAtUtc > CheckCacheLifetime)
            {
                return null;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    private void TrySaveCheckState(ApplicationUpdateInfo? update)
    {
        try
        {
            var directory = Path.GetDirectoryName(_checkStatePath)!;
            Directory.CreateDirectory(directory);
            var state = CachedApplicationUpdateCheck.Create(RuntimeInfo, update);
            var temporaryPath = $"{_checkStatePath}.{Environment.ProcessId}.tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(state));
            File.Move(temporaryPath, _checkStatePath, overwrite: true);
        }
        catch
        {
            // 更新检查结果仍有效；缓存不可写只会导致下一进程重新请求。
        }
    }

    private static ApplicationUpdateInfo Map(UpdateInfo update) =>
        new(
            update.TargetFullRelease.Version.ToString(),
            update.TargetFullRelease.NotesMarkdown,
            GetDownloadSize(update),
            update.DeltasToTarget.Length > 0);

    private static long GetDownloadSize(UpdateInfo update) =>
        update.DeltasToTarget.Length > 0
            ? update.DeltasToTarget.Sum(static asset => asset.Size)
            : update.TargetFullRelease.Size;

    private static ApplicationInstallMode ResolveInstallMode(UpdateManager updateManager)
    {
        if (!updateManager.IsInstalled)
        {
            return ApplicationInstallMode.Unmanaged;
        }

        return updateManager.IsPortable
            ? ApplicationInstallMode.Portable
            : ApplicationInstallMode.Installed;
    }

    private static ApplicationUpdateArchitecture ResolveArchitecture() =>
        RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X86 => ApplicationUpdateArchitecture.X86,
            Architecture.Arm64 => ApplicationUpdateArchitecture.Arm64,
            _ => ApplicationUpdateArchitecture.X64
        };

    private static string ResolveCurrentVersion(UpdateManager updateManager)
    {
        if (updateManager.CurrentVersion is not null)
        {
            return updateManager.CurrentVersion.ToString();
        }

        var assembly = typeof(VelopackApplicationUpdateGateway).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return informationalVersion?.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
    }

    private static AssestBundleTools.Domain.Common.Errors.AppError MapCheckError(Exception exception)
    {
        if (exception is System.Text.Json.JsonException or InvalidDataException)
        {
            return ApplicationUpdateErrors.InvalidFeed(exception.Message);
        }

        if (exception is HttpRequestException httpException)
        {
            if (httpException.StatusCode is System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.TooManyRequests)
            {
                return ApplicationUpdateErrors.RateLimited(exception.Message);
            }

            return httpException.StatusCode is null
                ? ApplicationUpdateErrors.NetworkUnavailable(exception.Message)
                : ApplicationUpdateErrors.SourceUnavailable(exception.Message);
        }

        return ApplicationUpdateErrors.CheckFailed(exception.Message);
    }

    private static bool IsDiskFull(IOException exception)
    {
        var win32Error = exception.HResult & 0xffff;
        return win32Error is 39 or 112;
    }

    private sealed record CachedApplicationUpdateCheck(
        DateTimeOffset CheckedAtUtc,
        string CurrentVersion,
        ApplicationUpdateArchitecture Architecture,
        bool HasUpdate,
        string? TargetVersion,
        string? ReleaseNotes,
        long? DownloadSizeInBytes,
        bool IsDeltaUpdate)
    {
        public static CachedApplicationUpdateCheck Create(
            ApplicationUpdateRuntimeInfo runtimeInfo,
            ApplicationUpdateInfo? update) =>
            new(
                DateTimeOffset.UtcNow,
                runtimeInfo.CurrentVersion,
                runtimeInfo.Architecture,
                update is not null,
                update?.TargetVersion,
                update?.ReleaseNotes,
                update?.DownloadSizeInBytes,
                update?.IsDeltaUpdate ?? false);

        public ApplicationUpdateInfo ToUpdateInfo() =>
            new(
                TargetVersion ?? throw new InvalidDataException("Cached update version is missing."),
                ReleaseNotes,
                DownloadSizeInBytes,
                IsDeltaUpdate);
    }
}
