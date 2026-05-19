namespace AssestBundleTools.Application.Extraction.Services;

/// <summary>
/// 解析工作区级批量提取的跨资源包并发度。
/// </summary>
internal sealed class WorkspaceBatchExtractionConcurrencyPolicy
{
    private const int MaximumWorkerCount = 4;
    private const double MemoryPressureThresholdRatio = 0.85;

    private readonly Func<int> _processorCountProvider;
    private readonly Func<bool> _memoryPressureEvaluator;

    public WorkspaceBatchExtractionConcurrencyPolicy(
        Func<int>? processorCountProvider = null,
        Func<bool>? memoryPressureEvaluator = null)
    {
        _processorCountProvider = processorCountProvider ?? (() => Environment.ProcessorCount);
        _memoryPressureEvaluator = memoryPressureEvaluator ?? IsProcessUnderMemoryPressure;
    }

    public int ResolveMaximumWorkerCount(int bundleGroupCount)
    {
        if (bundleGroupCount <= 1)
        {
            return 1;
        }

        return Math.Min(bundleGroupCount, ResolveBaseWorkerCount(_processorCountProvider()));
    }

    public int ResolveCurrentWorkerLimit(int bundleGroupCount)
    {
        var maximumWorkerCount = ResolveMaximumWorkerCount(bundleGroupCount);
        if (maximumWorkerCount <= 1)
        {
            return maximumWorkerCount;
        }

        return IsUnderMemoryPressure() ? 1 : maximumWorkerCount;
    }

    public static int ResolveBaseWorkerCount(int processorCount)
    {
        if (processorCount <= 1)
        {
            return 1;
        }

        return Math.Clamp(processorCount / 2, 1, MaximumWorkerCount);
    }

    private bool IsUnderMemoryPressure()
    {
        try
        {
            return _memoryPressureEvaluator();
        }
        catch
        {
            return false;
        }
    }

    private static bool IsProcessUnderMemoryPressure()
    {
        var memoryInfo = GC.GetGCMemoryInfo();
        if (memoryInfo.HighMemoryLoadThresholdBytes <= 0)
        {
            return false;
        }

        return memoryInfo.MemoryLoadBytes >=
               memoryInfo.HighMemoryLoadThresholdBytes * MemoryPressureThresholdRatio;
    }
}
