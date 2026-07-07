namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示某个工作区自己的恢复元数据。
/// </summary>
/// <remarks>
/// 这里保存的是“下次打开工作区时希望恢复什么”的偏好，
/// 而不是工作区会话里的真实资源数据。
/// 真实 AB 解析索引、预览派生内容和修改暂存应分别落到独立缓存层与暂存层，
/// 避免元数据文件随着资源规模不断膨胀。
/// </remarks>
public sealed record WorkspaceMetadataDto(
    string WorkspaceKey,
    string DisplayName,
    string SourcePath,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastOpenedAtUtc,
    string? LastBundleId,
    string? LastAssetId,
    string? LastOutputDirectory,
    string BundleSearchKeyword,
    string AssetSearchKeyword,
    string? AssetTypeFilter,
    bool ShowModifiedOnly,
    bool ShowPreviewableOnly,
    IReadOnlyList<WorkspaceMetadataBundleTabDto> OpenBundleTabs,
    string? ActiveBundleTabId,
    IReadOnlyList<WorkspaceImportedSourceDto>? ImportedSources = null,
    string? AssetBundleDecryptorId = null,
    IReadOnlyList<string>? ExcludedBundleSourcePaths = null,
    IReadOnlyList<WorkspaceAssetEditorWindowStateDto>? OpenAssetEditorWindows = null,
    WorkspaceExportSettingsSnapshotDto? ExportSettings = null);
