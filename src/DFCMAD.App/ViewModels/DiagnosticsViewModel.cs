using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;

namespace DFCMAD.App.ViewModels;

public sealed class DiagnosticsViewModel : ObservableObject
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly IAudioEnforcementService _enforcementService;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IAppPaths _paths;
    private readonly ISettingsService _settingsService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly ILogger<DiagnosticsViewModel> _logger;
    private string _diagnosticsText = "Loading diagnostics...";
    private string _deviceSummary = "Devices: loading";
    private string _startupSummary = "Startup: unknown";

    public DiagnosticsViewModel(
        IDiagnosticsService diagnosticsService,
        IAudioEnforcementService enforcementService,
        IAudioDeviceService audioDeviceService,
        IAppPaths paths,
        ISettingsService settingsService,
        IStartupRegistrationService startupRegistrationService,
        ILogger<DiagnosticsViewModel> logger)
    {
        _diagnosticsService = diagnosticsService;
        _enforcementService = enforcementService;
        _audioDeviceService = audioDeviceService;
        _paths = paths;
        _settingsService = settingsService;
        _startupRegistrationService = startupRegistrationService;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CopyCommand = new RelayCommand(CopyDiagnostics);
        OpenLogFolderCommand = new RelayCommand(OpenLogFolder);
        EnforceNowCommand = new AsyncRelayCommand(async token =>
        {
            await _enforcementService.EnforceNowAsync(EnforcementReason.Manual, token).ConfigureAwait(true);
            await RefreshAsync(token).ConfigureAwait(true);
        });
        ForceRefreshDevicesCommand = new AsyncRelayCommand(async token =>
        {
            await _audioDeviceService.GetDevicesAsync(token).ConfigureAwait(true);
            await RefreshAsync(token).ConfigureAwait(true);
        });
    }

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set => SetProperty(ref _diagnosticsText, value);
    }

    public string EnforcementSummary => _enforcementService.State.StatusText;
    public string LastErrorSummary => string.IsNullOrWhiteSpace(_enforcementService.State.LastError) ? "No recent errors" : _enforcementService.State.LastError;
    public string SettingsSummary => _settingsService.Current.FirstRunCompleted ? "Setup complete" : "Setup needed";
    public string LogFolder => _paths.LogDirectory;
    public string SettingsFile => _settingsService.SettingsFilePath;

    public string DeviceSummary
    {
        get => _deviceSummary;
        private set => SetProperty(ref _deviceSummary, value);
    }

    public string StartupSummary
    {
        get => _startupSummary;
        private set => SetProperty(ref _startupSummary, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand OpenLogFolderCommand { get; }
    public ICommand EnforceNowCommand { get; }
    public ICommand ForceRefreshDevicesCommand { get; }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await RefreshSummaryAsync(cancellationToken).ConfigureAwait(true);
            DiagnosticsText = await _diagnosticsService.BuildDiagnosticsAsync(cancellationToken).ConfigureAwait(true);
            OnPropertyChanged(nameof(EnforcementSummary));
            OnPropertyChanged(nameof(LastErrorSummary));
            OnPropertyChanged(nameof(SettingsSummary));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DiagnosticsText = $"Diagnostics failed: {ex}";
            _logger.LogError(ex, "Diagnostics refresh failed.");
        }
    }

    private async Task RefreshSummaryAsync(CancellationToken cancellationToken)
    {
        try
        {
            var devices = await _audioDeviceService.GetDevicesAsync(cancellationToken).ConfigureAwait(true);
            var outputs = devices.Count(device => device.Direction == AudioDirection.Output);
            var inputs = devices.Count(device => device.Direction == AudioDirection.Input);
            DeviceSummary = $"{outputs} outputs, {inputs} inputs";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            DeviceSummary = $"Device refresh failed: {ex.Message}";
        }

        try
        {
            StartupSummary = _startupRegistrationService.IsEnabled() ? "Starts with Windows" : "Startup disabled";
        }
        catch (Exception ex)
        {
            StartupSummary = $"Startup check failed: {ex.Message}";
        }
    }

    private void CopyDiagnostics()
    {
        System.Windows.Clipboard.SetText(DiagnosticsText);
        _logger.LogInformation("Diagnostics copied to clipboard.");
    }

    private void OpenLogFolder()
    {
        Directory.CreateDirectory(_paths.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.LogDirectory,
            UseShellExecute = true
        });
    }
}
