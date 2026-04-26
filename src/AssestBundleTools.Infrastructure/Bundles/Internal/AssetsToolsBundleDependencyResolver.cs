using AssetsTools.NET.Extra;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 负责从已加载的序列化文件中整理资源包级依赖信息。
/// </summary>
internal static class AssetsToolsBundleDependencyResolver
{
    private static readonly HashSet<string> BuiltInDependencyNames =
    [
        "library/unity default resources",
        "resources/unity default resources",
        "resources/unity_builtin_extra",
        "builtin/default resources"
    ];

    public static IReadOnlyDictionary<string, string> BuildInternalSerializedFileLookup(
        IReadOnlyList<AssetsFileInstance> serializedFiles)
    {
        ArgumentNullException.ThrowIfNull(serializedFiles);

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var serializedFile in serializedFiles)
        {
            RegisterLookupCandidate(lookup, serializedFile.name, serializedFile.name);
        }

        return lookup;
    }

    public static IReadOnlyList<string> ResolveBundleDependencies(IReadOnlyList<AssetsFileInstance> serializedFiles)
    {
        ArgumentNullException.ThrowIfNull(serializedFiles);

        var internalSerializedFileLookup = BuildInternalSerializedFileLookup(serializedFiles);
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var serializedFile in serializedFiles)
        {
            foreach (var external in serializedFile.file.Metadata.Externals)
            {
                if (TryResolveExternalToInternalSerializedFileName(external, internalSerializedFileLookup, out _))
                {
                    continue;
                }

                var dependencyPath = SelectExternalPath(external);
                if (string.IsNullOrWhiteSpace(dependencyPath))
                {
                    continue;
                }

                var normalizedPath = NormalizePath(dependencyPath);
                if (BuiltInDependencyNames.Contains(normalizedPath))
                {
                    continue;
                }

                dependencies.Add(normalizedPath);
            }
        }

        return dependencies
            .OrderBy(static dependency => dependency, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool TryResolveTargetSerializedFileName(
        AssetsFileInstance currentSerializedFile,
        long fileId,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out string targetSerializedFileName)
    {
        ArgumentNullException.ThrowIfNull(currentSerializedFile);
        ArgumentNullException.ThrowIfNull(internalSerializedFileLookup);

        if (fileId == 0)
        {
            targetSerializedFileName = currentSerializedFile.name;
            return true;
        }

        var externalIndex = checked((int)fileId) - 1;
        var externals = currentSerializedFile.file.Metadata.Externals;
        if (externalIndex < 0 || externalIndex >= externals.Count)
        {
            targetSerializedFileName = string.Empty;
            return false;
        }

        return TryResolveExternalToInternalSerializedFileName(
            externals[externalIndex],
            internalSerializedFileLookup,
            out targetSerializedFileName);
    }

    private static bool TryResolveExternalToInternalSerializedFileName(
        AssetsTools.NET.AssetsFileExternal external,
        IReadOnlyDictionary<string, string> internalSerializedFileLookup,
        out string targetSerializedFileName)
    {
        foreach (var candidate in EnumerateExternalCandidates(external))
        {
            if (internalSerializedFileLookup.TryGetValue(candidate, out var resolvedSerializedFileName) &&
                !string.IsNullOrWhiteSpace(resolvedSerializedFileName))
            {
                targetSerializedFileName = resolvedSerializedFileName;
                return true;
            }
        }

        targetSerializedFileName = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateExternalCandidates(AssetsTools.NET.AssetsFileExternal external)
    {
        foreach (var candidate in new[]
                 {
                     external.PathName,
                     external.OriginalPathName,
                     external.VirtualAssetPathName
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var normalized = NormalizePath(candidate);
            yield return normalized;

            var fileName = Path.GetFileName(normalized);
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                yield return fileName;
            }
        }
    }

    private static string? SelectExternalPath(AssetsTools.NET.AssetsFileExternal external)
    {
        return new[]
            {
                external.PathName,
                external.OriginalPathName,
                external.VirtualAssetPathName
            }
            .FirstOrDefault(static candidate => !string.IsNullOrWhiteSpace(candidate));
    }

    private static void RegisterLookupCandidate(
        IDictionary<string, string> lookup,
        string? candidate,
        string serializedFileName)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        var normalized = NormalizePath(candidate);
        lookup[normalized] = serializedFileName;

        var fileName = Path.GetFileName(normalized);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            lookup[fileName] = serializedFileName;
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Trim().Replace('\\', '/');
    }
}
