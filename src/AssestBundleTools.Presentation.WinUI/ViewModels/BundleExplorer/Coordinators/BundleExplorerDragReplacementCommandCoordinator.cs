using AssestBundleTools.Application.Replacement.Dtos;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

/// <summary>
/// 负责行级拖入替换的工作区前置条件和忙碌状态编排。
/// </summary>
internal sealed class BundleExplorerDragReplacementCommandCoordinator
{
    private readonly IBundleExplorerWorkspaceShellHost _workspaceShell;
    private readonly BundleExplorerDragReplacementActionHandler _actionHandler;
    private readonly BundleExplorerOperationFeedbackBridge _operationFeedbackBridge;
    private readonly Func<bool> _isBusyProvider;
    private readonly Action<bool> _setBusyState;

    public BundleExplorerDragReplacementCommandCoordinator(
        IBundleExplorerWorkspaceShellHost workspaceShell,
        BundleExplorerDragReplacementActionHandler actionHandler,
        BundleExplorerOperationFeedbackBridge operationFeedbackBridge,
        Func<bool> isBusyProvider,
        Action<bool> setBusyState)
    {
        _workspaceShell = workspaceShell ?? throw new ArgumentNullException(nameof(workspaceShell));
        _actionHandler = actionHandler ?? throw new ArgumentNullException(nameof(actionHandler));
        _operationFeedbackBridge = operationFeedbackBridge ?? throw new ArgumentNullException(nameof(operationFeedbackBridge));
        _isBusyProvider = isBusyProvider ?? throw new ArgumentNullException(nameof(isBusyProvider));
        _setBusyState = setBusyState ?? throw new ArgumentNullException(nameof(setBusyState));
    }

    public Task<FileReplacementCandidateDto?> PrepareAssetAsync(
        AssetListEntryViewModel target,
        string sourceFilePath)
    {
        ArgumentNullException.ThrowIfNull(target);
        return !TryGetWorkspace(out var workspaceSummary)
            ? Task.FromResult<FileReplacementCandidateDto?>(null)
            : _actionHandler.PrepareAssetAsync(
                workspaceSummary,
                target,
                sourceFilePath,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure);
    }

    public Task<bool> ApplyAssetAsync(
        AssetListEntryViewModel target,
        FileReplacementCandidateDto candidate)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidate);
        return !TryGetWorkspace(out var workspaceSummary)
            ? Task.FromResult(false)
            : _actionHandler.ApplyAssetAsync(
                workspaceSummary,
                target,
                candidate,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure);
    }

    public SpineAssetGroupEditDraftViewModel? PrepareSpineGroup(
        SpineAssetGroupEntryViewModel target,
        string sourceDirectoryPath) =>
        _isBusyProvider()
            ? null
            : _actionHandler.PrepareSpineGroup(target, sourceDirectoryPath);

    public Task<bool> ApplySpineGroupAsync(SpineAssetGroupEditDraftViewModel draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return !TryGetWorkspace(out var workspaceSummary)
            ? Task.FromResult(false)
            : _actionHandler.ApplySpineGroupAsync(
                workspaceSummary,
                draft,
                _setBusyState,
                _operationFeedbackBridge.AppendResultFailure);
    }

    private bool TryGetWorkspace(
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)]
        out WorkspaceSummaryDto? workspaceSummary)
    {
        workspaceSummary = _workspaceShell.WorkspaceSummary;
        return !_isBusyProvider() && workspaceSummary is not null;
    }
}
