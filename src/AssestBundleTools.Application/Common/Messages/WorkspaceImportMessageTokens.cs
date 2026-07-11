namespace AssestBundleTools.Application.Common.Messages;

/// <summary>
/// 集中定义工作区导入诊断链路使用的稳定消息令牌。
/// </summary>
/// <remarks>
/// 这些令牌不是最终展示文本，而是跨层传递的稳定语义标识；
/// 对应的本地化资源由 Presentation 层维护。
/// </remarks>
public static class WorkspaceImportMessageTokens
{
    public const string SourceInspectionSourceFileMissing = "WorkspaceImport.Detail.SourceInspection.SourceFileMissing";
    public const string SourceInspectionSourceFileEmpty = "WorkspaceImport.Detail.SourceInspection.SourceFileEmpty";
    public const string SourceInspectionSourceMetadataUnreadable = "WorkspaceImport.Detail.SourceInspection.SourceMetadataUnreadable";
    public const string SourceInspectionMatchedKnownExtension = "WorkspaceImport.Detail.SourceInspection.MatchedKnownExtension";
    public const string SourceInspectionMatchedKnownHeader = "WorkspaceImport.Detail.SourceInspection.MatchedKnownHeader";
    public const string SourceInspectionMatchedCommonNonBundleSignature = "WorkspaceImport.Detail.SourceInspection.MatchedCommonNonBundleSignature";
    public const string SourceInspectionNoKnownPlainSignature = "WorkspaceImport.Detail.SourceInspection.NoKnownPlainSignature";

    public const string ScreeningSourceRejectedDuringInspection = "WorkspaceImport.Detail.Screening.SourceRejectedDuringInspection";
    public const string ScreeningAutoDetectionNoDecryptorMatch = "WorkspaceImport.Detail.Screening.AutoDetectionNoDecryptorMatch";
    public const string ScreeningConfiguredDecryptorProbeUnsupported = "WorkspaceImport.Detail.Screening.ConfiguredDecryptorProbeUnsupported";
    public const string ScreeningProbeResultMissing = "WorkspaceImport.Detail.Screening.ProbeResultMissing";
    public const string ScreeningProbeFailed = "WorkspaceImport.Detail.Screening.ProbeFailed";
    public const string ScreeningProbeDidNotMatch = "WorkspaceImport.Detail.Screening.ProbeDidNotMatch";

    public const string ReaderNoRegisteredReaderRecognizedOutput = "WorkspaceImport.Detail.Reader.NoRegisteredReaderRecognizedOutput";

    public const string CacheCacheMiss = "WorkspaceImport.Detail.Cache.CacheMiss";
    public const string CacheSourceFingerprintChanged = "WorkspaceImport.Detail.Cache.SourceFingerprintChanged";
    public const string CacheDecryptorChanged = "WorkspaceImport.Detail.Cache.DecryptorChanged";
    public const string CacheDecryptorVersionChanged = "WorkspaceImport.Detail.Cache.DecryptorVersionChanged";
    public const string CacheToolVersionChanged = "WorkspaceImport.Detail.Cache.ToolVersionChanged";
    public const string CacheParserVersionChanged = "WorkspaceImport.Detail.Cache.ParserVersionChanged";
    public const string CacheSourceRemoved = "WorkspaceImport.Detail.Cache.SourceRemoved";

    public const string DecryptorProbeAutoDetectionDisabled = "WorkspaceImport.Detail.DecryptorProbe.AutoDetectionDisabled";
    public const string DecryptorProbeResultFileMissing = "WorkspaceImport.Detail.DecryptorProbe.ResultFileMissing";
    public const string DecryptorProbeResultFileInvalid = "WorkspaceImport.Detail.DecryptorProbe.ResultFileInvalid";
    public const string DecryptorProbeProcessExited = "WorkspaceImport.Detail.DecryptorProbe.ProcessExited";
    public const string DecryptorProbeProcessTimedOut = "WorkspaceImport.Detail.DecryptorProbe.ProcessTimedOut";
    public const string DecryptorProbeExecutionFailed = "WorkspaceImport.Detail.DecryptorProbe.ExecutionFailed";
}
