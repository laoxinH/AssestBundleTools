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
/// 选择 Sprite 关联 Texture2D 的待保存替换来源。
/// </summary>
internal sealed class BundleExplorerSpriteTextureReplacementActionHandler
{
    private readonly BundleExplorerFileReplacementActionExecutor _fileReplacementActionExecutor;
    private readonly IAppLogService _appLogService;
    private readonly IAppNotificationService _appNotificationService;
    private readonly ILocalizationService _localizationService;

    public BundleExplorerSpriteTextureReplacementActionHandler(
        BundleExplorerFileReplacementActionExecutor fileReplacementActionExecutor,
        IAppLogService appLogService,
        IAppNotificationService appNotificationService,
        ILocalizationService localizationService)
    {
        _fileReplacementActionExecutor = fileReplacementActionExecutor ?? throw new ArgumentNullException(nameof(fileReplacementActionExecutor));
        _appLogService = appLogService ?? throw new ArgumentNullException(nameof(appLogService));
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

            assetTab.UpdatePendingAssociatedFileReplacementCandidate(
                CreatePendingCandidate(textureDependency, candidate));

            _appLogService.AppendInfo(_localizationService.GetFormattedString(
                ResourceKeys.BundleExplorerLogSpriteTextureReplacementSourceSelected,
                candidate.SourceFileName,
                textureDependency.Name,
                details.Name));

            ShowOperationNotification(
                GetString(ResourceKeys.AppNotificationInfoTitle),
                _localizationService.GetFormattedString(
                    ResourceKeys.BundleExplorerNotificationSpriteTextureReplacementSourceSelected,
                    candidate.SourceFileName),
                AppNotificationSeverity.Informational);
        }
        finally
        {
            setBusyState(false);
        }
    }

    private static AssociatedAssetFileReplacementCandidate CreatePendingCandidate(
        AssetDependencyDto textureDependency,
        FileReplacementCandidateDto candidate) =>
        new(
            textureDependency.BundleId,
            textureDependency.AssetId,
            textureDependency.Name,
            AssetType.Texture2D,
            candidate);

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
