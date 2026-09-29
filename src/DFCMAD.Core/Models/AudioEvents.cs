namespace DFCMAD.Core.Models;

public sealed class AudioDeviceChangedEventArgs : EventArgs
{
    public AudioDeviceChangedEventArgs(string endpointId, AudioDeviceChangeKind changeKind, AudioDirection? direction, AudioDeviceState? state)
    {
        EndpointId = endpointId;
        ChangeKind = changeKind;
        Direction = direction;
        State = state;
    }

    public string EndpointId { get; }
    public AudioDeviceChangeKind ChangeKind { get; }
    public AudioDirection? Direction { get; }
    public AudioDeviceState? State { get; }
}

public sealed class DefaultAudioDeviceChangedEventArgs : EventArgs
{
    public DefaultAudioDeviceChangedEventArgs(AudioDirection direction, AudioRole role, string? endpointId)
    {
        Direction = direction;
        Role = role;
        EndpointId = endpointId;
    }

    public AudioDirection Direction { get; }
    public AudioRole Role { get; }
    public string? EndpointId { get; }
}

public sealed class AppSettingsChangedEventArgs : EventArgs
{
    public AppSettingsChangedEventArgs(AppSettings settings)
    {
        Settings = settings;
    }

    public AppSettings Settings { get; }
}
