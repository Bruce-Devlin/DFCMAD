using DFCMAD.Core.Models;

namespace DFCMAD.Core.Services;

public interface IAudioDeviceService
{
    Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken);
    Task<AudioDeviceInfo?> GetDefaultDeviceAsync(AudioDirection direction, AudioRole role, CancellationToken cancellationToken);
    Task SetDefaultDeviceAsync(string endpointId, AudioDirection direction, IReadOnlyCollection<AudioRole> roles, CancellationToken cancellationToken);

    event EventHandler<AudioDeviceChangedEventArgs>? DeviceChanged;
    event EventHandler<DefaultAudioDeviceChangedEventArgs>? DefaultDeviceChanged;
}
