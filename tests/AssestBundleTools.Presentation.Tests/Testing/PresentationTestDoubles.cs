using System.ComponentModel;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Queries;
using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Queries;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Application.Common.Results;
using AssestBundleTools.Application.Editing.Dtos;
using AssestBundleTools.Application.Editing.Queries;
using AssestBundleTools.Application.Editing.Services;
using AssestBundleTools.Application.Workspace.Commands;
using AssestBundleTools.Application.Workspace.Dtos;
using AssestBundleTools.Application.Workspace.Services;
using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Domain.Workspace;
using AssestBundleTools.Presentation.WinUI.Localization;
using AssestBundleTools.Presentation.WinUI.Services;

using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.ActionHandlers;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Bindings;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Contracts;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Coordinators;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Formatting;
using AssestBundleTools.Presentation.WinUI.ViewModels.BundleExplorer.Presenters;
namespace AssestBundleTools.Presentation.Tests.Testing;

internal sealed class TestLocalizationService : ILocalizationService
{
    public string GetRequiredString(string resourceKey) => resourceKey;

    public string GetFormattedString(string resourceKey, params object[] arguments) =>
        $"{resourceKey}:{string.Join("|", arguments.Select(static argument => argument?.ToString() ?? string.Empty))}";
}

internal sealed class TestAppLogService : IAppLogService
{
    public LogPanelViewModel LogPanel { get; } = new();

    public List<string> InfoMessages { get; } = [];

    public List<string> WarningMessages { get; } = [];

    public List<string> ErrorMessages { get; } = [];

    public int ClearCallCount { get; private set; }

    public void Clear()
    {
        ClearCallCount++;
        LogPanel.Clear();
        InfoMessages.Clear();
        WarningMessages.Clear();
        ErrorMessages.Clear();
    }

    public void AppendInfo(string message)
    {
        InfoMessages.Add(message);
        LogPanel.Append(message);
    }

    public void AppendWarning(string message)
    {
        WarningMessages.Add(message);
        LogPanel.Append(message);
    }

    public void AppendError(string message)
    {
        ErrorMessages.Add(message);
        LogPanel.Append(message);
    }
}

