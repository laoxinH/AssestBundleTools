using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;

namespace AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;

public sealed partial class AssetEditorWindowViewModel
{
    public void OpenCurrentPreviewLocation() => _assetActionCoordinator.OpenCurrentPreviewLocation();

    public Task ExtractSelectedAssetAsync() => _assetActionCoordinator.ExtractSelectedAssetAsync();

    public Task SelectFileReplacementSourceAsync() =>
        _assetActionCoordinator.SelectFileReplacementSourceAsync();

    public Task ModifyFromFileReplacementSourceAsync()
    {
        if (RequestSpriteEditModeForActiveAsset())
        {
            return Task.CompletedTask;
        }

        if (RequestSpineEditModeForActiveGroup())
        {
            return Task.CompletedTask;
        }

        return _assetActionCoordinator.ModifyFromFileReplacementSourceAsync();
    }

    public Task SelectSpriteTextureReplacementSourceAsync() =>
        _assetActionCoordinator.SelectSpriteTextureReplacementSourceAsync();

    public Task ApplyPendingReplacementAsync() => _assetActionCoordinator.ApplyPendingReplacementAsync();

    public async Task<bool> ApplySpriteEditAsync()
    {
        var applied = await _assetActionCoordinator.ApplySpriteEditAsync();
        if (applied)
        {
            DocumentGroup.ActiveAssetTab?.Inspector.Preview.Image.MarkSpriteEditSaved();
            NotifyActionBarStates();
        }

        return applied;
    }

    public void ExitSpriteEditModeForActiveAsset(bool discardChanges)
    {
        var assetTab = DocumentGroup.ActiveAssetTab;
        if (assetTab is null)
        {
            return;
        }

        if (discardChanges)
        {
            assetTab.Inspector.Preview.Image.DiscardSpriteEditChanges();
        }

        assetTab.CancelSpriteEditMode();
        NotifyActionBarStates();
    }

    public void UndoSpriteEditForActiveAsset()
    {
        DocumentGroup.ActiveAssetTab?.Inspector.Preview.Image.UndoSpriteEdit();
        NotifyActionBarStates();
    }

    public void RedoSpriteEditForActiveAsset()
    {
        DocumentGroup.ActiveAssetTab?.Inspector.Preview.Image.RedoSpriteEdit();
        NotifyActionBarStates();
    }

    public async Task SelectSpineEditPartFileAsync(SpineAssetGroupEditPartViewModel? part)
    {
        if (await _assetActionCoordinator.SelectSpineEditPartFileAsync(part))
        {
            await ReloadActiveSpineGroupDetailsAsync();
            NotifyActionBarStates();
        }
    }

    public async Task SelectSpineEditBatchSourceDirectoryAsync()
    {
        if (await _assetActionCoordinator.SelectSpineEditBatchSourceDirectoryAsync())
        {
            await ReloadActiveSpineGroupDetailsAsync();
            NotifyActionBarStates();
        }
    }

    public async Task<bool> ApplySpineEditAsync()
    {
        var applied = await _assetActionCoordinator.ApplySpineEditAsync();
        if (applied)
        {
            if (DocumentGroup.ActiveSpineGroupTab is { } spineGroupTab)
            {
                spineGroupTab.ExitSpineEditMode(discardChanges: false);
            }

            NotifyActionBarStates();
        }

        return applied;
    }

    public async Task<bool> RequestTextEditModeForActiveAssetAsync()
    {
        var assetTab = DocumentGroup.ActiveAssetTab;
        if (assetTab?.Details is null || !CanUseTextEditor(assetTab.Details))
        {
            return false;
        }

        if (assetTab.TextEditDraft is { HasUnsavedChanges: false, IsAppliedBaseline: true } &&
            !await TryRefreshAssetDocumentDetailsBeforeReopeningTextEditAsync(assetTab))
        {
            return false;
        }

        if (assetTab.TextEditDraft is { } existingDraft &&
            ShouldReopenTextEditDraft(assetTab.Details, existingDraft))
        {
            assetTab.ExitTextEditMode(clearDraftReference: false);
            ObserveActiveTextEditDraft();
        }

        if (assetTab.TextEditDraft is null)
        {
            var draft = await _assetActionCoordinator.OpenOrCreateTextAssetEditDraftAsync();
            if (draft is null)
            {
                return false;
            }

            assetTab.StartTextEditMode(draft);
            ObserveActiveTextEditDraft();
        }

        NotifyActionStates();
        NotifyActionBarStates();
        OnPropertyChanged(nameof(Inspector));
        return true;
    }

    public async Task<bool> ApplyTextEditAsync()
    {
        if (DocumentGroup.ActiveAssetTab?.TextEditDraft?.CanSave != true ||
            IsTextEditLoading ||
            IsBusy)
        {
            return false;
        }

        var ownsSavingState = !IsTextEditSaving;
        if (ownsSavingState)
        {
            SetTextEditSaving(true);
        }

        try
        {
            var applied = await _assetActionCoordinator.ApplyTextAssetEditDraftAsync();
            if (applied)
            {
                DocumentGroup.ActiveAssetTab?.ExitTextEditMode(clearDraftReference: false);
                ObserveActiveTextEditDraft();
                NotifyActionStates();
                NotifyActionBarStates();
            }

            return applied;
        }
        finally
        {
            if (ownsSavingState)
            {
                SetTextEditSaving(false);
            }
        }
    }

