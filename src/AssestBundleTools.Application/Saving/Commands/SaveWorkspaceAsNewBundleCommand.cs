using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Saving.Commands;

/// <summary>
/// 请求按当前工作区的保存计划执行“另存为新包”骨架。
/// </summary>
public sealed record SaveWorkspaceAsNewBundleCommand(
    WorkspaceId WorkspaceId,
    string? OutputDirectory = null);
