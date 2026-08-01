using System.Text.Json;
using System.Text.RegularExpressions;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Errors;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Infrastructure.Bundles.Decryption;
using AssestBundleTools.Infrastructure.Bundles.PostExport;
using AssestBundleTools.Infrastructure.Bundles.Processing;

namespace AssestBundleTools.Infrastructure.Bundles.Plugins;

internal static partial class PluginPackageManifestInspector
{
    private static readonly IReadOnlySet<string> ResourceKinds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Texture2D",
        "Audio",
        "Atlas",
        "Skel"
    };

    private static readonly IReadOnlySet<string> CodecPlaceholders = new HashSet<string>(StringComparer.Ordinal)
    {
        "{inputPath}",
        "{outputPath}",
        "{resultPath}",
        "{workspaceKey}",
        "{workspaceDataRootPath}",
        "{manifestDirectory}"
    };

    private static readonly IReadOnlySet<string> ProcessorPlaceholders = new HashSet<string>(StringComparer.Ordinal)
    {
        "{inputPath}",
        "{outputPath}",
        "{resultPath}",
        "{resourceKind}",
        "{assetName}",
        "{bundleName}",
        "{workspaceKey}",
        "{workspaceDataRootPath}",
        "{manifestDirectory}"
    };

    private static readonly IReadOnlySet<string> PostExportPlaceholders = new HashSet<string>(StringComparer.Ordinal)
    {
        "{manifestPath}",
        "{outputDirectory}",
        "{workspaceId}",
        "{workspaceKey}",
        "{manifestDirectory}"
    };

    public static Result<PluginPackageDescriptor> Inspect(string packageRootPath)
    {
        var candidates = new List<ManifestCandidate>();
        foreach (var filePath in Directory.EnumerateFiles(packageRootPath, "*.json", SearchOption.AllDirectories))
        {
            var candidateResult = TryReadCandidate(filePath);
            if (candidateResult.IsFailure)
            {
                return Result<PluginPackageDescriptor>.Failure(candidateResult.Errors);
            }

            if (candidateResult.Value is not null)
            {
                candidates.Add(candidateResult.Value);
            }
        }

        if (candidates.Count == 0)
        {
            return Invalid("ManifestMissing");
        }

        if (candidates.Count != 1)
        {
            return Invalid($"ManifestCount={candidates.Count}");
        }

        var candidate = candidates[0];
        return candidate.Kind switch
        {
            PluginPackageKindDto.AssetBundleCodec => InspectCodec(candidate, packageRootPath),
            PluginPackageKindDto.ResourceProcessor => InspectProcessor(candidate, packageRootPath),
            PluginPackageKindDto.PostExport => InspectPostExport(candidate, packageRootPath),
            _ => Invalid("KindUnsupported")
        };
    }

    private static Result<ManifestCandidate?> TryReadCandidate(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Result<ManifestCandidate?>.Success(null);
            }

            var root = document.RootElement;
            if (TryGetString(root, "kind", out var kind))
            {
                if (TryParseKind(kind, out var parsedKind))
                {
                    return Result<ManifestCandidate?>.Success(new ManifestCandidate(filePath, parsedKind));
                }

                if (HasProperty(root, "id") && HasProperty(root, "executablePath"))
                {
                    return Result<ManifestCandidate?>.Failure(
                        ApplicationErrors.PluginPackageManifestInvalid($"Kind={kind}"));
                }
            }

            return Result<ManifestCandidate?>.Success(null);
        }
        catch (JsonException)
        {
            return Result<ManifestCandidate?>.Success(null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<ManifestCandidate?>.Failure(
                ApplicationErrors.PluginPackageManifestInvalid(exception.GetType().Name));
        }
    }

    private static Result<PluginPackageDescriptor> InspectCodec(
        ManifestCandidate candidate,
        string packageRootPath)
    {
        var manifest = Deserialize<AssetBundleExternalProcessDecryptorManifest>(
            candidate.FilePath,
            AssetBundleDecryptorJsonSerialization.SerializerOptions);
        if (manifest is null ||
            manifest.SchemaVersion != 1 ||
            !string.Equals(manifest.Kind, "assetBundleCodec", StringComparison.OrdinalIgnoreCase) ||
            !HasIdentity(manifest.Id, manifest.DisplayName, manifest.Version) ||
            manifest.TimeoutMs <= 0 ||
            manifest.DecryptArguments.Count == 0)
        {
            return Invalid("CodecFields");
        }

        if (manifest.SupportsAutoDetection && manifest.ProbeArguments.Count == 0)
        {
            return Invalid("CodecProbeArguments");
        }

        var argumentError = ValidateArguments(manifest.DecryptArguments, CodecPlaceholders, "CodecDecrypt") ??
                            ValidateRequiredPlaceholders(manifest.DecryptArguments, "CodecDecrypt", "{inputPath}", "{outputPath}", "{resultPath}") ??
                            ValidateArguments(manifest.ProbeArguments, CodecPlaceholders, "CodecProbe") ??
                            (manifest.SupportsAutoDetection
                                ? ValidateRequiredPlaceholders(manifest.ProbeArguments, "CodecProbe", "{inputPath}", "{resultPath}")
                                : null) ??
                            ValidateArguments(manifest.EncryptArguments, CodecPlaceholders, "CodecEncrypt", allowParameterPlaceholder: true) ??
                            (manifest.EncryptArguments.Count > 0
                                ? ValidateRequiredPlaceholders(manifest.EncryptArguments, "CodecEncrypt", "{inputPath}", "{outputPath}", "{resultPath}")
                                : null);
        if (argumentError is not null)
        {
            return Invalid(argumentError);
        }

        return CreateDescriptor(
            candidate,
            packageRootPath,
            manifest.Id,
            manifest.DisplayName,
            manifest.Version,
            manifest.ExecutablePath,
            manifest.WorkingDirectoryPath);
    }

    private static Result<PluginPackageDescriptor> InspectProcessor(
        ManifestCandidate candidate,
        string packageRootPath)
    {
        var manifest = Deserialize<ResourceProcessorManifest>(
            candidate.FilePath,
            ResourceProcessorJsonSerialization.SerializerOptions);
        if (manifest is null ||
            manifest.SchemaVersion != 1 ||
            !string.Equals(manifest.Kind, "resourceProcessor", StringComparison.OrdinalIgnoreCase) ||
            !HasIdentity(manifest.Id, manifest.DisplayName, manifest.Version) ||
            manifest.TimeoutMs <= 0 ||
            manifest.ResourceKinds.Count == 0 ||
            manifest.ResourceKinds.Any(kind => !ResourceKinds.Contains(kind)) ||
            manifest.ProcessArguments.Count == 0)
        {
            return Invalid("ResourceProcessorFields");
        }

        var argumentError = ValidateArguments(manifest.ProcessArguments, ProcessorPlaceholders, "ResourceProcessor") ??
                            ValidateRequiredPlaceholders(
                                manifest.ProcessArguments,
                                "ResourceProcessor",
                                "{inputPath}",
                                "{outputPath}",
                                "{resultPath}");
        if (argumentError is not null)
        {
            return Invalid(argumentError);
        }

        return CreateDescriptor(
            candidate,
            packageRootPath,
            manifest.Id,
            manifest.DisplayName,
            manifest.Version,
            manifest.ExecutablePath,
            manifest.WorkingDirectoryPath);
    }

    private static Result<PluginPackageDescriptor> InspectPostExport(
        ManifestCandidate candidate,
        string packageRootPath)
    {
        var manifest = Deserialize<PostExportPluginManifest>(
            candidate.FilePath,
            ResourceProcessorJsonSerialization.SerializerOptions);
        if (manifest is null ||
            manifest.SchemaVersion != 1 ||
            !string.Equals(manifest.Kind, "postExport", StringComparison.OrdinalIgnoreCase) ||
            !HasIdentity(manifest.Id, manifest.DisplayName, manifest.Version) ||
            manifest.ProcessArguments.Count == 0)
        {
            return Invalid("PostExportFields");
        }

        var argumentError = ValidateArguments(manifest.ProcessArguments, PostExportPlaceholders, "PostExport") ??
                            ValidateRequiredPlaceholders(manifest.ProcessArguments, "PostExport", "{manifestPath}");
        if (argumentError is not null)
        {
            return Invalid(argumentError);
        }

        return CreateDescriptor(
            candidate,
            packageRootPath,
            manifest.Id,
            manifest.DisplayName,
            manifest.Version,
            manifest.ExecutablePath,
            manifest.WorkingDirectoryPath);
    }

    private static Result<PluginPackageDescriptor> CreateDescriptor(
        ManifestCandidate candidate,
        string packageRootPath,
        string id,
        string displayName,
        string version,
        string executablePath,
        string? workingDirectoryPath)
    {
        var pathError = ValidatePackagePath(
            candidate.FilePath,
            packageRootPath,
            executablePath,
            requireFile: true,
            "ExecutablePath");
        if (pathError is not null)
        {
            return Invalid(pathError);
        }

        if (!string.IsNullOrWhiteSpace(workingDirectoryPath))
        {
            pathError = ValidatePackagePath(
                candidate.FilePath,
                packageRootPath,
                workingDirectoryPath,
                requireFile: false,
                "WorkingDirectoryPath");
            if (pathError is not null)
            {
                return Invalid(pathError);
            }
        }

        return Result<PluginPackageDescriptor>.Success(new PluginPackageDescriptor(
            candidate.Kind,
            id.Trim(),
            displayName.Trim(),
            version.Trim(),
            Path.GetRelativePath(packageRootPath, candidate.FilePath)));
    }

    private static string? ValidatePackagePath(
        string manifestPath,
        string packageRootPath,
        string value,
        bool requireFile,
        string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return field;
        }

        var trimmed = value.Trim();
        if (Path.IsPathRooted(trimmed))
        {
            return $"{field}.Absolute";
        }

        var manifestDirectory = Path.GetDirectoryName(manifestPath) ?? packageRootPath;
        var localCandidate = Path.GetFullPath(Path.Combine(manifestDirectory, trimmed));
        if (IsWithin(localCandidate, packageRootPath) &&
            (requireFile ? File.Exists(localCandidate) : Directory.Exists(localCandidate)))
        {
            return null;
        }

        var isPureCommand = requireFile &&
                            !trimmed.Contains(Path.DirectorySeparatorChar) &&
                            !trimmed.Contains(Path.AltDirectorySeparatorChar);
        return isPureCommand && ResolveCommandFromPath(trimmed) is not null
            ? null
            : $"{field}.Missing";
    }

    private static string? ValidateArguments(
        IReadOnlyList<string> arguments,
        IReadOnlySet<string> supportedPlaceholders,
        string field,
        bool allowParameterPlaceholder = false)
    {
        foreach (var argument in arguments)
        {
            foreach (Match match in PlaceholderRegex().Matches(argument))
            {
                if (supportedPlaceholders.Contains(match.Value) ||
                    (allowParameterPlaceholder && match.Value.StartsWith("{parameter:", StringComparison.Ordinal)))
                {
                    continue;
                }

                return $"{field}.Placeholder={match.Value}";
            }
        }

        return null;
    }

    private static string? ValidateRequiredPlaceholders(
        IReadOnlyList<string> arguments,
        string field,
        params string[] requiredPlaceholders)
    {
        return requiredPlaceholders.FirstOrDefault(required =>
            !arguments.Any(argument => argument.Contains(required, StringComparison.Ordinal))) is { } missing
            ? $"{field}.Required={missing}"
            : null;
    }

    private static T? Deserialize<T>(string filePath, JsonSerializerOptions options)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T>(stream, options);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private static bool HasIdentity(string id, string displayName, string version) =>
        !string.IsNullOrWhiteSpace(id) &&
        !string.IsNullOrWhiteSpace(displayName) &&
        !string.IsNullOrWhiteSpace(version);

    private static bool TryParseKind(string? value, out PluginPackageKindDto kind)
    {
        if (string.Equals(value, "assetBundleCodec", StringComparison.OrdinalIgnoreCase))
        {
            kind = PluginPackageKindDto.AssetBundleCodec;
            return true;
        }

        if (string.Equals(value, "resourceProcessor", StringComparison.OrdinalIgnoreCase))
        {
            kind = PluginPackageKindDto.ResourceProcessor;
            return true;
        }

        if (string.Equals(value, "postExport", StringComparison.OrdinalIgnoreCase))
        {
            kind = PluginPackageKindDto.PostExport;
            return true;
        }

        kind = default;
        return false;
    }

    private static bool HasProperty(JsonElement element, string name) =>
        element.EnumerateObject().Any(property =>
            string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetString(JsonElement element, string name, out string? value)
    {
        var property = element.EnumerateObject().FirstOrDefault(candidate =>
            string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
        value = property.Value.ValueKind == JsonValueKind.String
            ? property.Value.GetString()
            : null;
        return property.Value.ValueKind == JsonValueKind.String;
    }

    private static bool IsWithin(string path, string rootPath)
    {
        var root = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(path);
        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase) ||
               candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveCommandFromPath(string command)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var extensions = string.IsNullOrWhiteSpace(Path.GetExtension(command))
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT").Split(';')
            : [string.Empty];
        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory.Trim(), command + extension.Trim());
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static Result<PluginPackageDescriptor> Invalid(string detail) =>
        Result<PluginPackageDescriptor>.Failure(
            ApplicationErrors.PluginPackageManifestInvalid(detail));

    [GeneratedRegex("\\{(?:parameter:[^{}]+|[A-Za-z][A-Za-z0-9]*)\\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    private sealed record ManifestCandidate(string FilePath, PluginPackageKindDto Kind);
}

internal sealed record PluginPackageDescriptor(
    PluginPackageKindDto Kind,
    string Id,
    string DisplayName,
    string Version,
    string ManifestRelativePath);
