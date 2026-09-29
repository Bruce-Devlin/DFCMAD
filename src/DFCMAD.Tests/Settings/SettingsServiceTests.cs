using DFCMAD.Core.Models;
using DFCMAD.Core.Settings;
using DFCMAD.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DFCMAD.Tests.Settings;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_CreatesDefaultSettings_WhenFileIsMissing()
    {
        using var temp = new TempDirectory();
        var service = new SettingsService(new FakePaths(temp.Path), NullLogger<SettingsService>.Instance);

        await service.LoadAsync(CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(temp.Path, "settings.json")));
        Assert.True(service.Current.EnforceOutputEnabled);
        Assert.True(service.Current.EnforceInputEnabled);
    }

    [Fact]
    public async Task SaveAsync_PersistsAndReloadsSettings()
    {
        using var temp = new TempDirectory();
        var paths = new FakePaths(temp.Path);
        var service = new SettingsService(paths, NullLogger<SettingsService>.Instance);
        await service.LoadAsync(CancellationToken.None);

        service.Current.PreferredOutputEndpointId = "output-1";
        service.Current.PreferredOutputDisplayNameSnapshot = "Speakers";
        service.Current.FirstRunCompleted = true;
        await service.SaveAsync(CancellationToken.None);

        var reloaded = new SettingsService(paths, NullLogger<SettingsService>.Instance);
        await reloaded.LoadAsync(CancellationToken.None);

        Assert.Equal("output-1", reloaded.Current.PreferredOutputEndpointId);
        Assert.Equal("Speakers", reloaded.Current.PreferredOutputDisplayNameSnapshot);
        Assert.True(reloaded.Current.FirstRunCompleted);
    }

    [Fact]
    public async Task LoadAsync_BacksUpCorruptSettingsAndRegenerates()
    {
        using var temp = new TempDirectory();
        var paths = new FakePaths(temp.Path);
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllTextAsync(paths.SettingsFilePath, "{ definitely not json");
        var service = new SettingsService(paths, NullLogger<SettingsService>.Instance);

        await service.LoadAsync(CancellationToken.None);

        Assert.NotNull(service.LastWarning);
        Assert.Contains("corrupt", service.LastWarning, StringComparison.OrdinalIgnoreCase);
        Assert.Single(Directory.GetFiles(temp.Path, "*.bak"));
        Assert.True(File.Exists(paths.SettingsFilePath));
    }

    [Fact]
    public async Task SaveAsync_UsesAtomicWriteWithoutLeavingTempFile()
    {
        using var temp = new TempDirectory();
        var paths = new FakePaths(temp.Path);
        var service = new SettingsService(paths, NullLogger<SettingsService>.Instance);
        await service.LoadAsync(CancellationToken.None);

        service.Current.LastKnownDevices.Add(new KnownAudioDevice
        {
            EndpointId = "device",
            FriendlyName = "Device",
            Direction = AudioDirection.Output,
            LastState = AudioDeviceState.Active
        });
        await service.SaveAsync(CancellationToken.None);

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
        Assert.True(new FileInfo(paths.SettingsFilePath).Length > 0);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dfcmad-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
