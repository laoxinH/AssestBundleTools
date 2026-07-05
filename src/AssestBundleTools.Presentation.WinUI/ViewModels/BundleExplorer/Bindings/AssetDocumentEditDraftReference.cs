namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

/// <summary>
/// 资产文档持有的轻量编辑草稿引用。
/// </summary>
/// <remarks>
/// 真实草稿内容仍应由后续编辑用例管理；资产文档只保留可恢复、可提示的引用标识。
/// </remarks>
public sealed record AssetDocumentEditDraftReference(
    string DraftId,
    string DraftMode,
    string? SourceFingerprint = null,
    string? SourceDescription = null,
    bool IsAppliedBaseline = false,
    int? DraftRevision = null);