internal sealed class TestAppNotificationService : IAppNotificationService
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsOpen { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public AppNotificationSeverity Severity { get; private set; }

    public int ShowCallCount { get; private set; }

    public void Show(string title, string message, AppNotificationSeverity severity, TimeSpan? duration = null)
    {
        ShowCallCount++;
        IsOpen = true;
        Title = title;
        Message = message;
        Severity = severity;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public void Dismiss()
    {
        IsOpen = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}

internal sealed class TestWorkspaceContextService : IWorkspaceContextService
{
    private readonly List<WorkspaceContextEntryDto> _workspaceEntries = [];

    public event EventHandler? CurrentWorkspaceChanged;

    public event EventHandler? WorkspaceEntriesChanged;

    public WorkspaceSummaryDto? CurrentWorkspaceSummary { get; private set; }

    public WorkspaceSummaryDto? ActiveWorkspaceSummary => CurrentWorkspaceSummary;

    public bool HasCurrentWorkspace => CurrentWorkspaceSummary is not null;

    public IReadOnlyList<WorkspaceContextEntryDto> WorkspaceEntries => _workspaceEntries;

    public int WorkspaceEntryCount => _workspaceEntries.Count;

    public int RegisterOrActivateWorkspaceCallCount { get; private set; }

    public int ClearCurrentWorkspaceCallCount { get; private set; }

    public WorkspaceBrowserStateSnapshotDto? CurrentBrowserState { get; set; }

    public int UpdateCurrentWorkspaceBrowserStateCallCount { get; private set; }

    public WorkspaceBrowserStateSnapshotDto? GetCurrentWorkspaceBrowserState() => CurrentBrowserState;

    public bool TryGetLastActiveWorkspaceNavigationRequest(out WorkspaceNavigationRequest? workspaceNavigationRequest)
    {
        workspaceNavigationRequest = null;
        return false;
    }

    public bool TryGetCurrentWorkspaceEntry(out WorkspaceContextEntryDto? workspaceEntry)
    {
        workspaceEntry = CurrentWorkspaceSummary is null
            ? null
            : _workspaceEntries.FirstOrDefault(entry =>
                string.Equals(
                    entry.WorkspaceSummary?.WorkspaceId.Value,
                    CurrentWorkspaceSummary.WorkspaceId.Value,
                    StringComparison.Ordinal));
        return workspaceEntry is not null;
    }

    public void RegisterOrActivateWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath = null,
        string? assetBundleDecryptorId = null)
    {
        RegisterOrActivateWorkspaceCallCount++;
        CurrentWorkspaceSummary = workspaceSummary;

        var workspaceKey = WorkspaceCatalogKeyFactory.CreateWorkspaceKey(
            workspaceSummary.SourcePath,
            workspaceDataRootPath);
        var workspaceEntry = new WorkspaceContextEntryDto(
            WorkspaceKey: workspaceKey,
            DisplayName: workspaceSummary.DisplayName,
            SourcePath: workspaceSummary.SourcePath,
            WorkspaceDataRootPath: workspaceDataRootPath ?? string.Empty,
            AssetBundleDecryptorId: assetBundleDecryptorId,
            WorkspaceMode: workspaceSummary.Mode,
            LastActivatedAtUtc: workspaceSummary.OpenedAtUtc,
            IsPinned: false,
            AvailabilityStatus: WorkspaceAvailabilityStatus.Available,
            LastKnownState: null,
            WorkspaceSummary: workspaceSummary);

        var existingIndex = _workspaceEntries.FindIndex(entry =>
            string.Equals(entry.WorkspaceKey, workspaceKey, StringComparison.Ordinal));
        if (existingIndex >= 0)
        {
            _workspaceEntries.RemoveAt(existingIndex);
        }

        _workspaceEntries.Insert(0, workspaceEntry);
        WorkspaceEntriesChanged?.Invoke(this, EventArgs.Empty);
        CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadWorkspaceCatalogEntries()
    {
    }

    public void UpdateCurrentWorkspaceBrowserState(WorkspaceBrowserStateSnapshotDto browserStateSnapshot)
    {
        UpdateCurrentWorkspaceBrowserStateCallCount++;
        CurrentBrowserState = browserStateSnapshot;
    }

    public bool TryActivateWorkspace(WorkspaceId workspaceId)
    {
        var targetEntry = _workspaceEntries.FirstOrDefault(entry =>
            string.Equals(entry.WorkspaceSummary?.WorkspaceId.Value, workspaceId.Value, StringComparison.Ordinal));
        if (targetEntry?.WorkspaceSummary is null)
        {
            return false;
        }

        CurrentWorkspaceSummary = targetEntry.WorkspaceSummary;
        CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void SetCurrentWorkspace(
        WorkspaceSummaryDto workspaceSummary,
        string? workspaceDataRootPath = null,
        string? assetBundleDecryptorId = null)
    {
        RegisterOrActivateWorkspace(workspaceSummary, workspaceDataRootPath, assetBundleDecryptorId);
    }

    public void ClearCurrentWorkspace()
    {
        ClearCurrentWorkspaceCallCount++;
        CurrentWorkspaceSummary = null;
        CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal sealed class TestAssetBundleDecryptorRegistry : IAssetBundleDecryptorRegistry
{
    private readonly IReadOnlyList<IAssetBundleDecryptor> _decryptors =
    [
        new TestAssetBundleDecryptor(
            new AssetBundleDecryptorDescriptorDto(
                AssetBundleDecryptorIds.BuiltInPassThrough,
                "PassThrough",
                "1.0.0",
                AssetBundleDecryptorKind.BuiltIn,
                SupportsAutoDetection: false,
                IsFallback: true,
                Description: "Fallback"))
    ];

    public IReadOnlyList<IAssetBundleDecryptor> GetAll() => _decryptors;

    public bool TryGet(string decryptorId, out IAssetBundleDecryptor decryptor)
    {
        decryptor = _decryptors.First();
        return string.Equals(
            decryptorId,
            decryptor.Descriptor.Id,
            StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class TestBackgroundTaskRunner : IBackgroundTaskRunner
{
    public int RunCallCount { get; private set; }

    public Type? LastResultType { get; private set; }

    public Func<Func<CancellationToken, Task<object>>, CancellationToken, Task<object>>? RunAsyncHandler { get; init; }

    public Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        RunCallCount++;
        LastResultType = typeof(TResult);
        if (RunAsyncHandler is not null)
        {
            return RunWithHandlerAsync(operation, cancellationToken);
        }

        return operation(cancellationToken);
    }

    private async Task<TResult> RunWithHandlerAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var result = await RunAsyncHandler!(
            async token => (await operation(token))!,
            cancellationToken);
        return (TResult)result;
    }
}

internal sealed class TestWorkspaceAssetInspectionGateway : IWorkspaceAssetInspectionGateway
{
    public Func<OpenAssetInPaneCommand, Task<Result<AssetPaneContextDto>>> OpenAssetInPaneHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Func<GetAssetDetailsQuery, Task<Result<AssetDetailsDto>>> GetAssetDetailsHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Func<GetAssetPreviewQuery, Task<Result<AssetPreviewDto>>> GetAssetPreviewHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Func<LoadPreviewTextChunkQuery, Task<Result<AssetPreviewTextChunkDto>>> LoadPreviewTextChunkHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Func<LoadPreviewTextLinesQuery, Task<Result<AssetPreviewTextLineRangeDto>>> LoadPreviewTextLinesHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Func<LoadSpineAssetGroupDetailsQuery, Task<Result<SpineAssetGroupDetailsDto>>> LoadSpineAssetGroupDetailsHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Task<Result<AssetDetailsDto>> GetAssetDetailsAsync(
        GetAssetDetailsQuery query,
        CancellationToken cancellationToken = default) =>
        GetAssetDetailsHandler(query);

    public Task<Result<AssetPreviewDto>> GetAssetPreviewAsync(
        GetAssetPreviewQuery query,
        CancellationToken cancellationToken = default) =>
        GetAssetPreviewHandler(query);

    public Task<Result<AssetPreviewTextChunkDto>> LoadPreviewTextChunkAsync(
        LoadPreviewTextChunkQuery query,
        CancellationToken cancellationToken = default) =>
        LoadPreviewTextChunkHandler(query);

    public Task<Result<AssetPreviewTextLineRangeDto>> LoadPreviewTextLinesAsync(
        LoadPreviewTextLinesQuery query,
        CancellationToken cancellationToken = default) =>
        LoadPreviewTextLinesHandler(query);

    public Task<Result<SpineAssetGroupDetailsDto>> LoadSpineAssetGroupDetailsAsync(
        LoadSpineAssetGroupDetailsQuery query,
        CancellationToken cancellationToken = default) =>
        LoadSpineAssetGroupDetailsHandler(query);

    public Task<Result<AssetPaneContextDto>> OpenAssetInPaneAsync(
        OpenAssetInPaneCommand command,
        CancellationToken cancellationToken = default) =>
        OpenAssetInPaneHandler(command);
}

internal sealed class TestWorkspaceExplorerGateway : IWorkspaceExplorerGateway
{
    public required Func<OpenWorkspaceCommand, Task<Result<WorkspaceSummaryDto>>> OpenWorkspaceHandler { get; init; }

    public required Func<LoadBundleTreeQuery, Task<Result<IReadOnlyList<BundleTreeNodeDto>>>> LoadBundleTreeHandler { get; init; }

    public Func<LoadAssetListQuery, Task<Result<AssetListResultDto>>> LoadAssetListHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Func<LoadSpineAssetGroupsQuery, Task<Result<SpineAssetGroupListResultDto>>> LoadSpineAssetGroupsHandler { get; init; } =
        _ => throw new NotSupportedException();

    public Task<Result<WorkspaceSummaryDto>> OpenWorkspaceAsync(
        OpenWorkspaceCommand command,
        CancellationToken cancellationToken = default) =>
        OpenWorkspaceHandler(command);

    public Task<Result<WorkspaceImportRegistrationResultDto>> RegisterWorkspaceImportAsync(
        RegisterWorkspaceImportCommand command,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<WorkspaceImportProcessingResultDto>> ProcessWorkspaceImportsAsync(
        ProcessWorkspaceImportsCommand command,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<WorkspaceBundleRemovalResultDto>> RemoveWorkspaceBundleAsync(
        RemoveWorkspaceBundleCommand command,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<IReadOnlyList<BundleTreeNodeDto>>> LoadBundleTreeAsync(
        LoadBundleTreeQuery query,
        CancellationToken cancellationToken = default) =>
        LoadBundleTreeHandler(query);

    public Task<Result<AssetListResultDto>> LoadAssetListAsync(
        LoadAssetListQuery query,
        CancellationToken cancellationToken = default) =>
        LoadAssetListHandler(query);

    public Task<Result<SpineAssetGroupListResultDto>> LoadSpineAssetGroupsAsync(
        LoadSpineAssetGroupsQuery query,
        CancellationToken cancellationToken = default) =>
        LoadSpineAssetGroupsHandler(query);
}

internal sealed class TestAssetEditDraftRestorePlanGateway : IAssetEditDraftRestorePlanGateway
{
    public List<GetAssetEditDraftRestorePlanQuery> Queries { get; } = [];

    public Func<GetAssetEditDraftRestorePlanQuery, Task<Result<AssetEditDraftRestorePlanDto>>> GetRestorePlanHandler { get; init; } =
        query => Task.FromResult(Result<AssetEditDraftRestorePlanDto>.Success(
            new AssetEditDraftRestorePlanDto(
                WorkspaceKey: query.WorkspaceKey,
                TotalReferenceCount: query.DraftReferences.Count,
                RestorableCount: 0,
                MissingCount: query.DraftReferences.Count,
                SourceMismatchCount: 0,
                ExpiredCount: 0,
                InvalidReferenceCount: 0,
                Candidates: Array.Empty<AssetEditDraftRestoreCandidateDto>())));

    public Task<Result<AssetEditDraftRestorePlanDto>> GetRestorePlanAsync(
        GetAssetEditDraftRestorePlanQuery query,
        CancellationToken cancellationToken = default)
    {
        Queries.Add(query);
        return GetRestorePlanHandler(query);
    }
}

internal sealed class TestBrowserStateCoordinator : IBundleExplorerBrowserStateCoordinator
{
    private readonly bool _restoreResult;

    public TestBrowserStateCoordinator(bool restoreResult)
    {
        _restoreResult = restoreResult;
    }

    public int RestoreCallCount { get; private set; }

    public int CancelCallCount { get; private set; }

    public int ClearPendingRestoreStateCallCount { get; private set; }

    public Task<bool> TryRestoreWorkspaceBrowserStateAsync()
    {
        RestoreCallCount++;
        return Task.FromResult(_restoreResult);
    }

    public void CancelWorkspaceBrowserStatePersistence()
    {
        CancelCallCount++;
    }

    public void ClearPendingAssetEditorWindowRestoreState()
    {
        ClearPendingRestoreStateCallCount++;
    }
}

internal sealed class TestAssetWorkflowBindingHost : IBundleExplorerAssetWorkflowBindingHost
{
    public AssetPaneViewModel PrimaryPane { get; } = new();

    public int ClearActiveTabStateCallCount { get; private set; }

    public void ClearActiveTabState()
    {
        ClearActiveTabStateCallCount++;
    }
}

internal static class PresentationTestDataFactory
{
    public static WorkspaceNavigationRequest CreateNavigationRequest(string name)
    {
        var token = Guid.NewGuid().ToString("N");
        return new WorkspaceNavigationRequest(
            SourcePath: $@"C:\Tests\Source\{name}-{token}",
            WorkspaceDisplayName: name,
            WorkspaceDataRootPath: $@"C:\Tests\Data\{name}-{token}",
            OpenTarget: WorkspaceOpenTarget.CurrentWindow,
            AssetBundleDecryptorId: AssetBundleDecryptorIds.BuiltInPassThrough);
    }

    public static WorkspaceSummaryDto CreateWorkspaceSummary(WorkspaceNavigationRequest navigationRequest)
    {
        return new WorkspaceSummaryDto(
            WorkspaceId: WorkspaceId.New(),
            DisplayName: navigationRequest.WorkspaceDisplayName,
            SourcePath: navigationRequest.SourcePath,
            Mode: WorkspaceMode.SinglePane,
            ActivePaneId: PaneId.New(),
            BundleCount: 1,
            AssetCount: 2,
            OpenedAtUtc: DateTimeOffset.UtcNow);
    }

    public static IReadOnlyList<BundleTreeNodeDto> CreateBundleTree(string bundleName)
    {
        return
        [
            new BundleTreeNodeDto(
                BundleId: new BundleId($"group-{bundleName}"),
                DisplayName: "Root",
                LogicalPath: "Root",
                SourcePath: string.Empty,
                IsVirtualFolder: true,
                AssetCount: 0,
                Children:
                [
                    new BundleTreeNodeDto(
                        BundleId: new BundleId($"bundle-{bundleName}"),
                        DisplayName: bundleName,
                        LogicalPath: $"{bundleName}.ab",
                        SourcePath: $@"C:\Bundles\{bundleName}.ab",
                        IsVirtualFolder: false,
                        AssetCount: 2,
                        Children: Array.Empty<BundleTreeNodeDto>())
                ])
        ];
    }

    public static AppError CreateFailure(string code = "tests.failure")
    {
        return new AppError(
            code: code,
            category: ErrorCategory.Unknown,
            severity: ErrorSeverity.Error,
            messageToken: ResourceKeys.CommonUnexpectedFailure);
    }
}

internal sealed class TestAssetBundleDecryptor : IAssetBundleDecryptor
{
    public TestAssetBundleDecryptor(AssetBundleDecryptorDescriptorDto descriptor)
    {
        Descriptor = descriptor;
    }

    public AssetBundleDecryptorDescriptorDto Descriptor { get; }

    public Task<AssetBundleDecryptorProbeResultDto> ProbeAsync(
        AssetBundleDecryptorProbeRequestDto request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Result<AssetBundleDecryptionResultDto>> DecryptAsync(
        AssetBundleDecryptionRequestDto request,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

