namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Spine;

internal static class SpineFixedFrameSamplingPolicy
{
    public static IReadOnlyList<double> CreateSampleTimes(
        double duration,
        double framesPerSecond,
        double startTime,
        double? endTime,
        bool includeFinalFrame)
    {
        if (duration <= 0d ||
            framesPerSecond <= 0d ||
            double.IsNaN(framesPerSecond) ||
            double.IsInfinity(framesPerSecond))
        {
            return [0d];
        }

        var start = ClampTime(startTime, duration);
        var end = ClampTime(endTime ?? duration, duration);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        if (Math.Abs(end - start) < double.Epsilon)
        {
            return [start];
        }

        var step = 1d / framesPerSecond;
        var sampleTimes = new List<double>();
        for (var time = start; time < end; time += step)
        {
            sampleTimes.Add(time);
        }

        if (includeFinalFrame &&
            !IsSameTime(sampleTimes[^1], end))
        {
            sampleTimes.Add(end);
        }

        return sampleTimes;
    }

    private static double ClampTime(double value, double duration)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value))
        {
            return 0d;
        }

        return Math.Clamp(value, 0d, duration);
    }

    private static bool IsSameTime(double left, double right) =>
        Math.Abs(left - right) <= 0.000001d;
}
