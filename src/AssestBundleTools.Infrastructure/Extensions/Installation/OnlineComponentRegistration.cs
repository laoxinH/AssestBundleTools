using AssestBundleTools.Application.Extensions.Dtos;

namespace AssestBundleTools.Infrastructure.Extensions.Installation;

internal sealed record OnlineComponentRegistration(
    string Id,
    string DisplayName,
    OnlineComponentKind Kind,
    string Publisher,
    string ProjectUri,
    string LicenseName,
    string SourceDisplayName,
    string InstallDirectoryPath,
    string RequiredFileRelativePath,
    IOnlineComponentPackageSource PackageSource,
    long MaximumDownloadSizeInBytes = 64L * 1024L * 1024L,
    long MaximumExtractedSizeInBytes = 256L * 1024L * 1024L,
    int MaximumArchiveEntryCount = 512)
{
    public bool IsInstalled =>
        File.Exists(Path.Combine(InstallDirectoryPath, RequiredFileRelativePath));

    public OnlineComponentDescriptorDto CreateDescriptor() =>
        new(
            Id,
            DisplayName,
            Kind,
            Publisher,
            ProjectUri,
            LicenseName,
            SourceDisplayName,
            IsInstalled);
}

internal interface IOnlineComponentPackageSource
{
    Task<ResolvedOnlineComponentPackage> ResolveAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken);

    bool IsDownloadUriAllowed(Uri downloadUri);
}

internal sealed record ResolvedOnlineComponentPackage(
    Uri DownloadUri,
    string Sha256,
    long? SizeInBytes,
    string? Version);
