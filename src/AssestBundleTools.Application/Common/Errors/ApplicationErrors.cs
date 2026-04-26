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

    public static AppError BundleDecryptorFallbackNotFound() =>
        new(
            code: "Bundle.Decryptor.FallbackNotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.FallbackNotFound");

    public static AppError BundleReadFailed(string bundlePath, string? detail = null) =>
        new(
            code: "Bundle.Read.Failed",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Read.Failed",
            parameters: CreateParameters(
                ("BundlePath", bundlePath),
                ("Detail", detail)));

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

    public static AppError BundleDecryptorCatalogSaveFailed(string? detail = null) =>
        new(
            code: "Bundle.Decryptor.Catalog.SaveFailed",
            category: ErrorCategory.ExternalDependency,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.Catalog.SaveFailed",
            parameters: CreateParameters(("Detail", detail)));

    public static AppError BundleDecryptorCatalogDeleteFailed(string? detail = null) =>
        new(
            code: "Bundle.Decryptor.Catalog.DeleteFailed",
            category: ErrorCategory.ExternalDependency,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Decryptor.Catalog.DeleteFailed",
            parameters: CreateParameters(("Detail", detail)));

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
}
