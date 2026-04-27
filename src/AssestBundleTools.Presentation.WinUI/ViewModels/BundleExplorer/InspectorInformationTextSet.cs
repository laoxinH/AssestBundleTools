using System.Globalization;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer;

/// <summary>
/// 承载检查器信息卡所需的本地化标题与字段名。
/// </summary>
public sealed record InspectorInformationTextSet(
    string BasicInfoTitle,
    string PreviewInfoTitle,
    string PreviewDiagnosticTitle,
    string AssetNameLabel,
    string AssetTypeLabel,
    string BundleLabel,
    string AssetPathLabel,
    string ContainerPathLabel,
    string AssetSizeLabel,
    string PathIdLabel,
    string PreviewCacheStatusLabel,
    string PreviewFallbackReasonLabel,
    string PreviewDiagnosticDetailLabel,
    string CopyToolTipText,
    string ItemCountFormat)
{
    public static InspectorInformationTextSet CreateFallback()
    {
        return new InspectorInformationTextSet(
            BasicInfoTitle: string.Empty,
            PreviewInfoTitle: string.Empty,
            PreviewDiagnosticTitle: string.Empty,
            AssetNameLabel: string.Empty,
            AssetTypeLabel: string.Empty,
            BundleLabel: string.Empty,
            AssetPathLabel: string.Empty,
            ContainerPathLabel: string.Empty,
            AssetSizeLabel: string.Empty,
            PathIdLabel: string.Empty,
            PreviewCacheStatusLabel: string.Empty,
            PreviewFallbackReasonLabel: string.Empty,
            PreviewDiagnosticDetailLabel: string.Empty,
            CopyToolTipText: string.Empty,
            ItemCountFormat: "{0}");
    }

    public string FormatItemCount(int count)
    {
        return string.Format(CultureInfo.CurrentCulture, ItemCountFormat, count);
    }
}
