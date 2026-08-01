using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Decryption;

internal static class BuiltInCrossCoreDecryptorFactory
{
    private const string Version = "2.0.0";
    private const string ExecutableFileName = @".\AssestBundleTools.BundleCodec.CrossCore.exe";

    public static IAssetBundleDecryptor CreateDecryptor()
    {
        var applicationDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "codecs",
            "crosscore");
        return new ExternalProcessAssetBundleDecryptor(
            CreateManifest(),
            applicationDirectory,
            AssetBundleDecryptorKind.BuiltIn);
    }

    public static AssetBundleDecryptorCatalogEntryDto CreateCatalogEntry()
    {
        var manifest = CreateManifest();
        return new AssetBundleDecryptorCatalogEntryDto(
            Definition: new AssetBundleDecryptorDefinitionDto(
                Id: manifest.Id,
                DisplayName: manifest.DisplayName,
                Version: manifest.Version,
                Description: manifest.Description,
                SupportsAutoDetection: manifest.SupportsAutoDetection,
                IsFallback: manifest.IsFallback,
                ExecutablePath: manifest.ExecutablePath,
                WorkingDirectoryPath: manifest.WorkingDirectoryPath,
                TimeoutMs: manifest.TimeoutMs,
                ProbeArguments: manifest.ProbeArguments,
                DecryptArguments: manifest.DecryptArguments,
                EncryptArguments: manifest.EncryptArguments),
            Kind: AssetBundleDecryptorKind.BuiltIn,
            IsBuiltIn: true,
            IsReadOnly: true,
            ManifestFilePath: null);
    }

    private static AssetBundleExternalProcessDecryptorManifest CreateManifest() =>
        new()
        {
            SchemaVersion = 1,
            Kind = "assetBundleCodec",
            Id = AssetBundleDecryptorIds.BuiltInCrossCoreDuplicatedHeader,
            DisplayName = AssetBundleDecryptorIds.BuiltInCrossCoreDuplicatedHeader,
            Version = Version,
            Description = AssetBundleDecryptorIds.BuiltInCrossCoreDuplicatedHeader,
            SupportsAutoDetection = true,
            IsFallback = false,
            ExecutablePath = ExecutableFileName,
            TimeoutMs = 120_000,
            ProbeArguments =
            [
                "probe",
                "--input",
                "{inputPath}",
                "--result",
                "{resultPath}"
            ],
            DecryptArguments =
            [
                "decrypt",
                "--input",
                "{inputPath}",
                "--output",
                "{outputPath}",
                "--result",
                "{resultPath}"
            ],
            EncryptArguments =
            [
                "encrypt",
                "--input",
                "{inputPath}",
                "--output",
                "{outputPath}",
                "--outer-prefix-base64",
                "{parameter:crosscore.outerPrefixBase64}",
                "--result",
                "{resultPath}"
            ]
        };
}
