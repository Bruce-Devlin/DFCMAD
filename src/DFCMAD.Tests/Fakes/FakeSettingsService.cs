using DFCMAD.Core.Models;
using DFCMAD.Core.Services;

namespace DFCMAD.Tests.Fakes;

internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Current { get; set; } = new();
    public string SettingsFilePath => "fake-settings.json";
    public string? LastWarning => null;
    public int SaveCount { get; private set; }

    public event EventHandler<AppSettingsChangedEventArgs>? SettingsChanged;

    public Task LoadAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(Current));
        return Task.CompletedTask;
    }
}
