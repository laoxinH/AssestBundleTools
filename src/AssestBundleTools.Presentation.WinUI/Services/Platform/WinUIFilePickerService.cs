using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 基于 WinUI 3 桌面文件选择器的实现。
/// </summary>
public sealed class WinUIFilePickerService : IFilePickerService
{
    private readonly Func<Window?> _ownerWindowAccessor;
    private readonly ILocalizationService _localizationService;

    public WinUIFilePickerService(
        Func<Window?> ownerWindowAccessor,
        ILocalizationService localizationService)
    {
        _ownerWindowAccessor = ownerWindowAccessor ?? throw new ArgumentNullException(nameof(ownerWindowAccessor));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task<string?> PickWorkspaceDataDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var picker = CreateFolderPicker();
        var selectedFolder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return selectedFolder?.Path;
    }

    public async Task<IReadOnlyList<string>> PickBundleFilePathsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ownerWindow = RequireOwnerWindow();

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        // 当前阶段仍处于导入骨架期，因此先接受常见资源包扩展名并保留“所有文件”兜底，
        // 后续真实 AB 解析接入时再把过滤规则收敛到正式支持矩阵。
        picker.FileTypeFilter.Add(".ab");
        picker.FileTypeFilter.Add(".bundle");
        picker.FileTypeFilter.Add(".assetbundle");
        picker.FileTypeFilter.Add(".unity3d");
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var selectedFiles = await picker.PickMultipleFilesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return selectedFiles
            .Select(static file => file.Path)
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<string?> PickBundleImportDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var picker = CreateFolderPicker();
        var selectedFolder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return selectedFolder?.Path;
    }

    public Task<string?> PickSpineFrameSequenceExportDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PickExportDirectory(cancellationToken));
    }

    public Task<string?> PickSpineAssetGroupExtractionDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PickExportDirectory(cancellationToken));
    }

    public async Task<string?> PickAssetExtractionOutputFileAsync(
        AssetType assetType,
        string? assetTypeDisplayName,
        string assetName,
        string? assetPath,
        string? containerPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ownerWindow = RequireOwnerWindow();
        var defaultFileName = ResolveSuggestedExtractionFileName(assetName, assetPath, containerPath);
        var typeChoices = ResolveExtractionFileTypeChoices(assetType, assetTypeDisplayName, defaultFileName);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = ResolvePickerSuggestedFileName(assetTypeDisplayName, defaultFileName),
            DefaultFileExtension = typeChoices[0].Extensions[0]
        };

        foreach (var typeChoice in typeChoices)
        {
            picker.FileTypeChoices.Add(
                _localizationService.GetRequiredString(typeChoice.ResourceKey),
                typeChoice.Extensions.ToList());
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var selectedFile = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return selectedFile?.Path;
    }

    public Task<string?> PickAssetBatchExtractionOutputDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PickExportDirectory(cancellationToken));
    }

    public async Task<string?> PickReplacementSourceFileAsync(
        AssetType targetAssetType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ownerWindow = RequireOwnerWindow();

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads
        };

        foreach (var extension in ReplacementFileTypeCatalog.GetCompatibleExtensions(targetAssetType))
        {
            picker.FileTypeFilter.Add(extension);
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var selectedFile = await picker.PickSingleFileAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return selectedFile?.Path;
    }

    public Task<string?> PickSpineReplacementSourceDirectoryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(PickExportDirectory(cancellationToken));
    }

    private FolderPicker CreateFolderPicker()
    {
        var ownerWindow = RequireOwnerWindow();

        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };

        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));
        return picker;
    }

    private string? PickExportDirectory(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ownerWindow = RequireOwnerWindow();
        var selectedDirectory = NativeFolderPicker.PickSingleFolder(WindowNative.GetWindowHandle(ownerWindow));
        cancellationToken.ThrowIfCancellationRequested();
        return selectedDirectory;
    }

    private static string ResolveSuggestedExtractionFileName(
        string assetName,
        string? assetPath,
        string? containerPath)
    {
        var fileName = ResolveFileNameFromPath(assetPath) ??
                       ResolveFileNameFromPath(containerPath) ??
                       assetName.Trim();
        return SanitizeSuggestedFileName(fileName);
    }

    private static string? ResolveFileNameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalizedPath = path.Trim().Replace('\\', '/');
        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        var fileName = lastSeparatorIndex >= 0
            ? normalizedPath[(lastSeparatorIndex + 1)..]
            : normalizedPath;
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

    internal static IReadOnlyList<ExtractionFileTypeChoice> ResolveExtractionFileTypeChoices(
        AssetType assetType,
        string? assetTypeDisplayName,
        string defaultFileName)
    {
        var normalizedTypeName = NormalizeAssetTypeDisplayName(assetTypeDisplayName);
        var typeChoices = assetType switch
        {
            AssetType.Texture2D or AssetType.Sprite => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeImage, ".png")],
            AssetType.TextAsset => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeText, ResolveTextAssetFileExtensions(defaultFileName))],
            AssetType.AudioClip => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeAudio, ".wav", ".ogg", ".mp3", ".flac", ".m4a", ".aac", ".bin")],
            AssetType.Mesh => CreateMeshTypeChoices(),
            AssetType.Material => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeJson, ".json")],
            _ when IsUnityTypeName(normalizedTypeName, "mesh") => CreateMeshTypeChoices(),
            _ when IsTextLikeAssetType(normalizedTypeName) => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeText, ResolveTextLikeAssetFileExtensions(normalizedTypeName, defaultFileName))],
            _ when IsMonoBehaviourAssetType(normalizedTypeName) => CreateMonoBehaviourTypeChoices(),
            _ when IsStructuredJsonAssetType(normalizedTypeName) => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeJson, ".json")],
            _ => [CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeBinary, ".bin")]
        };

        return typeChoices
            .Where(static choice => choice.Extensions.Count > 0)
            .ToArray();
    }

    private static string[] ResolveTextAssetFileExtensions(string defaultFileName)
    {
        return ResolveTextFileExtensions(defaultFileName, ".txt");
    }

    private static string[] ResolveTextLikeAssetFileExtensions(string normalizedTypeName, string defaultFileName)
    {
        if (IsUnityTypeName(normalizedTypeName, "shader"))
        {
            return ResolveTextFileExtensions(defaultFileName, ".shader");
        }

        if (IsUnityTypeName(normalizedTypeName, "monoscript"))
        {
            return ResolveTextFileExtensions(defaultFileName, ".cs");
        }

        return ResolveTextFileExtensions(defaultFileName, ".txt");
    }

    private static string[] ResolveTextFileExtensions(string defaultFileName, string fallbackExtension)
    {
        var extension = Path.GetExtension(defaultFileName);
        return string.IsNullOrWhiteSpace(extension)
            ? [fallbackExtension, ".txt"]
            : [extension, ".txt"];
    }

    internal static string ResolvePickerSuggestedFileName(
        string? assetTypeDisplayName,
        string defaultFileName)
    {
        var suggestedFileName = Path.GetFileNameWithoutExtension(defaultFileName);
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            return "asset";
        }

        var normalizedTypeName = NormalizeAssetTypeDisplayName(assetTypeDisplayName);
        if (!IsMonoBehaviourAssetType(normalizedTypeName))
        {
            return suggestedFileName;
        }

        return HasMonoBehaviourExportFormatSuffix(suggestedFileName)
            ? suggestedFileName
            : $"{suggestedFileName}.typetree";
    }

    private static string SanitizeSuggestedFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Trim()
            .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrWhiteSpace(sanitized)
            ? "asset"
            : sanitized;
    }

    private static string NormalizeAssetTypeDisplayName(string? assetTypeDisplayName) =>
        string.IsNullOrWhiteSpace(assetTypeDisplayName)
            ? string.Empty
            : assetTypeDisplayName.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();

    private static bool IsTextLikeAssetType(string normalizedTypeName) =>
        IsUnityTypeName(normalizedTypeName, "shader") ||
        IsUnityTypeName(normalizedTypeName, "monoscript");

    private static bool IsStructuredJsonAssetType(string normalizedTypeName) =>
        IsUnityTypeName(normalizedTypeName, "material") ||
        IsUnityTypeName(normalizedTypeName, "animationclip") ||
        IsUnityTypeName(normalizedTypeName, "font") ||
        IsUnityTypeName(normalizedTypeName, "gameobject") ||
        IsMonoBehaviourAssetType(normalizedTypeName) ||
        IsUnityTypeName(normalizedTypeName, "animatorcontroller");

    private static bool IsMonoBehaviourAssetType(string normalizedTypeName) =>
        IsUnityTypeName(normalizedTypeName, "monobehaviour") ||
        IsUnityTypeName(normalizedTypeName, "monobehavior");

    private static bool IsUnityTypeName(string normalizedTypeName, string expectedTypeName) =>
        string.Equals(normalizedTypeName, expectedTypeName, StringComparison.Ordinal) ||
        normalizedTypeName.EndsWith($".{expectedTypeName}", StringComparison.Ordinal);

    private static bool HasMonoBehaviourExportFormatSuffix(string fileNameWithoutExtension) =>
        fileNameWithoutExtension.EndsWith(".typetree", StringComparison.OrdinalIgnoreCase) ||
        fileNameWithoutExtension.EndsWith(".mono-behaviour", StringComparison.OrdinalIgnoreCase) ||
        fileNameWithoutExtension.EndsWith(".summary", StringComparison.OrdinalIgnoreCase) ||
        fileNameWithoutExtension.EndsWith(".fields", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<ExtractionFileTypeChoice> CreateMeshTypeChoices() =>
    [
        CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeObj, ".obj"),
        CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeFbx, ".fbx")
    ];

    private static IReadOnlyList<ExtractionFileTypeChoice> CreateMonoBehaviourTypeChoices() =>
    [
        CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeMonoBehaviourTypeTreeJson, ".json"),
        CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeYaml, ".yaml", ".yml"),
        CreateTypeChoice(ResourceKeys.FilePickerExtractionTypeMonoBehaviourFields, ".txt")
    ];

    private static ExtractionFileTypeChoice CreateTypeChoice(string resourceKey, params string[] extensions) =>
        new(
            resourceKey,
            extensions
                .Where(static extension => !string.IsNullOrWhiteSpace(extension))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

    /// <summary>
    /// 文件/目录选择器依赖已创建的宿主窗口句柄。
    /// 若当前窗口尚未就绪，直接抛出资源化异常，避免少量壳层英文继续散落。
    /// </summary>
    private Window RequireOwnerWindow()
    {
        return _ownerWindowAccessor()
               ?? throw PresentationExceptionFactory.CreateOwnerWindowUnavailableException(_localizationService);
    }

    internal sealed record ExtractionFileTypeChoice(
        string ResourceKey,
        IReadOnlyList<string> Extensions);
}
