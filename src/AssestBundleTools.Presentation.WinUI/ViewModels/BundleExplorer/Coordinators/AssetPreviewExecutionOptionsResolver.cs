namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 解析资产预览后台查询的有界并发度。
/// </summary>
internal sealed class AssetPreviewExecutionOptionsResolver
{
    private const int MaxPreviewWorkerCount = 4;

    public int ResolvePreviewWorkerCount()
    {
        return ResolvePreviewWorkerCount(Environment.ProcessorCount);
    }

    public static int ResolvePreviewWorkerCount(int processorCount)
    {
        var halfProcessorCount = processorCount / 2;
        return Math.Clamp(halfProcessorCount, 1, MaxPreviewWorkerCount);
    }

    public static int NormalizePreviewWorkerCount(int? previewWorkerCount)
    {
        return previewWorkerCount is > 0
            ? Math.Clamp(previewWorkerCount.Value, 1, MaxPreviewWorkerCount)
            : new AssetPreviewExecutionOptionsResolver().ResolvePreviewWorkerCount();
    }
}
