using System.IO;
using AssestBundleTools.Application.Bundles.Dtos;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

/// <summary>
/// 负责把 externals 中的原始引用路径解析成更适合界面展示的摘要。
/// </summary>
internal static class AssetsToolsBundleExternalDependencyDisplayResolver
{
    public static BundleExternalDependencyDto Resolve(string referencePath)
    {
        var normalizedReferencePath = NormalizeReferencePath(referencePath);
        if (TryResolveArchiveSerializedFileDisplayName(normalizedReferencePath, out var archiveDisplayName))
        {
            return new BundleExternalDependencyDto(
                ReferencePath: normalizedReferencePath,
                DisplayName: archiveDisplayName,
                Kind: BundleExternalDependencyKind.ArchiveSerializedFile,
                ReferenceDisplayName: archiveDisplayName);
        }

        var fileName = Path.GetFileName(normalizedReferencePath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return new BundleExternalDependencyDto(
                ReferencePath: normalizedReferencePath,
                DisplayName: fileName,
                Kind: BundleExternalDependencyKind.FilePath,
                ReferenceDisplayName: fileName);
        }

        return new BundleExternalDependencyDto(
            ReferencePath: normalizedReferencePath,
            DisplayName: normalizedReferencePath,
            Kind: BundleExternalDependencyKind.Unknown,
            ReferenceDisplayName: normalizedReferencePath);
    }

    /// <summary>
    /// 把外部依赖引用展开为可用于工作区内反查的候选键。
    /// 这里既保留原始规范化路径，也尽量提取 `CAB-*` / 文件名这类更稳定的短标识。
    /// </summary>
    public static IReadOnlyList<string> EnumerateLookupCandidates(string referencePath)
    {
        var normalizedReferencePath = NormalizeReferencePath(referencePath);
        if (string.IsNullOrWhiteSpace(normalizedReferencePath))
        {
            return Array.Empty<string>();
        }

        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalizedReferencePath
        };

        const string archivePrefix = "archive:/";
        if (normalizedReferencePath.StartsWith(archivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var segments = normalizedReferencePath[archivePrefix.Length..]
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var segment in segments)
            {
                if (!string.IsNullOrWhiteSpace(segment))
                {
                    candidates.Add(segment);
                }
            }
        }

        var fileName = Path.GetFileName(normalizedReferencePath);
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            candidates.Add(fileName);
        }

        return candidates.ToArray();
    }

    private static bool TryResolveArchiveSerializedFileDisplayName(
        string normalizedReferencePath,
        out string displayName)
    {
        const string archivePrefix = "archive:/";
        if (!normalizedReferencePath.StartsWith(archivePrefix, StringComparison.OrdinalIgnoreCase))
        {
            displayName = string.Empty;
            return false;
        }

        var segments = normalizedReferencePath[archivePrefix.Length..]
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            displayName = normalizedReferencePath;
            return true;
        }

        // Unity 的外部 serialized file 常见形态为 archive:/CAB-xxx/CAB-xxx，
        // 这里优先把真正的 CAB 标识提取出来，避免界面只展示冗长原始路径。
        displayName = segments[^1];
        return true;
    }

    private static string NormalizeReferencePath(string referencePath)
    {
        return string.IsNullOrWhiteSpace(referencePath)
            ? string.Empty
            : referencePath.Trim().Replace('\\', '/');
    }
}
