namespace AssestBundleTools.Application.Editing.Services;

public sealed record AssetEditDraftLookupKey(
    string WorkspaceKey,
    string DraftId,
    string DraftMode);
