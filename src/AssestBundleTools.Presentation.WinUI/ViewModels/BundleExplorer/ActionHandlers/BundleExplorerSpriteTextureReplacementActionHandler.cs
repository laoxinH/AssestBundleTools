using System.Diagnostics.CodeAnalysis;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;

/// <summary>
/// 通过 Sprite 编辑入口立即替换其关联 Texture2D。
/// </summary>
internal sealed class BundleExplorerSpriteTextureReplacementActionHandler
{
    private readonly BundleExplorerFileReplacementActionExecutor _fileReplacementActionExecutor;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;

    public BundleExplorerSpriteTextureReplacementActionHandler(
        BundleExplorerFileReplacementActionExecutor fileReplacementActionExecutor,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _fileReplacementActionExecutor = fileReplacementActionExecutor ?? throw new ArgumentNullException(nameof(fileReplacementActionExecutor));
        _appNotificationService = appNotificationService ?? throw new ArgumentNullException(nameof(appNotificationService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
    }

    public async Task SelectTextureReplacementSourceAsync(
        WorkspaceSummaryDto workspaceSummary,
        AssetDocumentTabViewModel assetTab,
        AssetDetailsDto details,
        Action<bool> setBusyState,
        Action<IReadOnlyList<AppError>> appendResultFailure)
    {
        ArgumentNullException.ThrowIfNull(workspaceSummary);
        ArgumentNullException.ThrowIfNull(assetTab);
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(setBusyState);
        ArgumentNullException.ThrowIfNull(appendResultFailure);

        if (!TryResolvePrimarySpriteTexture(details, out var textureDependency))
        {
            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationWarningTitle),
                GetString(ResourceKeys.BundleExplorerNotificationSpriteTextureNotFound),
                AppNotificationSeverity.Warning);
            return;
        }

        setBusyState(true);
        try
        {
            var candidate = await _fileReplacementActionExecutor.PickFileReplacementCandidateAsync(
                workspaceSummary,
                textureDependency.BundleId,
                textureDependency.AssetId,
                AssetType.Texture2D,
                textureDependency.Name,
                appendResultFailure);
            if (candidate is null)
            {
                return;
            }

            if (!await _fileReplacementActionExecutor.ApplyFileReplacementAsync(
                    workspaceSummary,
                    textureDependency.BundleId,
                    textureDependency.AssetId,
                    textureDependency.Name,
                    candidate,
                    appendResultFailure))
            {
                return;
            }
        }
        finally
        {
            setBusyState(false);
        }
    }

    private static bool TryResolvePrimarySpriteTexture(
        AssetDetailsDto details,
        [NotNullWhen(true)] out AssetDependencyDto? textureDependency)
    {
        textureDependency = details.Dependencies
            .Concat(details.TextureAssociations ?? Array.Empty<AssetDependencyDto>())
            .Where(static dependency => dependency.AssetType == AssetType.Texture2D)
            .GroupBy(static dependency => string.Concat(
                dependency.BundleId.Value,
                "\n",
                dependency.AssetId.Value), StringComparer.Ordinal)
            .Select(static group => group.First())
            .OrderBy(static dependency => dependency.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return textureDependency is not null;
    }

    private void ShowOperationNotification(string title, string message, AppNotificationSeverity severity)
    {
        _appNotificationService.Show(title, message, severity);
    }

    private string GetString(string resourceKey) => _localizationService.GetRequiredString(resourceKey);
}
