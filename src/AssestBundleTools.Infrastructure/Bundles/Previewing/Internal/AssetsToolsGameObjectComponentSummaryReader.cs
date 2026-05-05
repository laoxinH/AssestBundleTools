using AssetsTools.NET;
using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Infrastructure.Bundles.Internal;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Internal;

internal static class AssetsToolsGameObjectComponentSummaryReader
{
    private const int MaxSummaryCount = 12;

    public static IReadOnlyList<AssetPreviewGameObjectComponentSummaryDto> ReadComponentSummaries(
        AssetTypeValueField componentField,
        string? typeName,
        string? scriptName,
        IReadOnlyList<AssetPreviewGameObjectReferenceDto> references)
    {
        ArgumentNullException.ThrowIfNull(componentField);
        ArgumentNullException.ThrowIfNull(references);

        var summaries = new List<AssetPreviewGameObjectComponentSummaryDto>(MaxSummaryCount);
        AddCommonSummary(summaries, componentField, scriptName);

        if (HasTypeName(typeName, static value => value.EndsWith("Renderer", StringComparison.OrdinalIgnoreCase)))
        {
            AddRendererSummary(summaries, componentField, references);
        }

        if (HasTypeName(typeName, static value => string.Equals(value, "MeshFilter", StringComparison.OrdinalIgnoreCase)))
        {
            AddReferenceNameSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.MeshReference, references, "mesh");
        }

        if (HasTypeName(typeName, static value => value.Contains("Animator", StringComparison.OrdinalIgnoreCase)))
        {
            AddAnimatorSummary(summaries, componentField, references);
        }

        if (HasTypeName(typeName, static value => value.Contains("AudioSource", StringComparison.OrdinalIgnoreCase)))
        {
            AddAudioSourceSummary(summaries, componentField, references);
        }

