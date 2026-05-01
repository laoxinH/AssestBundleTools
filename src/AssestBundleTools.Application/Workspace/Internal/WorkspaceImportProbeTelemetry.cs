namespace AssestBundleTools.Application.Workspace.Internal;

internal readonly record struct WorkspaceImportProbeTelemetry(
    long DurationMs,
    int DecryptorCount,
    int FileEvaluationCount)
{
    public static WorkspaceImportProbeTelemetry None => new(
        DurationMs: 0,
        DecryptorCount: 0,
        FileEvaluationCount: 0);

    public WorkspaceImportProbeTelemetry Add(
        long durationMs,
        int decryptorCount,
        int fileEvaluationCount)
    {
        return new WorkspaceImportProbeTelemetry(
            DurationMs + Math.Max(0, durationMs),
            DecryptorCount + Math.Max(0, decryptorCount),
            FileEvaluationCount + Math.Max(0, fileEvaluationCount));
    }
}
