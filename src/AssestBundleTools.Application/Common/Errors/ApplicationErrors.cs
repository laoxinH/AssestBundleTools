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

    public static AppError BundleArtifactCachePruneLimitInvalid() =>
        new(
            code: "Workspace.Cache.BundleArtifact.PruneLimitInvalid",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.Cache.BundleArtifact.PruneLimitInvalid");

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

    public static AppError SpineAssetGroupIdRequired() =>
        new(
            code: "Asset.SpineGroup.Query.GroupIdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.SpineGroup.Query.GroupIdRequired");

    public static AppError SpineAssetGroupNotFound(string groupId) =>
        new(
            code: "Asset.SpineGroup.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.SpineGroup.NotFound",
            parameters: CreateParameters(("GroupId", groupId)),
            isUserActionable: true);

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

    public static AppError NoSpineAssetGroupExtractionCandidates(string groupId) =>
        new(
            code: "Asset.Extract.SpineGroup.NoCandidates",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Extract.SpineGroup.NoCandidates",
            parameters: CreateParameters(("GroupId", groupId)),
            isUserActionable: true);

    public static AppError AssetExtractionOutputDirectoryRequired() =>
        new(
            code: "Asset.Extract.OutputDirectoryRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Extract.OutputDirectoryRequired",
            isUserActionable: true);

    public static AppError AssetExtractionNotSupported(AssetId assetId) =>
        new(
            code: "Asset.Extract.NotSupported",
            category: ErrorCategory.Unsupported,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Extract.NotSupported",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    public static AppError AssetExtractionPayloadUnavailable(AssetId assetId) =>
        new(
            code: "Asset.Extract.PayloadUnavailable",
            category: ErrorCategory.ParseFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Asset.Extract.PayloadUnavailable",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    public static AppError AssetExtractionWriteFailed(string outputPath, string? detail = null) =>
        new(
            code: "Asset.Extract.WriteFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Asset.Extract.WriteFailed",
            parameters: CreateParameters(
                ("OutputPath", outputPath),
                ("Detail", detail)));

    public static AppError ExportManifestWriteFailed(string outputPath, string? detail = null) =>
        new(
            code: "Asset.Extract.ManifestWriteFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Asset.Extract.ManifestWriteFailed",
            parameters: CreateParameters(
                ("OutputPath", outputPath),
                ("Detail", detail)));

    public static AppError ExportManifestReadFailed(string manifestPath, string? detail = null) =>
        new(
            code: "Asset.Extract.ManifestReadFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Asset.Extract.ManifestReadFailed",
            parameters: CreateParameters(
                ("ManifestPath", manifestPath),
                ("Detail", detail)));

    public static AppError NoBatchReplacementCandidates(WorkspaceId workspaceId) =>
        new(
            code: "Asset.Replace.NoBatchCandidates",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.NoBatchCandidates",
            parameters: CreateParameters(("WorkspaceId", workspaceId.ToString())),
            isUserActionable: true);

    public static AppError BatchReplacementSourceDirectoryRequired() =>
        new(
            code: "Asset.Replace.BatchSourceDirectoryRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.BatchSourceDirectoryRequired",
            isUserActionable: true);

    public static AppError BatchReplacementSourceFileReadFailed(string sourcePath, string? detail = null) =>
        new(
            code: "Asset.Replace.BatchSourceFileReadFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Asset.Replace.BatchSourceFileReadFailed",
            parameters: CreateParameters(
                ("SourcePath", sourcePath),
                ("Detail", detail)));

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

    public static AppError BundlePreviewOutOfMemory(string bundlePath, string? detail = null) =>
        new(
            code: "Bundle.Preview.OutOfMemory",
            category: ErrorCategory.Unknown,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.Preview.OutOfMemory",
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

    public static AppError ResourceProcessorCatalogIdRequired() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.IdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.IdRequired");

    public static AppError ResourceProcessorCatalogDisplayNameRequired() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.DisplayNameRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.DisplayNameRequired");

    public static AppError ResourceProcessorCatalogResourceKindsRequired() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.ResourceKindsRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.ResourceKindsRequired");

    public static AppError ResourceProcessorCatalogExecutablePathRequired() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.ExecutablePathRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.ExecutablePathRequired");

    public static AppError ResourceProcessorCatalogProcessArgumentsRequired() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.ProcessArgumentsRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.ProcessArgumentsRequired");

    public static AppError ResourceProcessorCatalogTimeoutInvalid() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.TimeoutInvalid",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.TimeoutInvalid");

    public static AppError ResourceProcessorCatalogNotFound(string processorId) =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.NotFound",
            parameters: CreateParameters(("ProcessorId", processorId)));

    public static AppError ResourceProcessorCatalogReadOnly(string processorId) =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.ReadOnly",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.ReadOnly",
            parameters: CreateParameters(("ProcessorId", processorId)));

    public static AppError ResourceProcessorCatalogAlreadyExists(string processorId) =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.AlreadyExists",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.AlreadyExists",
            parameters: CreateParameters(("ProcessorId", processorId)));

    public static AppError ResourceProcessorCatalogSaveVerificationFailed(string processorId) =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.SaveVerificationFailed",
            category: ErrorCategory.SerializationFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.SaveVerificationFailed",
            parameters: CreateParameters(("ProcessorId", processorId)));

    public static AppError ResourceProcessorCatalogSaveFailed() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.SaveFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.SaveFailed");

    public static AppError ResourceProcessorCatalogDeleteFailed() =>
        new(
            code: "Bundle.ResourceProcessor.Catalog.DeleteFailed",
            category: ErrorCategory.IoFailure,
            severity: ErrorSeverity.Error,
            messageToken: "Error.Bundle.ResourceProcessor.Catalog.DeleteFailed");

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

    public static AppError NoRedoReplacement(AssetId assetId) =>
        new(
            code: "Asset.Replace.NoRedoChange",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.Replace.NoRedoChange",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    public static AppError WorkspaceOperationRecordNotFound(string operationId) =>
        new(
            code: "Workspace.OperationRecord.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.OperationRecord.NotFound",
            parameters: CreateParameters(("OperationId", operationId)),
            isUserActionable: true);

    public static AppError WorkspaceOperationRecordDeleteNotAllowed(string operationId) =>
        new(
            code: "Workspace.OperationRecord.DeleteNotAllowed",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Workspace.OperationRecord.DeleteNotAllowed",
            parameters: CreateParameters(("OperationId", operationId)),
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

    public static AppError AssetEditDraftWorkspaceKeyRequired() =>
        new(
            code: "Asset.EditDraft.WorkspaceKeyRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.WorkspaceKeyRequired");

    public static AppError AssetEditDraftReferenceRequired() =>
        new(
            code: "Asset.EditDraft.ReferenceRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.ReferenceRequired");

    public static AppError AssetEditDraftIdRequired() =>
        new(
            code: "Asset.EditDraft.IdRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.IdRequired");

    public static AppError AssetEditDraftModeRequired() =>
        new(
            code: "Asset.EditDraft.ModeRequired",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.ModeRequired");

    public static AppError AssetEditDraftInvalidGeometry(AssetId assetId) =>
        new(
            code: "Asset.EditDraft.InvalidGeometry",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.InvalidGeometry",
            parameters: CreateParameters(("AssetId", assetId.ToString())),
            isUserActionable: true);

    public static AppError AssetEditAssociatedAssetNotFound(AssetId ownerAssetId, AssetId associatedAssetId) =>
        new(
            code: "Asset.EditDraft.AssociatedAssetNotFound",
            category: ErrorCategory.Validation,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.AssociatedAssetNotFound",
            parameters: CreateParameters(
                ("OwnerAssetId", ownerAssetId.ToString()),
                ("AssociatedAssetId", associatedAssetId.ToString())),
            isUserActionable: true);

    public static AppError AssetEditDraftNotFound(
        string workspaceKey,
        string draftId,
        string draftMode) =>
        new(
            code: "Asset.EditDraft.NotFound",
            category: ErrorCategory.NotFound,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.NotFound",
            parameters: CreateParameters(
                ("WorkspaceKey", workspaceKey),
                ("DraftId", draftId),
                ("DraftMode", draftMode)),
            isUserActionable: true);

    public static AppError AssetEditDraftSourceMismatch(
        string workspaceKey,
        string draftId,
        string draftMode) =>
        new(
            code: "Asset.EditDraft.SourceMismatch",
            category: ErrorCategory.Conflict,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.SourceMismatch",
            parameters: CreateParameters(
                ("WorkspaceKey", workspaceKey),
                ("DraftId", draftId),
                ("DraftMode", draftMode)),
            isUserActionable: true);

    public static AppError AssetEditDraftExpired(
        string workspaceKey,
        string draftId,
        string draftMode) =>
        new(
            code: "Asset.EditDraft.Expired",
            category: ErrorCategory.Conflict,
            severity: ErrorSeverity.Warning,
            messageToken: "Error.Asset.EditDraft.Expired",
            parameters: CreateParameters(
                ("WorkspaceKey", workspaceKey),
                ("DraftId", draftId),
                ("DraftMode", draftMode)),
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
