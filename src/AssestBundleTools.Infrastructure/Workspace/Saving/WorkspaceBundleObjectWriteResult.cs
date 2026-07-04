namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal sealed record WorkspaceBundleObjectWriteResult(
    string OriginalCompression,
    string WrittenCompression);
