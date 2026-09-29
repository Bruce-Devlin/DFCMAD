using DFCMAD.Core.Diagnostics;
using DFCMAD.Core.Models;
using DFCMAD.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DFCMAD.Tests.Diagnostics;

public sealed class DiagnosticsServiceTests
{
    [Fact]
    public async Task BuildDiagnosticsAsync_IncludesSettingsAndDeviceState()
    {
        var audio = new FakeAudioDeviceService();
        audio.Devices.Add(new AudioDeviceInfo
        {
            EndpointId = "out",
            FriendlyName = "Speakers",
            Direction = AudioDirection.Output,
            State = AudioDeviceState.Active
        });
        audio.SetDefault(AudioDirection.Output, AudioRole.Console, "out");
        var settings = new FakeSettingsService
        {
            Current = new AppSettings
            {
                PreferredOutputEndpointId = "out",
                PreferredOutputDisplayNameSnapshot = "Speakers"
            }
        };
        var diagnostics = CreateDiagnostics(settings, audio);

        var text = await diagnostics.BuildDiagnosticsAsync(CancellationToken.None);

        Assert.Contains("Preferred output endpoint ID: out", text);
        Assert.Contains("Available render devices", text);
        Assert.Contains("Speakers", text);
    }

    [Fact]
    public async Task BuildDiagnosticsAsync_DoesNotThrow_WhenNoDevicesExist()
    {
        var diagnostics = CreateDiagnostics(new FakeSettingsService(), new FakeAudioDeviceService());

        var text = await diagnostics.BuildDiagnosticsAsync(CancellationToken.None);

        Assert.Contains("DFCMAD diagnostics", text);
        Assert.Contains("Available render devices", text);
        Assert.Contains("Available capture devices", text);
    }

    private static DiagnosticsService CreateDiagnostics(FakeSettingsService settings, FakeAudioDeviceService audio)
    {
        var root = Path.Combine(Path.GetTempPath(), $"dfcmad-diagnostics-tests-{Guid.NewGuid():N}");
        var paths = new FakePaths(root);
        return new DiagnosticsService(
            settings,
            new FakeStartupRegistrationService(),
            audio,
            new FakeEnforcementService
            {
                Events =
                {
                    new EnforcementEvent
                    {
                        Reason = EnforcementReason.Manual,
                        Message = "Manual enforce",
                        Succeeded = true
                    }
                }
            },
            paths,
            NullLogger<DiagnosticsService>.Instance);
    }
}
