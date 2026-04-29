using AssestBundleTools.Presentation.WinUI.Localization;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;

/// <summary>
/// 统一构建检查器信息卡所需的本地化文案集合，
/// 避免 ViewModel 创建时机依赖视图 code-behind 再补标题。
/// </summary>
internal static class InspectorInformationTextSetFactory
{
    public static InspectorInformationTextSet Create(ILocalizationService localizationService)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        return new InspectorInformationTextSet(
            BasicInfoTitle: localizationService.GetRequiredString(ResourceKeys.InspectorBasicInfoTitle),
            PreviewInfoTitle: localizationService.GetRequiredString(ResourceKeys.InspectorPreviewInfoTitle),
            PreviewDiagnosticTitle: localizationService.GetRequiredString(ResourceKeys.InspectorPreviewDiagnosticTitle),
            AssetNameLabel: localizationService.GetRequiredString(ResourceKeys.InspectorAssetNameLabel),
            AssetTypeLabel: localizationService.GetRequiredString(ResourceKeys.InspectorAssetTypeLabel),
            BundleLabel: localizationService.GetRequiredString(ResourceKeys.InspectorBundleLabel),
            AssetPathLabel: localizationService.GetRequiredString(ResourceKeys.InspectorAssetPathLabel),
            ContainerPathLabel: localizationService.GetRequiredString(ResourceKeys.InspectorContainerPathLabel),
            AssetSizeLabel: localizationService.GetRequiredString(ResourceKeys.InspectorAssetSizeLabel),
            PathIdLabel: localizationService.GetRequiredString(ResourceKeys.InspectorPathIdLabel),
            PreviewCacheStatusLabel: localizationService.GetRequiredString(ResourceKeys.InspectorPreviewCacheStatusLabel),
            PreviewFallbackReasonLabel: localizationService.GetRequiredString(ResourceKeys.InspectorPreviewFallbackReasonLabel),
            PreviewDiagnosticDetailLabel: localizationService.GetRequiredString(ResourceKeys.InspectorPreviewDiagnosticDetailLabel),
            CopyToolTipText: localizationService.GetRequiredString(ResourceKeys.InspectorInfoCardCopyToolTip),
            ItemCountFormat: localizationService.GetRequiredString(ResourceKeys.InspectorInfoCardItemCountFormat));
    }
}
