using System.Text.Json;
using System.Text.Json.Serialization;

namespace AssestBundleTools.Infrastructure.Workspace.Persistence;

/// <summary>
/// 统一提供工作区相关 JSON 落盘的序列化选项。
/// </summary>
internal static class JsonWorkspaceSerialization
{
    public static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }
}
