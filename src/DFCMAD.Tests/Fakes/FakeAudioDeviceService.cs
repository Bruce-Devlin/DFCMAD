using DFCMAD.Core.Models;
using DFCMAD.Core.Services;

namespace DFCMAD.Tests.Fakes;

internal sealed class FakeAudioDeviceService : IAudioDeviceService
{
    private readonly Dictionary<(AudioDirection Direction, AudioRole Role), string?> _defaults = [];

    public List<AudioDeviceInfo> Devices { get; } = [];
    public List<SetCall> SetCalls { get; } = [];
    public bool ThrowOnSet { get; set; }

    public event EventHandler<AudioDeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler<DefaultAudioDeviceChangedEventArgs>? DefaultDeviceChanged;

    public Task<IReadOnlyList<AudioDeviceInfo>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult((IReadOnlyList<AudioDeviceInfo>)Devices.ToArray());
    }

    public Task<AudioDeviceInfo?> GetDefaultDeviceAsync(AudioDirection direction, AudioRole role, CancellationToken cancellationToken)
    {
        _defaults.TryGetValue((direction, role), out var endpointId);
        return Task.FromResult(Devices.FirstOrDefault(device => device.Direction == direction && device.EndpointId == endpointId));
    }

    public Task SetDefaultDeviceAsync(string endpointId, AudioDirection direction, IReadOnlyCollection<AudioRole> roles, CancellationToken cancellationToken)
    {
        if (ThrowOnSet)
        {
            throw new InvalidOperationException("set failed");
        }

        SetCalls.Add(new SetCall(endpointId, direction, roles.ToArray()));
        foreach (var role in roles)
        {
            _defaults[(direction, role)] = endpointId;
        }

        return Task.CompletedTask;
    }

    public void SetDefault(AudioDirection direction, AudioRole role, string endpointId)
    {
        _defaults[(direction, role)] = endpointId;
    }

    public void RaiseDeviceChanged(AudioDeviceChangedEventArgs args)
    {
        DeviceChanged?.Invoke(this, args);
    }

    public void RaiseDefaultChanged(DefaultAudioDeviceChangedEventArgs args)
    {
        DefaultDeviceChanged?.Invoke(this, args);
    }

    public sealed record SetCall(string EndpointId, AudioDirection Direction, IReadOnlyCollection<AudioRole> Roles);
}
