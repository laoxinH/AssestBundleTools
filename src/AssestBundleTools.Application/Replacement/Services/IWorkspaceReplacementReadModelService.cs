using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Replacement.Dtos;

namespace AssestBundleTools.Application.Replacement.Services;

/// <summary>
/// 为后续统一导出 / 保存阶段提供从操作记录物化替换数据的稳定入口。
/// </summary>
public interface IWorkspaceReplacementReadModelService
{
    Result<WorkspaceReplacementReadModelDto> GetEffectiveReplacements(
        string workspaceKey,
        string workspaceDataRootPath);
}
