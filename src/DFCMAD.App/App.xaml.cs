using System.IO;
using System.Windows;
using System.Windows.Threading;
using DFCMAD.App.Services;
using DFCMAD.App.Tray;
using DFCMAD.App.ViewModels;
using DFCMAD.App.Views;
using DFCMAD.Core.Diagnostics;
using DFCMAD.Core.Enforcement;
using DFCMAD.Core.Logging;
using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using DFCMAD.Core.Settings;
using DFCMAD.Core.Startup;
using DFCMAD.WindowsAudio.CoreAudio;
using DFCMAD.WindowsAudio.PolicyConfig;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Application = System.Windows.Application;

namespace DFCMAD.App;

public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceService? _singleInstanceService;
    private TrayIconService? _trayIconService;
    private ILogger<App>? _logger;

    protected override async void OnStartup(StartupEventArgs e)
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.HasOwnership)
        {
            await SingleInstanceService.SignalExistingInstanceAsync(CancellationToken.None).ConfigureAwait(true);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        var paths = new AppPaths();
        Directory.CreateDirectory(paths.AppDataDirectory);
        Directory.CreateDirectory(paths.LogDirectory);

        _host = Host.CreateDefaultBuilder(e.Args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(new RollingFileLoggerProvider(new FileLogWriter(paths)));
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<IAppPaths>(paths);
                services.AddSingleton(Dispatcher);
                services.AddSingleton<IRunRegistry, CurrentUserRunRegistry>();
                services.AddSingleton<IStartupRegistrationService, StartupRegistrationService>();
                services.AddSingleton<IDefaultAudioDeviceSetter, PolicyConfigDefaultAudioDeviceSetter>();
                services.AddSingleton<IAudioDeviceService, CoreAudioDeviceService>();
                services.AddSingleton<ISettingsService, SettingsService>();
                services.AddSingleton<IAudioEnforcementService, AudioEnforcementService>();
                services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<DiagnosticsViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddTransient<DiagnosticsWindow>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();

        _logger = _host.Services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Application started.");

        try
        {
            _singleInstanceService.ShowRequested += (_, _) => Dispatcher.Invoke(() => _host.Services.GetRequiredService<MainWindow>().ShowPanel());
            _singleInstanceService.StartServer();

            var settingsService = _host.Services.GetRequiredService<ISettingsService>();
            await settingsService.LoadAsync(CancellationToken.None).ConfigureAwait(true);

            var viewModel = _host.Services.GetRequiredService<MainViewModel>();
            await viewModel.InitializeAsync(CancellationToken.None).ConfigureAwait(true);

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _trayIconService = _host.Services.GetRequiredService<TrayIconService>();
            _trayIconService.Initialize();

            var enforcementService = _host.Services.GetRequiredService<IAudioEnforcementService>();
            await enforcementService.StartAsync(CancellationToken.None).ConfigureAwait(true);

            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            if (ShouldShowPanelOnStartup(settingsService.Current, e.Args))
            {
                mainWindow.ShowPanel();
            }
            else
            {
                NotifyIfPreferredDeviceMissing(settingsService.Current, viewModel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Application startup failed.");
            System.Windows.MessageBox.Show(
                $"DFCMAD failed to start cleanly. Details were written to {paths.LogDirectory}.",
                "DFCMAD",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

            if (_host is not null)
            {
                var mainWindow = _host.Services.GetService<MainWindow>();
                mainWindow?.AllowClose();

                var enforcementService = _host.Services.GetService<IAudioEnforcementService>();
                if (enforcementService is not null)
                {
                    await enforcementService.StopAsync(CancellationToken.None).ConfigureAwait(true);
                }

                var settingsService = _host.Services.GetService<ISettingsService>();
                if (settingsService is not null)
                {
                    await settingsService.SaveAsync(CancellationToken.None).ConfigureAwait(true);
                }
            }

            _trayIconService?.Dispose();
            _logger?.LogInformation("Application exited.");
            _host?.Dispose();
            _singleInstanceService?.Dispose();
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _ = EnforceFromSystemEventAsync(EnforcementReason.Resume);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _ = EnforceFromSystemEventAsync(EnforcementReason.DisplayChanged);
    }

    private async Task EnforceFromSystemEventAsync(EnforcementReason reason)
    {
        if (_host is null)
        {
            return;
        }

        try
        {
            await _host.Services.GetRequiredService<IAudioEnforcementService>()
                .EnforceNowAsync(reason, CancellationToken.None)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "System-triggered enforcement failed.");
        }
    }

    private void NotifyIfPreferredDeviceMissing(AppSettings settings, MainViewModel viewModel)
    {
        var outputMissing = !string.IsNullOrWhiteSpace(settings.PreferredOutputEndpointId)
                            && viewModel.OutputAvailabilityStatus.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
        var inputMissing = !string.IsNullOrWhiteSpace(settings.PreferredInputEndpointId)
                           && viewModel.InputAvailabilityStatus.Contains("unavailable", StringComparison.OrdinalIgnoreCase);

        if (outputMissing || inputMissing)
        {
            _trayIconService?.ShowBalloon("Selected preferred device is unavailable. DFCMAD will keep watching for it.");
        }
    }

    private static bool ShouldShowPanelOnStartup(AppSettings settings, string[] args)
    {
        if (args.Any(arg => string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase)))
        {
            return !settings.FirstRunCompleted;
        }

        return !settings.FirstRunCompleted
               || (string.IsNullOrWhiteSpace(settings.PreferredOutputEndpointId)
                   && string.IsNullOrWhiteSpace(settings.PreferredInputEndpointId));
    }
}
