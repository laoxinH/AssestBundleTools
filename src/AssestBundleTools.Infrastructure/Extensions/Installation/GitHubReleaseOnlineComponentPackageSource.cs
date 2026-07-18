using System.Text.Json;

namespace AssestBundleTools.Infrastructure.Extensions.Installation;

internal sealed class GitHubReleaseOnlineComponentPackageSource : IOnlineComponentPackageSource
{
    private const string Sha256Prefix = "sha256:";
    private readonly Uri _releaseApiUri;
    private readonly string _assetName;
    private readonly string _allowedDownloadHost;
    private readonly string _allowedDownloadPathPrefix;
    private readonly IReadOnlySet<string> _allowedRedirectHosts;

    public GitHubReleaseOnlineComponentPackageSource(
        Uri releaseApiUri,
        string assetName,
        string allowedDownloadHost,
        string allowedDownloadPathPrefix,
        IEnumerable<string>? allowedRedirectHosts = null)
    {
        _releaseApiUri = releaseApiUri ?? throw new ArgumentNullException(nameof(releaseApiUri));
        _assetName = string.IsNullOrWhiteSpace(assetName)
            ? throw new ArgumentException("Release asset name is required.", nameof(assetName))
            : assetName.Trim();
        _allowedDownloadHost = string.IsNullOrWhiteSpace(allowedDownloadHost)
            ? throw new ArgumentException("Allowed download host is required.", nameof(allowedDownloadHost))
            : allowedDownloadHost.Trim();
        _allowedDownloadPathPrefix = NormalizePathPrefix(allowedDownloadPathPrefix);
        _allowedRedirectHosts = (allowedRedirectHosts ?? Array.Empty<string>())
            .Where(static host => !string.IsNullOrWhiteSpace(host))
            .Select(static host => host.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ResolvedOnlineComponentPackage> ResolveAsync(
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        using var request = new HttpRequestMessage(HttpMethod.Get, _releaseApiUri);
        request.Headers.UserAgent.ParseAdd("AssestBundleTools/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var version = TryGetString(root, "name") ?? TryGetString(root, "tag_name");
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub release metadata does not contain an assets array.");
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (!string.Equals(TryGetString(asset, "name"), _assetName, StringComparison.Ordinal))
            {
                continue;
            }

            var downloadUrl = TryGetString(asset, "browser_download_url");
            var digest = TryGetString(asset, "digest");
            if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
                !IsDownloadUriAllowed(downloadUri))
            {
                throw new InvalidDataException("GitHub release asset download URL is outside the trusted source.");
            }

            if (string.IsNullOrWhiteSpace(digest) ||
                !digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase) ||
                digest.Length != Sha256Prefix.Length + 64 ||
                !ContainsOnlyHexadecimalCharacters(digest.AsSpan(Sha256Prefix.Length)))
            {
                throw new InvalidDataException("GitHub release asset does not provide a valid SHA-256 digest.");
            }

            long? size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
                ? parsedSize
                : null;
            return new ResolvedOnlineComponentPackage(
                downloadUri,
                digest[Sha256Prefix.Length..].ToLowerInvariant(),
                size,
                version);
        }

        throw new InvalidDataException($"GitHub release asset '{_assetName}' was not found.");
    }

    public bool IsDownloadUriAllowed(Uri downloadUri) =>
        downloadUri.Scheme == Uri.UriSchemeHttps &&
        ((string.Equals(downloadUri.Host, _allowedDownloadHost, StringComparison.OrdinalIgnoreCase) &&
          downloadUri.AbsolutePath.StartsWith(_allowedDownloadPathPrefix, StringComparison.Ordinal)) ||
         _allowedRedirectHosts.Contains(downloadUri.Host));

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool ContainsOnlyHexadecimalCharacters(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizePathPrefix(string pathPrefix)
    {
        if (string.IsNullOrWhiteSpace(pathPrefix))
        {
            throw new ArgumentException("Allowed download path prefix is required.", nameof(pathPrefix));
        }

        var normalized = pathPrefix.Trim();
        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }
}
