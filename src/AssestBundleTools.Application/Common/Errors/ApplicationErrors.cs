using AssestBundleTools.Domain.Assets;
using AssestBundleTools.Domain.Bundles;
using AssestBundleTools.Domain.Common.Errors;
using AssestBundleTools.Domain.Workspace;

namespace AssestBundleTools.Application.Common.Errors;

/// <summary>
/// 统一定义应用层用例中会返回的标准错误。
/// </summary>
public static class ApplicationErrors
{
    public static AppError WorkspacePathRequired() =>
        new(
            code: "Workspace.Open.PathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Open.PathRequired");

    public static AppError WorkspaceCatalogKeyRequired() =>
        new(
            code: "Workspace.Catalog.WorkspaceKeyRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Catalog.WorkspaceKeyRequired");

    public static AppError WorkspaceCatalogSourcePathRequired() =>
        new(
            code: "Workspace.Catalog.SourcePathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Catalog.SourcePathRequired");

    public static AppError WorkspaceCatalogDataRootPathRequired() =>
        new(
            code: "Workspace.Catalog.DataRootPathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Catalog.DataRootPathRequired");

    public static AppError WorkspaceImportWorkspaceKeyRequired() =>
        new(
            code: "Workspace.Import.WorkspaceKeyRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Import.WorkspaceKeyRequired");

    public static AppError WorkspaceImportDataRootPathRequired() =>
        new(
            code: "Workspace.Import.DataRootPathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Import.DataRootPathRequired");

    public static AppError WorkspaceImportPathsRequired() =>
        new(
            code: "Workspace.Import.PathsRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Import.PathsRequired");

    public static AppError WorkspaceImportBundleSourcePathRequired() =>
        new(
            code: "Workspace.Import.BundleSourcePathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Import.BundleSourcePathRequired");

    public static AppError WorkspaceImportMetadataNotFound(string workspaceKey) =>
        new(
            code: "Workspace.Import.MetadataNotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Workspace.Import.MetadataNotFound",
            parameters: CreateParameters(("WorkspaceKey", workspaceKey)));

    public static AppError WorkspaceOpenDataRootPathRequired() =>
        new(
            code: "Workspace.Open.DataRootPathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Open.DataRootPathRequired");

    public static AppError WorkspaceIdRequired() =>
        new(
            code: "Workspace.Query.WorkspaceIdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Query.WorkspaceIdRequired");

    public static AppError BundleIdRequired() =>
        new(
            code: "Bundle.Query.BundleIdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Query.BundleIdRequired");

    public static AppError AssetIdRequired() =>
        new(
            code: "Asset.Query.AssetIdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Query.AssetIdRequired");

    public static AppError PaneIdRequired() =>
        new(
            code: "Workspace.OpenAsset.PaneIdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.OpenAsset.PaneIdRequired");

