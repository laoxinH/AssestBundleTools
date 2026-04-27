using AssestBundleTools.Application.Assets.Dtos;

namespace AssestBundleTools.Application.Assets.Services;

/// <summary>
/// 提供音频载荷到标准可播放格式的转码协调入口。
/// </summary>
/// <remarks>
/// 该服务只暴露稳定的“是否可转码、由谁转码、转码结果是什么”语义，
/// 不把外部进程、命令行参数或具体工具路径泄漏给应用层。
/// </remarks>
public interface IAudioPayloadTranscodingService
{
    IReadOnlyList<AudioPayloadTranscoderDescriptorDto> GetAvailableDescriptors();

    AudioPayloadTranscoderDescriptorDto? ResolveDescriptor(string transcoderId);

    Task<AudioPayloadTranscodeResultDto> TryTranscodeAsync(
        AudioPayloadTranscodeRequestDto request,
        CancellationToken cancellationToken = default);
}
