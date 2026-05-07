using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Queries;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.Services;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

internal sealed class AssetEditorWindowDraftRestorePlanCoordinator
{
    private readonly IAssetEditDraftRestorePlanGateway _draftRestorePlanGateway;
    private readonly IWorkspaceContextService _workspaceContextService;
    private readonly AssetEditorWindowRestorePromptViewModel _restorePrompt;

    public AssetEditorWindowDraftRestorePlanCoordinator(
        IAssetEditDraftRestorePlanGateway draftRestorePlanGateway,
        IWorkspaceContextService workspaceContextService,
        AssetEditorWindowRestorePromptViewModel restorePrompt)
    {
        _draftRestorePlanGateway = draftRestorePlanGateway
            ?? throw new ArgumentNullException(nameof(draftRestorePlanGateway));
        _workspaceContextService = workspaceContextService
            ?? throw new ArgumentNullException(nameof(workspaceContextService));
        _restorePrompt = restorePrompt ?? throw new ArgumentNullException(nameof(restorePrompt));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var draftReferences = CaptureDraftReferences(_restorePrompt.PendingWindows);
        if (draftReferences.Count == 0 ||
            !_workspaceContextService.TryGetCurrentWorkspaceEntry(out var workspaceEntry) ||
            workspaceEntry is null ||
            string.IsNullOrWhiteSpace(workspaceEntry.WorkspaceKey))
        {
            _restorePrompt.ClearDraftRestorePlan();
            return;
        }

        var result = await _draftRestorePlanGateway.GetRestorePlanAsync(
            new GetAssetEditDraftRestorePlanQuery(
                WorkspaceKey: workspaceEntry.WorkspaceKey,
                DraftReferences: draftReferences),
            cancellationToken);

        if (result.IsSuccess && result.Value is { } restorePlan)
        {
            _restorePrompt.ReplaceDraftRestorePlan(restorePlan);
            return;
        }

        _restorePrompt.ClearDraftRestorePlan();
    }

    private static IReadOnlyList<AssetEditDraftReferenceDto> CaptureDraftReferences(
        IReadOnlyList<WorkspaceAssetEditorWindowStateDto> pendingWindows)
    {
        var draftReferences = new List<AssetEditDraftReferenceDto>();
        foreach (var pendingWindow in pendingWindows)
        {
            foreach (var assetTab in pendingWindow.OpenAssetTabs)
            {
                if (assetTab.EditDraftReference is null)
                {
                    continue;
                }

                draftReferences.Add(AssetDocumentEditDraftReferenceMapper.ToRestorePlanDto(
                    assetTab.EditDraftReference));
            }
        }

        return draftReferences;
    }
}
