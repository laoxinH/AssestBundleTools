using AssestBundleTools.Application.Bundles;
using AssestBundleTools.Application.Bundles.Dtos;
using AssestBundleTools.Application.Bundles.Services;
using AssestBundleTools.Presentation.WinUI.Localization;

namespace AssestBundleTools.Presentation.WinUI.Services;

/// <summary>
/// 统一处理解密器在 Presentation 层的显示文本。
/// </summary>
public static class AssetBundleDecryptorDisplayText
{
    public static string GetDisplayName(
        ILocalizationService localizationService,
        string? decryptorId)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        if (string.Equals(
                decryptorId?.Trim(),
                AssetBundleDecryptorIds.BuiltInAutoDetect,
                StringComparison.OrdinalIgnoreCase))
        {
            return localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInAutoDetectDisplayName);
        }

        if (string.Equals(
                decryptorId?.Trim(),
                AssetBundleDecryptorIds.BuiltInPassThrough,
                StringComparison.OrdinalIgnoreCase))
        {
            return localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInPassThroughDisplayName);
        }

        return string.IsNullOrWhiteSpace(decryptorId)
            ? localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInPassThroughDisplayName)
            : decryptorId.Trim();
    }

    public static string GetDisplayName(
        ILocalizationService localizationService,
        IAssetBundleDecryptorRegistry decryptorRegistry,
        string? decryptorId)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(decryptorRegistry);

        var descriptor = ResolveDescriptor(localizationService, decryptorRegistry, decryptorId);
        return GetDisplayName(localizationService, descriptor);
    }

    public static string GetDisplayName(
        ILocalizationService localizationService,
        AssetBundleDecryptorDescriptorDto descriptor)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (IsBuiltInAutoDetect(descriptor))
        {
            return localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInAutoDetectDisplayName);
        }

        return IsBuiltInPassThrough(descriptor)
            ? localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInPassThroughDisplayName)
            : descriptor.DisplayName;
    }

    public static string GetDescription(
        ILocalizationService localizationService,
        IAssetBundleDecryptorRegistry decryptorRegistry,
        string? decryptorId)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(decryptorRegistry);

        var descriptor = ResolveDescriptor(localizationService, decryptorRegistry, decryptorId);
        return GetDescription(localizationService, descriptor);
    }

    public static string GetDescription(
        ILocalizationService localizationService,
        AssetBundleDecryptorDescriptorDto descriptor)
    {
        ArgumentNullException.ThrowIfNull(localizationService);
        ArgumentNullException.ThrowIfNull(descriptor);

        if (IsBuiltInAutoDetect(descriptor))
        {
            return localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInAutoDetectDescription);
        }

        if (IsBuiltInPassThrough(descriptor))
        {
            return localizationService.GetRequiredString(ResourceKeys.AssetBundleDecryptorBuiltInPassThroughDescription);
        }

        return string.IsNullOrWhiteSpace(descriptor.Description)
            ? descriptor.DisplayName
            : descriptor.Description;
    }

    private static AssetBundleDecryptorDescriptorDto ResolveDescriptor(
        ILocalizationService localizationService,
        IAssetBundleDecryptorRegistry decryptorRegistry,
        string? decryptorId)
    {
        ArgumentNullException.ThrowIfNull(localizationService);

        if (string.Equals(
                decryptorId?.Trim(),
                AssetBundleDecryptorIds.BuiltInAutoDetect,
                StringComparison.OrdinalIgnoreCase))
        {
            return new AssetBundleDecryptorDescriptorDto(
                Id: AssetBundleDecryptorIds.BuiltInAutoDetect,
                DisplayName: AssetBundleDecryptorIds.BuiltInAutoDetect,
                Version: string.Empty,
                Kind: AssetBundleDecryptorKind.BuiltIn,
                SupportsAutoDetection: true,
                IsFallback: false,
                Description: AssetBundleDecryptorIds.BuiltInAutoDetect);
        }

        if (!string.IsNullOrWhiteSpace(decryptorId) &&
            decryptorRegistry.TryGet(decryptorId.Trim(), out var decryptor))
        {
            return decryptor.Descriptor;
        }

        if (!string.IsNullOrWhiteSpace(decryptorId))
        {
            return new AssetBundleDecryptorDescriptorDto(
                Id: decryptorId.Trim(),
                DisplayName: decryptorId.Trim(),
                Version: string.Empty,
                Kind: AssetBundleDecryptorKind.ExternalProcess,
                SupportsAutoDetection: false,
                IsFallback: false,
                Description: decryptorId.Trim());
        }

        return decryptorRegistry.GetAll().FirstOrDefault(static decryptor => decryptor.Descriptor.IsFallback)?.Descriptor
               ?? throw PresentationExceptionFactory.CreateFallbackDecryptorRequiredException(localizationService);
    }

    private static bool IsBuiltInPassThrough(AssetBundleDecryptorDescriptorDto descriptor)
    {
        return descriptor.Kind == AssetBundleDecryptorKind.BuiltIn &&
               string.Equals(descriptor.Id, AssetBundleDecryptorIds.BuiltInPassThrough, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuiltInAutoDetect(AssetBundleDecryptorDescriptorDto descriptor)
    {
        return descriptor.Kind == AssetBundleDecryptorKind.BuiltIn &&
               string.Equals(descriptor.Id, AssetBundleDecryptorIds.BuiltInAutoDetect, StringComparison.OrdinalIgnoreCase);
    }
}
