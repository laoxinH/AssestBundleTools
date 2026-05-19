using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Services;

/// <summary>
/// 为保存 / 重建计划提供目标输出路径预检端口。
/// </summary>
/// <remarks>
/// Application 只消费结构化结果，不直接访问文件系统；具体探测实现下沉到 Infrastructure。
/// </remarks>
public interface IWorkspaceSaveOutputProbe
{
    WorkspaceSaveOutputProbeResult Probe(WorkspaceSaveOutputProbeRequest request);
}

public sealed record WorkspaceSaveOutputProbeRequest(string OutputFilePath);

public sealed record WorkspaceSaveOutputProbeResult(
    WorkspaceSaveOutputReadiness Readiness,
    bool IsWriteBlocked,
    string? DiagnosticCode = null)
{
    public static WorkspaceSaveOutputProbeResult Ready() =>
        new(WorkspaceSaveOutputReadiness.Ready, IsWriteBlocked: false);

    public static WorkspaceSaveOutputProbeResult Blocked(
        WorkspaceSaveOutputReadiness readiness,
        string diagnosticCode) =>
        new(readiness, IsWriteBlocked: true, DiagnosticCode: diagnosticCode);
}