        if (HasTypeName(typeName, static value => value.Contains("Collider", StringComparison.OrdinalIgnoreCase)))
        {
            AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.IsTrigger, componentField, "m_IsTrigger");
        }

        if (HasTypeName(typeName, static value => value.Contains("ParticleSystem", StringComparison.OrdinalIgnoreCase)))
        {
            AddParticleSystemSummary(summaries, componentField);
        }

        if (HasTypeName(typeName, static value => value.Contains("SpriteRenderer", StringComparison.OrdinalIgnoreCase)))
        {
            AddReferenceNameSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.SpriteReference, references, "sprite");
        }

        return summaries
            .DistinctBy(static summary => summary.Key)
            .Take(MaxSummaryCount)
            .ToArray();
    }

    private static void AddCommonSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetTypeValueField componentField,
        string? scriptName)
    {
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.Enabled, componentField, "m_Enabled");
        if (!string.IsNullOrWhiteSpace(scriptName))
        {
            summaries.Add(CreateStringSummary(
                AssetPreviewGameObjectComponentSummaryKey.Script,
                scriptName.Trim()));
        }
    }

    private static void AddRendererSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetTypeValueField componentField,
        IReadOnlyList<AssetPreviewGameObjectReferenceDto> references)
    {
        var materialCount = references.Count(static reference =>
            string.Equals(reference.Role, "material", StringComparison.OrdinalIgnoreCase));
        if (materialCount > 0)
        {
            summaries.Add(CreateIntegerSummary(
                AssetPreviewGameObjectComponentSummaryKey.MaterialCount,
                materialCount));
        }

        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.CastShadows, componentField, "m_CastShadows");
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.ReceiveShadows, componentField, "m_ReceiveShadows");
    }

    private static void AddAnimatorSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetTypeValueField componentField,
        IReadOnlyList<AssetPreviewGameObjectReferenceDto> references)
    {
        AddReferenceNameSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.Controller, references, "controller");
        AddReferenceNameSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.Avatar, references, "avatar");
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.RootMotion, componentField, "m_ApplyRootMotion");
    }

    private static void AddAudioSourceSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetTypeValueField componentField,
        IReadOnlyList<AssetPreviewGameObjectReferenceDto> references)
    {
        AddReferenceNameSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.AudioClip, references, "audio-clip");
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.Loop, componentField, "Loop", "m_Loop", "loop");
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.PlayOnAwake, componentField, "m_PlayOnAwake", "playOnAwake");
        AddDoubleSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.Volume, componentField, "m_Volume", "volume");
    }

    private static void AddParticleSystemSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetTypeValueField componentField)
    {
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.ParticleLooping, componentField, "looping", "m_Looping");
        AddBooleanSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.ParticlePrewarm, componentField, "prewarm", "m_Prewarm");
        AddDoubleSummary(summaries, AssetPreviewGameObjectComponentSummaryKey.ParticleDuration, componentField, "lengthInSec", "m_LengthInSec");
    }

    private static void AddReferenceNameSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetPreviewGameObjectComponentSummaryKey key,
        IReadOnlyList<AssetPreviewGameObjectReferenceDto> references,
        string role)
    {
        var reference = references.FirstOrDefault(reference =>
            string.Equals(reference.Role, role, StringComparison.OrdinalIgnoreCase));
        if (reference is null)
        {
            return;
        }

        var value = !string.IsNullOrWhiteSpace(reference.Name)
            ? reference.Name.Trim()
            : reference.PathId?.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(value))
        {
            summaries.Add(CreateStringSummary(key, value));
        }
    }

    private static void AddBooleanSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetPreviewGameObjectComponentSummaryKey key,
        AssetTypeValueField componentField,
        params string[] fieldNames)
    {
        var value = ReadBoolean(componentField, fieldNames);
        if (value.HasValue)
        {
            summaries.Add(new AssetPreviewGameObjectComponentSummaryDto(
                key,
                AssetPreviewInfoValueKind.Boolean,
                BooleanValue: value.Value));
        }
    }

    private static void AddDoubleSummary(
        ICollection<AssetPreviewGameObjectComponentSummaryDto> summaries,
        AssetPreviewGameObjectComponentSummaryKey key,
        AssetTypeValueField componentField,
        params string[] fieldNames)
    {
        var value = ReadDouble(componentField, fieldNames);
        if (value.HasValue)
        {
            summaries.Add(new AssetPreviewGameObjectComponentSummaryDto(
                key,
                AssetPreviewInfoValueKind.Decimal,
                DoubleValue: value.Value));
        }
    }

    private static bool? ReadBoolean(AssetTypeValueField componentField, IReadOnlyList<string> fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var field = AssetsToolsFieldReader.TryGetField(componentField, fieldName);
            var value = AssetsToolsFieldReader.ReadBoolean(field);
            if (value.HasValue)
            {
                return value.Value;
            }

            var numericValue = AssetsToolsFieldReader.ReadInt32(field);
            if (numericValue.HasValue)
            {
                return numericValue.Value != 0;
            }
        }

        return null;
    }

    private static double? ReadDouble(AssetTypeValueField componentField, IReadOnlyList<string> fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var value = AssetsToolsFieldReader.ReadFloat(AssetsToolsFieldReader.TryGetField(componentField, fieldName));
            if (value.HasValue)
            {
                return value.Value;
            }
        }

        return null;
    }

    private static AssetPreviewGameObjectComponentSummaryDto CreateIntegerSummary(
        AssetPreviewGameObjectComponentSummaryKey key,
        long value) =>
        new(key, AssetPreviewInfoValueKind.Integer, Int64Value: value);

    private static AssetPreviewGameObjectComponentSummaryDto CreateStringSummary(
        AssetPreviewGameObjectComponentSummaryKey key,
        string value) =>
        new(key, AssetPreviewInfoValueKind.Text, StringValue: value);

    private static bool HasTypeName(string? typeName, Func<string, bool> predicate) =>
        !string.IsNullOrWhiteSpace(typeName) && predicate(typeName.Trim());
}
