using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using AssestBundleTools.Presentation.WinUI.Diagnostics;

namespace AssestBundleTools.Presentation.WinUI.Localization;

/// <summary>
/// 基于 WinUI `Resources.resw` 的资源读取实现。
/// </summary>
public sealed class ResourceLocalizationService : ILocalizationService
{
    private readonly object _syncRoot = new();
    private readonly CultureInfo? _preferredCulture;
    private ResourceLoader? _resourceLoader;
    private IReadOnlyDictionary<string, string>? _fallbackResources;
    private bool _preferFallbackResources;

    public ResourceLocalizationService(string? preferredCultureName = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredCultureName))
        {
            _preferredCulture = CultureInfo.GetCultureInfo(preferredCultureName);
        }
    }

    public string GetRequiredString(string resourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceKey);

        var localizedValue = TryGetFromWinUiResourceLoader(resourceKey);

        if (!string.IsNullOrWhiteSpace(localizedValue))
        {
            return localizedValue;
        }

        localizedValue = TryGetFromFallbackResourceFiles(resourceKey);

        if (!string.IsNullOrWhiteSpace(localizedValue))
        {
            return localizedValue;
        }

        StartupDiagnostics.Log($"Missing required localization resource: {resourceKey}");
        throw new InvalidOperationException($"Missing required localization resource: {resourceKey}");
    }

    public string GetFormattedString(string resourceKey, params object[] arguments)
    {
        var format = GetRequiredString(resourceKey);
        return string.Format(_preferredCulture ?? CultureInfo.CurrentUICulture, format, arguments);
    }

    /// <summary>
    /// 优先使用 WinUI 标准资源加载器。
    /// 如果该路径在调试环境中触发 COM 异常，则自动切换到 `.resw` 文件回退方案。
    /// </summary>
    private string? TryGetFromWinUiResourceLoader(string resourceKey)
    {
        if (_preferFallbackResources)
        {
            return null;
        }

        // WinUI 在代码中读取分段资源键时，需要将点号转换为路径分隔符。
        var normalizedResourceKey = resourceKey.Replace('.', '/');

        try
        {
            var localizedValue = EnsureResourceLoader().GetString(normalizedResourceKey);

            if (!string.IsNullOrWhiteSpace(localizedValue))
            {
                return localizedValue;
            }

            return null;
        }
        catch (COMException ex)
        {
            _preferFallbackResources = true;
            StartupDiagnostics.Log($"WinUI ResourceLoader failed with COM exception and will switch to fallback resources. Key={resourceKey}; HResult=0x{ex.HResult:X8}; Exception={ex}");
            return null;
        }
        catch (Exception ex)
        {
            _preferFallbackResources = true;
            StartupDiagnostics.Log($"WinUI ResourceLoader failed unexpectedly and will switch to fallback resources. Key={resourceKey}; Exception={ex}");
            return null;
        }
    }

    private ResourceLoader EnsureResourceLoader()
    {
        lock (_syncRoot)
        {
            _resourceLoader ??= new ResourceLoader();
            return _resourceLoader;
        }
    }

    private string? TryGetFromFallbackResourceFiles(string resourceKey)
    {
        var resources = EnsureFallbackResources();
        return resources.TryGetValue(resourceKey, out var localizedValue)
            ? localizedValue
            : null;
    }

    private IReadOnlyDictionary<string, string> EnsureFallbackResources()
    {
        lock (_syncRoot)
        {
            _fallbackResources ??= LoadFallbackResources();
            return _fallbackResources;
        }
    }

    private IReadOnlyDictionary<string, string> LoadFallbackResources()
    {
        foreach (var cultureName in GetCultureProbeOrder())
        {
            var resourceFilePath = FindResourceFile(cultureName);

            if (resourceFilePath is null)
            {
                continue;
            }

            StartupDiagnostics.Log($"Using fallback resource file: {resourceFilePath}");
            return ParseResourceFile(resourceFilePath);
        }

        StartupDiagnostics.Log("No fallback resource file was found for the current UI culture.");
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> GetCultureProbeOrder()
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cultures = _preferredCulture is null
            ? new[] { CultureInfo.CurrentUICulture }
            : new[] { _preferredCulture!, CultureInfo.CurrentUICulture };

        foreach (var initialCulture in cultures)
        {
            var culture = initialCulture;
            while (!string.IsNullOrWhiteSpace(culture.Name))
            {
                if (yielded.Add(culture.Name))
                {
                    yield return culture.Name;
                }

                culture = culture.Parent;
            }
        }

        if (yielded.Add("zh-CN"))
        {
            yield return "zh-CN";
        }

        if (yielded.Add("en-US"))
        {
            yield return "en-US";
        }
    }

    private static string? FindResourceFile(string cultureName)
    {
        // 先尝试运行目录旁的 Strings 目录，再向上回溯项目目录，
        // 便于开发态直接从仓库资源文件回退读取。
        foreach (var stringsDirectory in EnumerateCandidateStringsDirectories())
        {
            var candidateFilePath = Path.Combine(stringsDirectory, cultureName, "Resources.resw");

            if (File.Exists(candidateFilePath))
            {
                return candidateFilePath;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateStringsDirectories()
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var directStringsPath = Path.Combine(AppContext.BaseDirectory, "Strings");
        if (Directory.Exists(directStringsPath) && yielded.Add(directStringsPath))
        {
            yield return directStringsPath;
        }

        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var stringsDirectory = Path.Combine(currentDirectory.FullName, "Strings");

            if (Directory.Exists(stringsDirectory) && yielded.Add(stringsDirectory))
            {
                yield return stringsDirectory;
            }

            currentDirectory = currentDirectory.Parent;
        }
    }

    private static IReadOnlyDictionary<string, string> ParseResourceFile(string resourceFilePath)
    {
        var document = XDocument.Load(resourceFilePath);

        return document
            .Root?
            .Elements("data")
            .Where(element => element.Attribute("name")?.Value is not null)
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
