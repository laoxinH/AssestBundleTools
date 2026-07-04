using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssestBundleTools.Application.Saving.Dtos;

namespace AssestBundleTools.Infrastructure.Workspace.Saving;

internal static class BundleExportWriteDiagnostics
{
    public static T ExecuteStage<T>(
        string stage,
        WorkspaceBundleExportRequestDto request,
        Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (IsDiagnosableWriteException(exception))
        {
            throw new BundleExportStageException(
                BuildWriteFailureDetail(stage, request, exception),
                exception);
        }
    }

    public static void ExecuteStage(
        string stage,
        WorkspaceBundleExportRequestDto request,
        Action action) =>
        ExecuteStage(
            stage,
            request,
            () =>
            {
                action();
                return true;
            });

    public static T ExecuteStage<T>(
        string stage,
        BundleExportStageContext context,
        Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (IsDiagnosableWriteException(exception))
        {
            throw new BundleExportStageException(
                BuildWriteFailureDetail(stage, context, exception),
                exception);
        }
    }

    public static T ExecuteStage<T>(
        string stage,
        WorkspaceBundleExportRequestDto request,
        BundleExportStageContext context,
        Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception exception) when (IsDiagnosableWriteException(exception))
        {
            throw new BundleExportStageException(
                BuildWriteFailureDetail(stage, request, context, exception),
                exception);
        }
    }

    public static void ExecuteStage(
        string stage,
        WorkspaceBundleExportRequestDto request,
        BundleExportStageContext context,
        Action action) =>
        ExecuteStage(
            stage,
            request,
            context,
            () =>
            {
                action();
                return true;
            });

    public static void ExecuteStage(
        string stage,
        BundleExportStageContext context,
        Action action) =>
        ExecuteStage(
            stage,
            context,
            () =>
            {
                action();
                return true;
            });

    public static bool IsDiagnosableWriteException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or OutOfMemoryException or BundleExportStageException;

    public static string ResolveTopLevelDetail(
        string stage,
        WorkspaceBundleExportRequestDto request,
        Exception exception) =>
        exception is BundleExportStageException stageException
            ? stageException.DiagnosticDetail
            : BuildWriteFailureDetail(stage, request, exception);

    public static string FormatExceptionDiagnostic(Exception exception) =>
        $"exception={exception.GetType().Name}; message={NormalizeDiagnosticText(exception.Message)}";

    public static string FormatProcessArchitecture() =>
        Environment.Is64BitProcess ? "x64" : "x86";

    public static string FormatManagedMemory() =>
        $"managedMemory={GC.GetTotalMemory(forceFullCollection: false)}";

    private static string BuildWriteFailureDetail(
        string stage,
        WorkspaceBundleExportRequestDto request,
        Exception exception) =>
        string.Join(
            "; ",
            new[]
            {
                $"stage={stage}",
                $"bundle={request.BundleDisplayName}",
                $"changes={request.AssetWriteItems.Count}",
                $"sessionKey={FormatSessionCacheKey(request.BundleSessionCacheKey)}",
                $"sourceSize={FormatFileSize(request.SourceArtifactPath)}",
                $"process={FormatProcessArchitecture()}",
                FormatManagedMemory(),
                FormatExceptionDiagnostic(UnwrapStageException(exception))
            }.Where(static part => !string.IsNullOrWhiteSpace(part)));

    private static string BuildWriteFailureDetail(
        string stage,
        BundleExportStageContext context,
        Exception exception) =>
        string.Join(
            "; ",
            new[]
            {
                $"stage={stage}",
                $"compression={context.Compression}",
                $"directoryCount={context.DirectoryCount}",
                $"process={FormatProcessArchitecture()}",
                FormatManagedMemory(),
                FormatExceptionDiagnostic(UnwrapStageException(exception))
            }.Where(static part => !string.IsNullOrWhiteSpace(part)));

    private static string BuildWriteFailureDetail(
        string stage,
        WorkspaceBundleExportRequestDto request,
        BundleExportStageContext context,
        Exception exception) =>
        string.Join(
            "; ",
            new[]
            {
                $"stage={stage}",
                $"bundle={request.BundleDisplayName}",
                $"changes={request.AssetWriteItems.Count}",
                $"sessionKey={FormatSessionCacheKey(request.BundleSessionCacheKey)}",
                $"sourceSize={FormatFileSize(request.SourceArtifactPath)}",
                $"compression={context.Compression}",
                $"directoryCount={context.DirectoryCount}",
                $"process={FormatProcessArchitecture()}",
                FormatManagedMemory(),
                FormatExceptionDiagnostic(UnwrapStageException(exception))
            }.Where(static part => !string.IsNullOrWhiteSpace(part)));

    private static Exception UnwrapStageException(Exception exception) =>
        exception is BundleExportStageException stageException && stageException.InnerException is not null
            ? stageException.InnerException
            : exception;

    private static string FormatFileSize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "missing";
        }

        try
        {
            return new FileInfo(path).Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "unavailable";
        }
    }

    private static string FormatSessionCacheKey(string? sessionCacheKey) =>
        string.IsNullOrWhiteSpace(sessionCacheKey)
            ? "none"
            : sessionCacheKey.Trim();

    private static string NormalizeDiagnosticText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
}

internal sealed class BundleExportStageException : Exception
{
    public BundleExportStageException(string diagnosticDetail, Exception innerException)
        : base(diagnosticDetail, innerException)
    {
        DiagnosticDetail = diagnosticDetail;
    }

    public string DiagnosticDetail { get; }
}

internal readonly record struct BundleExportStageContext(
    string Compression,
    int DirectoryCount)
{
    public static BundleExportStageContext FromBundle(BundleFileInstance bundleInstance) =>
        new(
            bundleInstance.originalCompression.ToString(),
            bundleInstance.file.BlockAndDirInfo.DirectoryInfos.Count);

    public static BundleExportStageContext FromBundle(AssetBundleFile bundleFile) =>
        new(
            "current",
            bundleFile.BlockAndDirInfo.DirectoryInfos.Count);
}
