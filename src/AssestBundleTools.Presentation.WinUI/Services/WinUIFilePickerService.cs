using AssestBundleTools.Application.Replacement.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.Localization;
using Microsoft.UI.Xaml;
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
