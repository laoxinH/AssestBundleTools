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

    public async Task<string?> PickSpineFrameSequenceExportDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var picker = CreateFolderPicker();
        var selectedFolder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return selectedFolder?.Path;
    }

    public async Task<string?> PickSpineAssetGroupExtractionDirectoryAsync(CancellationToken cancellationToken = default)
    {
        var picker = CreateFolderPicker();
        var selectedFolder = await picker.PickSingleFolderAsync();
        cancellationToken.ThrowIfCancellationRequested();
        return selectedFolder?.Path;
    }

    public async Task<string?> PickAssetExtractionOutputFileAsync(
        AssetType assetType,
        string assetName,
        string? assetPath,
        string? containerPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ownerWindow = RequireOwnerWindow();
        var defaultFileName = ResolveSuggestedExtractionFileName(assetName, assetPath, containerPath);
        var extensions = ResolveExtractionFileExtensions(assetType, defaultFileName);
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
            SuggestedFileName = Path.GetFileNameWithoutExtension(defaultFileName),
            DefaultFileExtension = extensions[0]
        };

        picker.FileTypeChoices.Add(
            _localizationService.GetRequiredString(ResolveExtractionFileTypeResourceKey(assetType)),
            extensions.ToList());
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(ownerWindow));

        var selectedFile = await picker.PickSaveFileAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return selectedFile?.Path;
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

    private static IReadOnlyList<string> ResolveExtractionFileExtensions(
        AssetType assetType,
        string defaultFileName)
    {
        var extensions = assetType switch
        {
            AssetType.Texture2D or AssetType.Sprite => new[] { ".png" },
            AssetType.TextAsset => ResolveTextAssetFileExtensions(defaultFileName),
            AssetType.AudioClip => [".wav", ".ogg", ".mp3", ".flac", ".m4a", ".aac", ".bin"],
            AssetType.Mesh or AssetType.Material => [".json"],
            _ => [".bin"]
        };

        return extensions
            .Where(static extension => !string.IsNullOrWhiteSpace(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] ResolveTextAssetFileExtensions(string defaultFileName)
    {
        var extension = Path.GetExtension(defaultFileName);
        return string.IsNullOrWhiteSpace(extension)
            ? [".txt"]
            : [extension, ".txt"];
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

    private static string ResolveExtractionFileTypeResourceKey(AssetType assetType) =>
        assetType switch
        {
            AssetType.Texture2D or AssetType.Sprite => ResourceKeys.FilePickerExtractionTypeImage,
            AssetType.TextAsset => ResourceKeys.FilePickerExtractionTypeText,
            AssetType.AudioClip => ResourceKeys.FilePickerExtractionTypeAudio,
            AssetType.Mesh or AssetType.Material => ResourceKeys.FilePickerExtractionTypeJson,
            _ => ResourceKeys.FilePickerExtractionTypeBinary
        };

    /// <summary>
    /// 文件/目录选择器依赖已创建的宿主窗口句柄。
    /// 若当前窗口尚未就绪，直接抛出资源化异常，避免少量壳层英文继续散落。
    /// </summary>
    private Window RequireOwnerWindow()
    {
        return _ownerWindowAccessor()
               ?? throw PresentationExceptionFactory.CreateOwnerWindowUnavailableException(_localizationService);
    }
}