    public static AppError NoWorkspaceReplacementCandidates(AssetId assetId) =>
        new(
            code: "Asset.Replace.NoCompatibleSource",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.NoCompatibleSource",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    public static AppError NoBatchExtractionCandidates(WorkspaceId workspaceId) =>
        new(
            code: "Asset.Extract.NoBatchCandidates",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Extract.NoBatchCandidates",
            parameters: CreateParameters(("WorkspaceId", workspaceId.ToString())),
            isUserActionable: true);

    public static AppError NoBatchReplacementCandidates(WorkspaceId workspaceId) =>
        new(
            code: "Asset.Replace.NoBatchCandidates",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.NoBatchCandidates",
            parameters: CreateParameters(("WorkspaceId", workspaceId.ToString())),
            isUserActionable: true);

    public static AppError NoSaveCandidates(WorkspaceId workspaceId) =>
        new(
            code: "Bundle.Save.NoCandidates",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Save.NoCandidates",
            parameters: CreateParameters(("WorkspaceId", workspaceId.ToString())),
            isUserActionable: true);

    public static AppError BundleDecryptionFailed(string decryptorId, string? detail = null) =>
        new(
            code: "Bundle.Decrypt.Failed",
            category: ErrorCategory.ExternalDependency,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.Failed",
            parameters: CreateParameters(
                ("DecryptorId", decryptorId),
                ("Detail", detail)));

    public static AppError BundleDecryptionInvalidResult(string decryptorId, string? detail = null) =>
        new(
            code: "Bundle.Decrypt.InvalidResult",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.InvalidResult",
            parameters: CreateParameters(
                ("DecryptorId", decryptorId),
                ("Detail", detail)));

    public static AppError BundleDecryptionArgumentsRequired(string decryptorId) =>
        new(
            code: "Bundle.Decrypt.ArgumentsRequired",
            category: ErrorCategory.Configuration,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.ArgumentsRequired",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptionResultFileMissing(string decryptorId) =>
        new(
            code: "Bundle.Decrypt.ResultFileMissing",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.ResultFileMissing",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptionResultFileInvalid(string decryptorId) =>
        new(
            code: "Bundle.Decrypt.ResultFileInvalid",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.ResultFileInvalid",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptionOutputMissing(string decryptorId) =>
        new(
            code: "Bundle.Decrypt.OutputMissing",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.OutputMissing",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptionProcessExited(string decryptorId, int exitCode) =>
        new(
            code: "Bundle.Decrypt.ProcessExited",
            category: ErrorCategory.ExternalDependency,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.ProcessExited",
            parameters: CreateParameters(
                ("DecryptorId", decryptorId),
                ("ExitCode", exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture))));

    public static AppError BundleDecryptionTimedOut(string decryptorId, int timeoutMs) =>
        new(
            code: "Bundle.Decrypt.TimedOut",
            category: ErrorCategory.Timeout,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.TimedOut",
            parameters: CreateParameters(
                ("DecryptorId", decryptorId),
                ("TimeoutMs", timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture))));

    public static AppError BundleDecryptionExecutionFailed(string decryptorId) =>
        new(
            code: "Bundle.Decrypt.ExecutionFailed",
            category: ErrorCategory.ExternalDependency,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decrypt.ExecutionFailed",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptorFallbackNotFound() =>
        new(
            code: "Bundle.Decryptor.FallbackNotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.FallbackNotFound");

    public static AppError BundleSourceMissing(string bundlePath) =>
        new(
            code: "Bundle.SourceMissing",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.SourceMissing",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundleReadFormatUnrecognized(string bundlePath) =>
        new(
            code: "Bundle.Read.FormatUnrecognized",
            category: ErrorCategory.Unsupported,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Read.FormatUnrecognized",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundleReadNoCompatibleReader(string bundlePath) =>
        new(
            code: "Bundle.Read.NoCompatibleReader",
            category: ErrorCategory.Unsupported,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Read.NoCompatibleReader",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundleReadUnexpected(string bundlePath) =>
        new(
            code: "Bundle.Read.Unexpected",
            category: ErrorCategory.Unknown,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Read.Unexpected",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundleReadFailed(string bundlePath, string? detail = null) =>
        new(
            code: "Bundle.Read.Failed",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Read.Failed",
            parameters: CreateParameters(
                ("BundlePath", bundlePath),
                ("Detail", detail)));

    public static AppError BundlePreviewSerializedFileLocatorMissing(string bundlePath) =>
        new(
            code: "Bundle.Preview.SerializedFileLocatorMissing",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Preview.SerializedFileLocatorMissing",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundlePreviewAssetLocatorMissing(string bundlePath) =>
        new(
            code: "Bundle.Preview.AssetLocatorMissing",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Preview.AssetLocatorMissing",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundlePreviewFieldTreeUnavailable(string bundlePath) =>
        new(
            code: "Bundle.Preview.FieldTreeUnavailable",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Preview.FieldTreeUnavailable",
            parameters: CreateParameters(("BundlePath", bundlePath)));

    public static AppError BundlePreviewGenerationFailed(string bundlePath, string? detail = null) =>
        new(
            code: "Bundle.Preview.GenerationFailed",
            category: ErrorCategory.Unknown,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Preview.GenerationFailed",
            parameters: CreateParameters(("Detail", ResolveDiagnosticDetail(detail, bundlePath))));

    public static AppError BundleDecryptorCatalogIdRequired() =>
        new(
            code: "Bundle.Decryptor.Catalog.IdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.IdRequired");

    public static AppError BundleDecryptorCatalogDisplayNameRequired() =>
        new(
            code: "Bundle.Decryptor.Catalog.DisplayNameRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.DisplayNameRequired");

    public static AppError BundleDecryptorCatalogExecutablePathRequired() =>
        new(
            code: "Bundle.Decryptor.Catalog.ExecutablePathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.ExecutablePathRequired");

    public static AppError BundleDecryptorCatalogDecryptArgumentsRequired() =>
        new(
            code: "Bundle.Decryptor.Catalog.DecryptArgumentsRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.DecryptArgumentsRequired");

    public static AppError BundleDecryptorCatalogTimeoutInvalid() =>
        new(
            code: "Bundle.Decryptor.Catalog.TimeoutInvalid",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.TimeoutInvalid");

    public static AppError BundleDecryptorCatalogNotFound(string decryptorId) =>
        new(
            code: "Bundle.Decryptor.Catalog.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.Catalog.NotFound",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptorCatalogReadOnly(string decryptorId) =>
        new(
            code: "Bundle.Decryptor.Catalog.ReadOnly",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.ReadOnly",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptorCatalogAlreadyExists(string decryptorId) =>
        new(
            code: "Bundle.Decryptor.Catalog.AlreadyExists",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.Decryptor.Catalog.AlreadyExists",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptorCatalogSaveVerificationFailed(string decryptorId) =>
        new(
            code: "Bundle.Decryptor.Catalog.SaveVerificationFailed",
            category: ErrorCategory.SerializationFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.Catalog.SaveVerificationFailed",
            parameters: CreateParameters(("DecryptorId", decryptorId)));

    public static AppError BundleDecryptorCatalogSaveFailed() =>
        new(
            code: "Bundle.Decryptor.Catalog.SaveFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.Catalog.SaveFailed");

    public static AppError BundleDecryptorCatalogDeleteFailed() =>
        new(
            code: "Bundle.Decryptor.Catalog.DeleteFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.Catalog.DeleteFailed");

    public static AppError SourceFilePathRequired() =>
        new(
            code: "Asset.Replace.SourceFilePathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.SourceFilePathRequired");

    public static AppError AssetTypeMismatch(AssetType sourceType, AssetType targetType) =>
        new(
            code: "Asset.Replace.TypeMismatch",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.TypeMismatch",
            parameters: CreateParameters(
                ("SourceType", sourceType.ToString()),
                ("TargetType", targetType.ToString())),
            isUserActionable: true);

    public static AppError NoAppliedReplacement(AssetId assetId) =>
        new(
            code: "Asset.Replace.NoAppliedChange",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.NoAppliedChange",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    public static AppError WorkspaceNotFound(WorkspaceId workspaceId) =>
        new(
            code: "Workspace.Query.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Workspace.Query.NotFound",
            parameters: CreateParameters(("WorkspaceId", workspaceId.ToString())),
            isUserActionable: true);

    public static AppError BundleNotFound(BundleId bundleId) =>
        new(
            code: "Bundle.Query.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Query.NotFound",
            parameters: CreateParameters(("BundleId", bundleId.ToString())),
            isUserActionable: true);

    public static AppError AssetNotFound(AssetId assetId) =>
        new(
            code: "Asset.Query.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Asset.Query.NotFound",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    private static IReadOnlyDictionary<string, object?> CreateParameters(params (string Key, object? Value)[] pairs)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in pairs)
        {
            parameters[key] = value;
        }

        return parameters;
    }

    private static string ResolveDiagnosticDetail(string? detail, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(detail))
        {
            return detail.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback)
            ? "Unknown error."
            : fallback.Trim();
    }
}
