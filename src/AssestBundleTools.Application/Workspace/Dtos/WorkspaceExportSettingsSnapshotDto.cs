using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示当前工作区导出窗口需要恢复的轻量设置。
/// </summary>
public sealed record WorkspaceExportSettingsSnapshotDto(
    bool ExportToWorkspaceDirectory = false,
    bool OverwriteExistingFiles = false,
    WorkspaceBundleExportCompressionModeDto CompressionMode = WorkspaceBundleExportCompressionModeDto.PreserveOriginal,
    WorkspaceBundleExportAudioWriteModeDto AudioWriteMode = WorkspaceBundleExportAudioWriteModeDto.MatchOriginalContainer,
    IReadOnlyList<WorkspaceExportProcessorSettingSnapshotDto>? ProcessorSettings = null,
    bool UseSourceCodec = true,
    string? PostExportPluginId = null);

/// <summary>
/// 表示单个资源类型槽的导出处理器恢复设置。
/// </summary>
public sealed record WorkspaceExportProcessorSettingSnapshotDto(
    string ResourceKind,
    bool IsEnabled,
    string? ProcessorId);
