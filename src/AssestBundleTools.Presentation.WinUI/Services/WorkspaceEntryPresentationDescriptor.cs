namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 表示工作区目录项在页面中的展示摘要。
/// </summary>
public sealed record WorkspaceEntryPresentationDescriptor(
    string PrimaryBadgeText,
    string? SecondaryBadgeText,
    string? AvailabilitySummary,
    bool CanOpen);
