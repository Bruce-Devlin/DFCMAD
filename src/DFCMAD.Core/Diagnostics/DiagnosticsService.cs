using System.Runtime.InteropServices;
using System.Text;
using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;

namespace DFCMAD.Core.Diagnostics;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly ISettingsService _settingsService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IAudioEnforcementService _enforcementService;
    private readonly IAppPaths _paths;
    private readonly ILogger<DiagnosticsService> _logger;

    public DiagnosticsService(
        ISettingsService settingsService,
        IStartupRegistrationService startupRegistrationService,
        IAudioDeviceService audioDeviceService,
        IAudioEnforcementService enforcementService,
        IAppPaths paths,
        ILogger<DiagnosticsService> logger)
    {
        _settingsService = settingsService;
        _startupRegistrationService = startupRegistrationService;
        _audioDeviceService = audioDeviceService;
        _enforcementService = enforcementService;
        _paths = paths;
        _logger = logger;
    }

    public async Task<string> BuildDiagnosticsAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Current;
        var devices = Array.Empty<AudioDeviceInfo>() as IReadOnlyList<AudioDeviceInfo>;
        Exception? deviceError = null;

        try
        {
            devices = await _audioDeviceService.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            deviceError = ex;
        }

        var builder = new StringBuilder();
        builder.AppendLine("DFCMAD diagnostics");
        builder.AppendLine($"App version: {AppVersionInfo.Version}");
        builder.AppendLine($"Windows version: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"Process: {Environment.ProcessPath}");
        builder.AppendLine($"Startup enabled: {SafeStartupState()}");
        builder.AppendLine($"Settings file: {_settingsService.SettingsFilePath}");
        builder.AppendLine($"Log folder: {_paths.LogDirectory}");
        builder.AppendLine($"Settings warning: {_settingsService.LastWarning ?? "none"}");
        builder.AppendLine();

        builder.AppendLine("Enforcement");
        builder.AppendLine($"State: {_enforcementService.State.Status}");
        builder.AppendLine($"Status text: {_enforcementService.State.StatusText}");
        builder.AppendLine($"Paused until: {_enforcementService.State.PausedUntil?.ToString("O") ?? "not paused"}");
        builder.AppendLine($"Last enforcement UTC: {_enforcementService.State.LastEnforcementUtc?.ToString("O") ?? "never"}");
        builder.AppendLine($"Last Windows change UTC: {_enforcementService.State.LastDetectedWindowsChangeUtc?.ToString("O") ?? "never"}");
        builder.AppendLine($"Last error: {_enforcementService.State.LastError ?? "none"}");
        builder.AppendLine();

        builder.AppendLine("Configured devices");
        builder.AppendLine($"Preferred output endpoint ID: {settings.PreferredOutputEndpointId ?? "not set"}");
        builder.AppendLine($"Preferred output name: {settings.PreferredOutputDisplayNameSnapshot ?? "not set"}");
        builder.AppendLine($"Preferred input endpoint ID: {settings.PreferredInputEndpointId ?? "not set"}");
        builder.AppendLine($"Preferred input name: {settings.PreferredInputDisplayNameSnapshot ?? "not set"}");
        builder.AppendLine($"Enforce output: {settings.EnforceOutputEnabled}");
        builder.AppendLine($"Enforce input: {settings.EnforceInputEnabled}");
        builder.AppendLine($"Enforce all roles: {settings.EnforceAllRolesEnabled}");
        builder.AppendLine();

        if (deviceError is not null)
        {
            builder.AppendLine($"Device enumeration error: {deviceError.Message}");
        }
        else
        {
            await AppendDefaultsAsync(builder, AudioDirection.Output, cancellationToken).ConfigureAwait(false);
            await AppendDefaultsAsync(builder, AudioDirection.Input, cancellationToken).ConfigureAwait(false);
            AppendDevices(builder, "Available render devices", devices.Where(device => device.Direction == AudioDirection.Output));
            AppendDevices(builder, "Available capture devices", devices.Where(device => device.Direction == AudioDirection.Input));
        }

        builder.AppendLine("Last enforcement events");
        foreach (var item in _enforcementService.RecentEvents.TakeLast(settings.DiagnosticsPreferences.EventCount))
        {
            builder.AppendLine($"{item.TimestampUtc:O} [{item.Reason}] {(item.Succeeded ? "OK" : "FAIL")} {item.Message} {item.Error}");
        }

        _logger.LogInformation("Diagnostics generated.");
        return builder.ToString();
    }

    private async Task AppendDefaultsAsync(StringBuilder builder, AudioDirection direction, CancellationToken cancellationToken)
    {
        builder.AppendLine($"Current default {direction.ToString().ToLowerInvariant()} devices by role");
        foreach (var role in new[] { AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications })
        {
            try
            {
                var device = await _audioDeviceService.GetDefaultDeviceAsync(direction, role, cancellationToken).ConfigureAwait(false);
                builder.AppendLine($"  {role}: {device?.FriendlyName ?? "none"} ({device?.EndpointId ?? "none"})");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                builder.AppendLine($"  {role}: error - {ex.Message}");
            }
        }

        builder.AppendLine();
    }

    private static void AppendDevices(StringBuilder builder, string heading, IEnumerable<AudioDeviceInfo> devices)
    {
        builder.AppendLine(heading);
        foreach (var device in devices.OrderByDescending(device => device.State == AudioDeviceState.Active).ThenBy(device => device.FriendlyName))
        {
            builder.AppendLine($"  [{device.State}] {(device.IsDefault ? "*" : " ")} {device.FriendlyName}");
            builder.AppendLine($"      ID: {device.EndpointId}");
            builder.AppendLine($"      Description: {device.DeviceDescription ?? "none"}");
            builder.AppendLine($"      Interface: {device.InterfaceFriendlyName ?? "none"}");
            builder.AppendLine($"      Form factor: {device.FormFactor ?? "none"}");
            builder.AppendLine($"      Container ID: {device.ContainerId ?? "none"}");
        }

        builder.AppendLine();
    }

    private string SafeStartupState()
    {
        try
        {
            return _startupRegistrationService.IsEnabled().ToString();
        }
        catch (Exception ex)
        {
            return $"error - {ex.Message}";
        }
    }
}
