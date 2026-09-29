using DFCMAD.Core.Models;
using DFCMAD.WindowsAudio.ComInterop;
using NAudio.CoreAudioApi;

namespace DFCMAD.WindowsAudio.CoreAudio;

internal static class AudioNativeMapping
{
    public static EDataFlow ToNative(this AudioDirection direction) =>
        direction == AudioDirection.Output ? EDataFlow.eRender : EDataFlow.eCapture;

    public static ERole ToNative(this AudioRole role) =>
        role switch
        {
            AudioRole.Console => ERole.eConsole,
            AudioRole.Multimedia => ERole.eMultimedia,
            AudioRole.Communications => ERole.eCommunications,
            _ => ERole.eConsole
        };

    public static DataFlow ToNAudio(this AudioDirection direction) =>
        direction == AudioDirection.Output ? DataFlow.Render : DataFlow.Capture;

    public static Role ToNAudio(this AudioRole role) =>
        role switch
        {
            AudioRole.Console => Role.Console,
            AudioRole.Multimedia => Role.Multimedia,
            AudioRole.Communications => Role.Communications,
            _ => Role.Console
        };

    public static AudioDirection ToModel(this DataFlow dataFlow) =>
        dataFlow == DataFlow.Capture ? AudioDirection.Input : AudioDirection.Output;

    public static AudioRole ToModel(this Role role) =>
        role switch
        {
            Role.Console => AudioRole.Console,
            Role.Multimedia => AudioRole.Multimedia,
            Role.Communications => AudioRole.Communications,
            _ => AudioRole.Console
        };

    public static DFCMAD.Core.Models.AudioDeviceState ToModel(this NAudio.CoreAudioApi.DeviceState state)
    {
        if (state.HasFlag(NAudio.CoreAudioApi.DeviceState.Active))
        {
            return DFCMAD.Core.Models.AudioDeviceState.Active;
        }

        if (state.HasFlag(NAudio.CoreAudioApi.DeviceState.Disabled))
        {
            return DFCMAD.Core.Models.AudioDeviceState.Disabled;
        }

        if (state.HasFlag(NAudio.CoreAudioApi.DeviceState.NotPresent))
        {
            return DFCMAD.Core.Models.AudioDeviceState.NotPresent;
        }

        if (state.HasFlag(NAudio.CoreAudioApi.DeviceState.Unplugged))
        {
            return DFCMAD.Core.Models.AudioDeviceState.Unplugged;
        }

        return DFCMAD.Core.Models.AudioDeviceState.Unknown;
    }
}
