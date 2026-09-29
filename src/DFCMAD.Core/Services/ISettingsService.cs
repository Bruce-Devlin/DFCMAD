using DFCMAD.Core.Models;

namespace DFCMAD.Core.Services;

public interface ISettingsService
{
    AppSettings Current { get; }
    string SettingsFilePath { get; }
    string? LastWarning { get; }

    event EventHandler<AppSettingsChangedEventArgs>? SettingsChanged;

    Task LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}
