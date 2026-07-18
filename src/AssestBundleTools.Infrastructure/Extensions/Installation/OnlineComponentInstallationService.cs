using System.Collections.Concurrent;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Extensions;
using AssestBundleTools.Application.Extensions.Dtos;
using AssestBundleTools.Application.Extensions.Services;

namespace AssestBundleTools.Infrastructure.Extensions.Installation;

public sealed class OnlineComponentInstallationService : IOnlineComponentInstallationService
{
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyDictionary<string, OnlineComponentRegistration> _registrations;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _installGates = new(StringComparer.OrdinalIgnoreCase);

    public OnlineComponentInstallationService()
        : this(new HttpClient(), OnlineComponentCatalog.CreateDefault())
    {
    }

    internal OnlineComponentInstallationService(
        HttpClient httpClient,
        IEnumerable<OnlineComponentRegistration> registrations)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _registrations = (registrations ?? throw new ArgumentNullException(nameof(registrations)))
            .ToDictionary(static registration => registration.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<OnlineComponentDescriptorDto> GetCatalog() =>
        _registrations.Values
            .OrderBy(static registration => registration.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(static registration => registration.CreateDescriptor())
            .ToArray();

    public OnlineComponentDescriptorDto? ResolveDescriptor(string componentId) =>
        TryResolveRegistration(componentId, out var registration)
            ? registration.CreateDescriptor()
            : null;

    public async Task<Result<OnlineComponentInstallResultDto>> InstallAsync(
        string componentId,
        IProgress<OnlineComponentInstallProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveRegistration(componentId, out var registration))
        {
            return Result<OnlineComponentInstallResultDto>.Failure(
                OnlineComponentErrors.NotRegistered(componentId ?? string.Empty));
        }

        var installGate = _installGates.GetOrAdd(registration.Id, static _ => new SemaphoreSlim(1, 1));
        try
        {
            await installGate.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<OnlineComponentInstallResultDto>.Cancelled();
        }

        try
        {
            if (registration.IsInstalled)
            {
                return Result<OnlineComponentInstallResultDto>.Success(new OnlineComponentInstallResultDto(
                    registration.Id,
                    registration.DisplayName,
                    InstalledVersion: null,
                    WasAlreadyInstalled: true));
            }

            return await InstallCoreAsync(registration, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result<OnlineComponentInstallResultDto>.Cancelled();
        }
        catch (Exception exception)
        {
            return Result<OnlineComponentInstallResultDto>.Failure(
                OnlineComponentErrors.InstallFailed(registration.Id, exception.ToString()));
        }
        finally
        {
            installGate.Release();
        }
    }

    private async Task<Result<OnlineComponentInstallResultDto>> InstallCoreAsync(
        OnlineComponentRegistration registration,
        IProgress<OnlineComponentInstallProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OnlineComponentInstallProgressDto(OnlineComponentInstallStage.ResolvingPackage));
        var package = await registration.PackageSource.ResolveAsync(_httpClient, cancellationToken);
        var stagingParentDirectoryPath = Path.Combine(
            Path.GetDirectoryName(registration.InstallDirectoryPath)!,
            ".staging",
            SanitizePathSegment(registration.Id));
        var stagingDirectoryPath = Path.Combine(stagingParentDirectoryPath, Guid.NewGuid().ToString("N"));
        var archivePath = Path.Combine(stagingDirectoryPath, "package.zip");
        var extractionDirectoryPath = Path.Combine(stagingDirectoryPath, "content");
        Directory.CreateDirectory(stagingDirectoryPath);
        try
        {
            await VerifiedZipComponentInstaller.DownloadAsync(
                _httpClient,
                registration,
                package,
                archivePath,
                progress,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new OnlineComponentInstallProgressDto(OnlineComponentInstallStage.Extracting));
            VerifiedZipComponentInstaller.Extract(registration, archivePath, extractionDirectoryPath);
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new OnlineComponentInstallProgressDto(OnlineComponentInstallStage.Installing));
            VerifiedZipComponentInstaller.Commit(registration, extractionDirectoryPath);
            return Result<OnlineComponentInstallResultDto>.Success(new OnlineComponentInstallResultDto(
                registration.Id,
                registration.DisplayName,
                package.Version,
                WasAlreadyInstalled: false));
        }
        finally
        {
            VerifiedZipComponentInstaller.TryDeleteDirectory(stagingDirectoryPath);
        }
    }

    private bool TryResolveRegistration(
        string? componentId,
        out OnlineComponentRegistration registration)
    {
        if (!string.IsNullOrWhiteSpace(componentId) &&
            _registrations.TryGetValue(componentId.Trim(), out registration!))
        {
            return true;
        }

        registration = null!;
        return false;
    }

    private static string SanitizePathSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
    }
}