    internal bool TryBeginTextEditSaveOperation()
    {
        if (IsTextEditSaving || IsTextEditLoading || IsBusy)
        {
            return false;
        }

        SetTextEditSaving(true);
        return true;
    }

    internal void EndTextEditSaveOperation()
    {
        SetTextEditSaving(false);
    }

    public async Task<bool> ExitTextEditModeForActiveAssetAsync()
    {
        var assetTab = DocumentGroup.ActiveAssetTab;
        var draft = assetTab?.TextEditDraft;
        if (draft is null)
        {
            return true;
        }

        if (draft.IsAppliedBaseline)
        {
            assetTab?.ExitTextEditMode(clearDraftReference: false);
            ObserveActiveTextEditDraft();
            NotifyActionStates();
            NotifyActionBarStates();
            OnPropertyChanged(nameof(Inspector));
            return true;
        }

        var discarded = await _assetActionCoordinator.DiscardTextAssetEditDraftAsync();
        if (!discarded)
        {
            return false;
        }

        assetTab?.ExitTextEditMode(clearDraftReference: true);
        ObserveActiveTextEditDraft();
        NotifyActionStates();
        NotifyActionBarStates();
        OnPropertyChanged(nameof(Inspector));
        return true;
    }

    public async Task ExitSpineEditModeForActiveGroupAsync(bool discardChanges)
    {
        var spineGroupTab = DocumentGroup.ActiveSpineGroupTab;
        if (spineGroupTab is null)
        {
            return;
        }

        var shouldReloadPreview = spineGroupTab.ExitSpineEditMode(discardChanges);
        if (shouldReloadPreview)
        {
            await ReloadSpineGroupDetailsAsync(spineGroupTab);
        }

        NotifyActionStates();
        NotifyActionBarStates();
        OnPropertyChanged(nameof(Inspector));
    }

    public async Task UndoSpineEditForActiveGroupAsync()
    {
        DocumentGroup.ActiveSpineGroupTab?.SpineEditDraft?.Undo();
        DocumentGroup.ActiveSpineGroupTab?.NotifySpineEditStateChanged();
        await ReloadActiveSpineGroupDetailsAsync();
        NotifyActionBarStates();
    }

    public async Task RedoSpineEditForActiveGroupAsync()
    {
        DocumentGroup.ActiveSpineGroupTab?.SpineEditDraft?.Redo();
        DocumentGroup.ActiveSpineGroupTab?.NotifySpineEditStateChanged();
        await ReloadActiveSpineGroupDetailsAsync();
        NotifyActionBarStates();
    }

    public Task UndoAppliedReplacementAsync(string? replacementOperationId = null) =>
        _assetActionCoordinator.UndoAppliedReplacementAsync(replacementOperationId);

    public Task RedoAppliedReplacementAsync(string? replacementOperationId = null) =>
        _assetActionCoordinator.RedoAppliedReplacementAsync(replacementOperationId);

    public Task DeleteChangeRecordAsync(string? operationId) =>
        _assetActionCoordinator.DeleteChangeRecordAsync(operationId);

    public bool RequestSpriteEditModeForActiveAsset()
    {
        var assetTab = DocumentGroup.ActiveAssetTab;
        if (assetTab?.Details?.AssetType != AssetType.Sprite)
        {
            return false;
        }

        assetTab.RequestSpriteEditMode();
        NotifyActionBarStates();
        return true;
    }

    public bool RequestSpineEditModeForActiveGroup()
    {
        var spineGroupTab = DocumentGroup.ActiveSpineGroupTab;
        if (spineGroupTab is null)
        {
            return false;
        }

        if (!spineGroupTab.IsSpineEditModeActive)
        {
            spineGroupTab.StartSpineEditMode(new SpineAssetGroupEditDraftViewModel(
                spineGroupTab.Details?.Group ?? spineGroupTab.Item,
                _localizationService));
        }

        spineGroupTab.Inspector.SelectReplacementSection();
        NotifyActionStates();
        NotifyActionBarStates();
        OnPropertyChanged(nameof(Inspector));
        return true;
    }

    private static bool CanUseTextEditor(AssetDetailsDto details) =>
        details.AssetType == AssetType.TextAsset ||
        string.Equals(details.AssetTypeDisplayName, "Shader", StringComparison.OrdinalIgnoreCase);

    private async Task<bool> TryRefreshAssetDocumentDetailsBeforeReopeningTextEditAsync(
        AssetDocumentTabViewModel assetTab)
    {
        if (_workspaceShell.WorkspaceSummary is not { } workspaceSummary)
        {
            return false;
        }

        var detailsResult = await _backgroundTaskRunner.RunAsync(
            token => _workspaceAssetInspectionGateway.GetAssetDetailsAsync(
                new GetAssetDetailsQuery(
                    workspaceSummary.WorkspaceId,
                    assetTab.BundleId,
                    assetTab.AssetId),
                token));
        if (!TryGetValue(detailsResult, out var details))
        {
            return false;
        }

        ReapplyAssetDocumentState(assetTab, details);
        return true;
    }

    private static bool ShouldReopenTextEditDraft(
        AssetDetailsDto details,
        TextAssetEditDraftViewModel draft)
    {
        if (!draft.IsAppliedBaseline || draft.HasUnsavedChanges)
        {
            return false;
        }

        return !AssetDocumentTextEditEffectiveReferenceResolver.DetailsPointToDraftRevision(
            details,
            draft.DraftId,
            draft.DraftMode,
            draft.Reference.DraftRevision);
    }
}
