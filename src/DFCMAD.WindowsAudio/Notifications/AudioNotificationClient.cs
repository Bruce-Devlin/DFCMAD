using DFCMAD.Core.Models;
using DFCMAD.WindowsAudio.CoreAudio;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace DFCMAD.WindowsAudio.Notifications;

internal sealed class AudioNotificationClient : IMMNotificationClient
{
    public event EventHandler<AudioDeviceChangedEventArgs>? DeviceChanged;
    public event EventHandler<DefaultAudioDeviceChangedEventArgs>? DefaultDeviceChanged;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
    {
        DeviceChanged?.Invoke(
            this,
            new AudioDeviceChangedEventArgs(deviceId, AudioDeviceChangeKind.StateChanged, null, newState.ToModel()));
    }

    public void OnDeviceAdded(string pwstrDeviceId)
    {
        DeviceChanged?.Invoke(this, new AudioDeviceChangedEventArgs(pwstrDeviceId, AudioDeviceChangeKind.Added, null, null));
    }

    public void OnDeviceRemoved(string deviceId)
    {
        DeviceChanged?.Invoke(this, new AudioDeviceChangedEventArgs(deviceId, AudioDeviceChangeKind.Removed, null, null));
    }

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (flow is DataFlow.Render or DataFlow.Capture)
        {
            DefaultDeviceChanged?.Invoke(this, new DefaultAudioDeviceChangedEventArgs(flow.ToModel(), role.ToModel(), defaultDeviceId));
        }
    }

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
    {
        DeviceChanged?.Invoke(this, new AudioDeviceChangedEventArgs(pwstrDeviceId, AudioDeviceChangeKind.PropertyChanged, null, null));
    }
}
