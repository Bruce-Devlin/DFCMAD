using DFCMAD.Core.Enforcement;
using DFCMAD.Core.Models;
using DFCMAD.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DFCMAD.Tests.Enforcement;

public sealed class AudioEnforcementServiceTests
{
    [Fact]
    public async Task EnforceNowAsync_SetsPreferredOutput_WhenDefaultDiffers()
    {
        var audio = CreateAudio();
        audio.Devices.Add(Device("out-preferred", "Speakers", AudioDirection.Output));
        audio.Devices.Add(Device("out-other", "Monitor", AudioDirection.Output));
        SetAllDefaults(audio, AudioDirection.Output, "out-other");
        var settings = CreateSettings();
        settings.Current.PreferredOutputEndpointId = "out-preferred";
        settings.Current.EnforceInputEnabled = false;
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        var call = Assert.Single(audio.SetCalls);
        Assert.Equal("out-preferred", call.EndpointId);
        Assert.Equal(AudioDirection.Output, call.Direction);
        Assert.Equal([AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications], call.Roles);
    }

    [Fact]
    public async Task EnforceNowAsync_SetsPreferredInput_WhenDefaultDiffers()
    {
        var audio = CreateAudio();
        audio.Devices.Add(Device("in-preferred", "Mic", AudioDirection.Input));
        audio.Devices.Add(Device("in-other", "Webcam Mic", AudioDirection.Input));
        SetAllDefaults(audio, AudioDirection.Input, "in-other");
        var settings = CreateSettings();
        settings.Current.EnforceOutputEnabled = false;
        settings.Current.PreferredInputEndpointId = "in-preferred";
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        var call = Assert.Single(audio.SetCalls);
        Assert.Equal("in-preferred", call.EndpointId);
        Assert.Equal(AudioDirection.Input, call.Direction);
        Assert.Equal([AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications], call.Roles);
    }

    [Fact]
    public async Task EnforceNowAsync_Skips_WhenPaused()
    {
        var audio = CreateAudio();
        audio.Devices.Add(Device("out-preferred", "Speakers", AudioDirection.Output));
        audio.Devices.Add(Device("out-other", "Monitor", AudioDirection.Output));
        SetAllDefaults(audio, AudioDirection.Output, "out-other");
        var settings = CreateSettings();
        settings.Current.PreferredOutputEndpointId = "out-preferred";
        settings.Current.EnforceInputEnabled = false;
        settings.Current.EnforcementPausedUntil = DateTimeOffset.UtcNow.AddMinutes(10);
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        Assert.Empty(audio.SetCalls);
        Assert.Equal(EnforcementStatus.Paused, service.State.Status);
    }

    [Fact]
    public async Task EnforceNowAsync_Waits_WhenPreferredDeviceMissing()
    {
        var audio = CreateAudio();
        audio.Devices.Add(Device("out-other", "Monitor", AudioDirection.Output));
        SetAllDefaults(audio, AudioDirection.Output, "out-other");
        var settings = CreateSettings();
        settings.Current.PreferredOutputEndpointId = "out-missing";
        settings.Current.EnforceInputEnabled = false;
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        Assert.Empty(audio.SetCalls);
        Assert.Equal(EnforcementStatus.WaitingForDevice, service.State.Status);
    }

    [Fact]
    public async Task EnforceNowAsync_DoesNotSet_WhenAlreadyCorrect()
    {
        var audio = CreateAudio();
        audio.Devices.Add(Device("out-preferred", "Speakers", AudioDirection.Output));
        SetAllDefaults(audio, AudioDirection.Output, "out-preferred");
        var settings = CreateSettings();
        settings.Current.PreferredOutputEndpointId = "out-preferred";
        settings.Current.EnforceInputEnabled = false;
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        Assert.Empty(audio.SetCalls);
        Assert.Equal(EnforcementStatus.Enforcing, service.State.Status);
    }

    [Fact]
    public async Task EnforceNowAsync_UsesConsoleOnly_WhenAllRolesDisabled()
    {
        var audio = CreateAudio();
        audio.Devices.Add(Device("out-preferred", "Speakers", AudioDirection.Output));
        audio.Devices.Add(Device("out-other", "Monitor", AudioDirection.Output));
        SetAllDefaults(audio, AudioDirection.Output, "out-other");
        var settings = CreateSettings();
        settings.Current.PreferredOutputEndpointId = "out-preferred";
        settings.Current.EnforceInputEnabled = false;
        settings.Current.EnforceAllRolesEnabled = false;
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        var call = Assert.Single(audio.SetCalls);
        Assert.Equal([AudioRole.Console], call.Roles);
    }

    [Fact]
    public async Task EnforceNowAsync_HandlesSetFailure()
    {
        var audio = CreateAudio();
        audio.ThrowOnSet = true;
        audio.Devices.Add(Device("out-preferred", "Speakers", AudioDirection.Output));
        audio.Devices.Add(Device("out-other", "Monitor", AudioDirection.Output));
        SetAllDefaults(audio, AudioDirection.Output, "out-other");
        var settings = CreateSettings();
        settings.Current.PreferredOutputEndpointId = "out-preferred";
        settings.Current.EnforceInputEnabled = false;
        var service = CreateService(audio, settings);

        await service.EnforceNowAsync(EnforcementReason.Manual, CancellationToken.None);

        Assert.Equal(EnforcementStatus.Error, service.State.Status);
        Assert.Contains(service.RecentEvents, item => !item.Succeeded);
    }

    private static AudioEnforcementService CreateService(FakeAudioDeviceService audio, FakeSettingsService settings)
    {
        return new AudioEnforcementService(audio, settings, NullLogger<AudioEnforcementService>.Instance);
    }

    private static FakeAudioDeviceService CreateAudio() => new();

    private static FakeSettingsService CreateSettings() => new()
    {
        Current = new AppSettings
        {
            EnforceAllRolesEnabled = true,
            EnforceOutputEnabled = true,
            EnforceInputEnabled = true
        }
    };

    private static AudioDeviceInfo Device(string id, string name, AudioDirection direction, AudioDeviceState state = AudioDeviceState.Active)
    {
        return new AudioDeviceInfo
        {
            EndpointId = id,
            FriendlyName = name,
            Direction = direction,
            State = state
        };
    }

    private static void SetAllDefaults(FakeAudioDeviceService audio, AudioDirection direction, string endpointId)
    {
        audio.SetDefault(direction, AudioRole.Console, endpointId);
        audio.SetDefault(direction, AudioRole.Multimedia, endpointId);
        audio.SetDefault(direction, AudioRole.Communications, endpointId);
    }
}
