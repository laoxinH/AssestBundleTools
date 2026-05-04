using System.Security.Cryptography;
using System.Text;

namespace AssestBundleTools.Infrastructure.Bundles.Internal;

internal static class AssetsToolsAssetIdFactory
{
    public static string CreateAssetId(string serializedFileName, long pathId)
    {
        var normalizedKey = $"{serializedFileName.ToLowerInvariant()}::{pathId}";
        var bytes = Encoding.UTF8.GetBytes(normalizedKey);
        var hash = SHA256.HashData(bytes);
        return $"asset.{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
