using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Common.Messages;
using AssestBundleTools.Application.Workspace.Dtos;

namespace AssestBundleTools.Application.Workspace.Internal;

internal sealed record WorkspaceBundleArtifactResolutionDto(
    string ResolvedBundlePath,
    BundleArtifactManifestDto Manifest,
    AssetBundleDecryptorDescriptorDto Decryptor,
    bool IsFromCache,
    LocalizedMessageDto? DiagnosticMessage);
