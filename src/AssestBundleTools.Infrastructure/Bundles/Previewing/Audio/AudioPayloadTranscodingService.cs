using AssestBundleTools.Application.Assets.Dtos;
using AssestBundleTools.Application.Assets.Services;

namespace AssestBundleTools.Infrastructure.Bundles.Previewing.Audio;

/// <summary>
/// 协调可用音频转码器，并向应用层暴露统一的转码入口。
/// </summary>
public sealed class AudioPayloadTranscodingService : IAudioPayloadTranscodingService
{
    private readonly IReadOnlyList<IAudioPayloadTranscoderBackend> _backends;
    private readonly Dictionary<string, IAudioPayloadTranscoderBackend> _backendsById;

    public AudioPayloadTranscodingService()
        : this(CreateDefaultBackends())
    {
    }

    internal AudioPayloadTranscodingService(IEnumerable<IAudioPayloadTranscoderBackend> backends)
    {
        _backends = backends
            .GroupBy(static backend => backend.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

        _backendsById = _backends.ToDictionary(
            static backend => backend.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<AudioPayloadTranscoderDescriptorDto> GetAvailableDescriptors()
    {
        return _backends
            .Select(static backend => backend.TryResolveDescriptor())
            .Where(static descriptor => descriptor is not null)
            .Cast<AudioPayloadTranscoderDescriptorDto>()
            .ToArray();
    }

    public AudioPayloadTranscoderDescriptorDto? ResolveDescriptor(string transcoderId)
    {
        if (string.IsNullOrWhiteSpace(transcoderId))
        {
            return null;
        }

        return _backendsById.TryGetValue(transcoderId.Trim(), out var backend)
            ? backend.TryResolveDescriptor()
            : null;
    }

    public async Task<AudioPayloadTranscodeResultDto> TryTranscodeAsync(
        AudioPayloadTranscodeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        AudioPayloadTranscodeResultDto? unavailableResult = null;

        foreach (var backend in _backends)
        {
            if (!backend.CanTranscode(request))
            {
                continue;
            }

            var result = await backend.TryTranscodeAsync(request, cancellationToken);
            if (result.Status == AudioPayloadTranscodeStatusKind.Unavailable)
            {
                unavailableResult ??= result;
                continue;
            }

            return result;
        }

        return unavailableResult ?? new AudioPayloadTranscodeResultDto(
            Status: AudioPayloadTranscodeStatusKind.Unsupported,
            Issue: AudioPayloadTranscodeIssueKind.None,
            Transcoder: null,
            ContentType: null,
            Content: null,
            Message: null);
    }

    private static IReadOnlyList<IAudioPayloadTranscoderBackend> CreateDefaultBackends()
    {
        return
        [
            new VgmstreamCliAudioPayloadTranscoder()
        ];
    }
}

/// <summary>
/// Infrastructure 内部转码器协议，用于承载具体工具适配。
/// </summary>
internal interface IAudioPayloadTranscoderBackend
{
    string Id { get; }

    bool CanTranscode(AudioPayloadTranscodeRequestDto request);

    AudioPayloadTranscoderDescriptorDto? TryResolveDescriptor();

    Task<AudioPayloadTranscodeResultDto> TryTranscodeAsync(
        AudioPayloadTranscodeRequestDto request,
        CancellationToken cancellationToken = default);
}
