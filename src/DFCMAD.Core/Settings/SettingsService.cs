using System.Text.Json;
using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;

namespace DFCMAD.Core.Settings;

public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IAppPaths _paths;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(IAppPaths paths, ILogger<SettingsService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public AppSettings Current { get; private set; } = new();
    public string SettingsFilePath => _paths.SettingsFilePath;
    public string? LastWarning { get; private set; }

    public event EventHandler<AppSettingsChangedEventArgs>? SettingsChanged;

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.AppDataDirectory);

            if (!File.Exists(_paths.SettingsFilePath))
            {
                Current = new AppSettings();
                LastWarning = null;
                await SaveCurrentUnsafeAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Settings file did not exist. Created default settings at {SettingsFile}", _paths.SettingsFilePath);
                SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(Current));
                return;
            }

            await using var stream = File.OpenRead(_paths.SettingsFilePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            Current = Normalize(settings ?? new AppSettings());
            LastWarning = null;
            _logger.LogInformation("Settings loaded from {SettingsFile}", _paths.SettingsFilePath);
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(Current));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var backup = BackupCorruptSettings();
            LastWarning = $"Settings were corrupt or unreadable and were reset. Backup: {backup}";
            Current = new AppSettings();
            await SaveCurrentUnsafeAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning(ex, "Settings load failed. A new default settings file was created.");
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(Current));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Current = Normalize(Current);
            await SaveCurrentUnsafeAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Settings saved to {SettingsFile}", _paths.SettingsFilePath);
            SettingsChanged?.Invoke(this, new AppSettingsChangedEventArgs(Current));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task SaveCurrentUnsafeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.AppDataDirectory);

        var temporaryPath = $"{_paths.SettingsFilePath}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        if (File.Exists(_paths.SettingsFilePath))
        {
            File.Replace(temporaryPath, _paths.SettingsFilePath, null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(temporaryPath, _paths.SettingsFilePath);
        }
    }

    private string BackupCorruptSettings()
    {
        if (!File.Exists(_paths.SettingsFilePath))
        {
            return string.Empty;
        }

        var backupPath = $"{_paths.SettingsFilePath}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.bak";
        try
        {
            File.Copy(_paths.SettingsFilePath, backupPath, overwrite: true);
            return backupPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not back up corrupt settings file.");
            return "backup failed";
        }
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.AppVersion = AppVersionInfo.Version;
        settings.Fallback ??= new FallbackSettings();
        settings.LastKnownDevices ??= [];
        settings.UiPreferences ??= new UiPreferences();
        settings.DiagnosticsPreferences ??= new DiagnosticsPreferences();
        if (settings.DiagnosticsPreferences.EventCount <= 0)
        {
            settings.DiagnosticsPreferences.EventCount = 20;
        }

        return settings;
    }
}
