using AssestBundleTools.Application.Common.Messages;

namespace AssestBundleTools.Application.Workspace.Dtos;

/// <summary>
/// 表示自动识别阶段对某个解密器候选的探测摘要。
/// </summary>
public sealed record WorkspaceImportDecryptorProbeDiagnosticDto(
    string DecryptorId,
    string DecryptorDisplayName,
    int MatchedFileCount,
    int SuccessfulProbeCount,
    int TotalConfidence,
    bool WasSelected,
    IReadOnlyList<LocalizedMessageDto> SampleMessages,
    IReadOnlyList<string>? DetectedFormatIds = null,
    IReadOnlyList<string>? Evidence = null);
