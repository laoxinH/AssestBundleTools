namespace AssestBundleTools.Application.Common.Diagnostics;

public enum WorkspaceDiagnosticLogLevel
{
    Information = 0,
    Warning = 1,
    Error = 2
}

public sealed record WorkspaceDiagnosticLogEntryDto(
    DateTimeOffset Timestamp,
    WorkspaceDiagnosticLogLevel Level,
    string Message);
