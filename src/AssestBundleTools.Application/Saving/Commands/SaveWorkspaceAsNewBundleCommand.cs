using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Saving.Commands;

/// <summary>
/// 请求按当前工作区的保存计划执行“另存为新包”。
/// </summary>
public sealed record SaveWorkspaceAsNewBundleCommand(
    WorkspaceId WorkspaceId,
    string? OutputDirectory = null,
    string? WorkspaceKey = null,
    string? WorkspaceDataRootPath = null,
    IReadOnlyList<BundleId>? SelectedBundleIds = null,
    IReadOnlyList<WorkspaceExportProcessorSelectionDto>? ProcessorSelections = null,
    IProgress<WorkspaceSaveExecutionProgressDto>? Progress = null,
    int? BundleWriteConcurrency = null,
    WorkspaceBundleExportCompressionModeDto CompressionMode = WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
    WorkspaceBundleExportOutputConflictPolicyDto OutputConflictPolicy = WorkspaceBundleExportOutputConflictPolicyDto.ReplaceExisting,
    WorkspaceBundleExportAudioWriteModeDto AudioWriteMode = WorkspaceBundleExportAudioWriteModeDto.MatchOriginalContainer);
