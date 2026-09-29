using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;

namespace DFCMAD.Core.Enforcement;

public sealed class AudioEnforcementService : IAudioEnforcementService
{
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan SelfChangeWindow = TimeSpan.FromSeconds(2);
    private static readonly AudioRole[] AllRoles = [AudioRole.Console, AudioRole.Multimedia, AudioRole.Communications];
    private static readonly AudioRole[] BasicRoles = [AudioRole.Console];

    private readonly IAudioDeviceService _audioDeviceService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<AudioEnforcementService> _logger;
    private readonly SemaphoreSlim _enforcementGate = new(1, 1);
    private readonly object _stateGate = new();
    private readonly Queue<EnforcementEvent> _recentEvents = new();
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _serviceCts;
    private DateTimeOffset _selfChangeUntilUtc;
    private bool _started;
    private bool _savingInternalSettings;

    public AudioEnforcementService(
        IAudioDeviceService audioDeviceService,
        ISettingsService settingsService,
        ILogger<AudioEnforcementService> logger)
    {
        _audioDeviceService = audioDeviceService;
        _settingsService = settingsService;
        _logger = logger;
    }

    public EnforcementState State { get; private set; } = new() { StatusText = "Stopped" };

    public IReadOnlyList<EnforcementEvent> RecentEvents
    {
        get
        {
            lock (_stateGate)
            {
                return _recentEvents.ToArray();
            }
        }
    }

    public event EventHandler<EnforcementState>? StateChanged;
    public event EventHandler<EnforcementEvent>? EnforcementEventRecorded;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_started)
        {
            return Task.CompletedTask;
        }

        _started = true;
        _serviceCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _audioDeviceService.DeviceChanged += OnDeviceChanged;
        _audioDeviceService.DefaultDeviceChanged += OnDefaultDeviceChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
        SetState(State with { Status = EnforcementStatus.Enforcing, StatusText = "Enforcing selected devices" });
        QueueEnforcement(EnforcementReason.Startup);
        _logger.LogInformation("Audio enforcement service started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _audioDeviceService.DeviceChanged -= OnDeviceChanged;
        _audioDeviceService.DefaultDeviceChanged -= OnDefaultDeviceChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _debounceCts?.Cancel();
        _serviceCts?.Cancel();
        await _enforcementGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        _enforcementGate.Release();
        SetState(State with { Status = EnforcementStatus.Stopped, StatusText = "Stopped" });
        _logger.LogInformation("Audio enforcement service stopped.");
    }

    public Task EnforceNowAsync(EnforcementReason reason, CancellationToken cancellationToken) =>
        RunEnforcementAsync(reason, cancellationToken);

    public async Task PauseAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        var until = DateTimeOffset.UtcNow.Add(duration);
        _settingsService.Current.EnforcementPausedUntil = until;
        await _settingsService.SaveAsync(cancellationToken).ConfigureAwait(false);
        SetState(State with
        {
            Status = EnforcementStatus.Paused,
            PausedUntil = until,
            StatusText = $"Paused until {until.ToLocalTime():HH:mm}"
        });
        RecordEvent(EnforcementReason.Manual, $"Enforcement paused until {until:O}.", true, null);
    }

    public async Task ResumeAsync(CancellationToken cancellationToken)
    {
        _settingsService.Current.EnforcementPausedUntil = null;
        await _settingsService.SaveAsync(cancellationToken).ConfigureAwait(false);
        SetState(State with { Status = EnforcementStatus.Enforcing, PausedUntil = null, StatusText = "Enforcing selected devices" });
        RecordEvent(EnforcementReason.Manual, "Enforcement resumed.", true, null);
        await RunEnforcementAsync(EnforcementReason.Manual, cancellationToken).ConfigureAwait(false);
    }

    private void OnDeviceChanged(object? sender, AudioDeviceChangedEventArgs args)
    {
        var reason = args.ChangeKind switch
        {
            AudioDeviceChangeKind.Added => EnforcementReason.DeviceAdded,
            AudioDeviceChangeKind.Removed => EnforcementReason.DeviceRemoved,
            AudioDeviceChangeKind.StateChanged => EnforcementReason.DeviceStateChanged,
            AudioDeviceChangeKind.PropertyChanged => EnforcementReason.DevicePropertyChanged,
            _ => EnforcementReason.Timer
        };
        QueueEnforcement(reason);
    }

    private void OnDefaultDeviceChanged(object? sender, DefaultAudioDeviceChangedEventArgs args)
    {
        SetState(State with { LastDetectedWindowsChangeUtc = DateTimeOffset.UtcNow });
        if (DateTimeOffset.UtcNow < _selfChangeUntilUtc)
        {
            QueueEnforcement(EnforcementReason.SelfChangeVerification);
            return;
        }

        QueueEnforcement(EnforcementReason.DefaultDeviceChanged);
    }

    private void OnSettingsChanged(object? sender, AppSettingsChangedEventArgs args)
    {
        if (_savingInternalSettings)
        {
            return;
        }

        QueueEnforcement(EnforcementReason.SettingsChanged);
    }

    private void QueueEnforcement(EnforcementReason reason)
    {
        if (!_started)
        {
            return;
        }

        var serviceToken = _serviceCts?.Token ?? CancellationToken.None;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = CancellationTokenSource.CreateLinkedTokenSource(serviceToken);
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelay, token).ConfigureAwait(false);
                await RunEnforcementAsync(reason, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private async Task RunEnforcementAsync(EnforcementReason reason, CancellationToken cancellationToken)
    {
        await _enforcementGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = _settingsService.Current;
            if (settings.EnforcementPausedUntil is { } pausedUntil)
            {
                if (pausedUntil > DateTimeOffset.UtcNow)
                {
                    SetState(State with
                    {
                        Status = EnforcementStatus.Paused,
                        PausedUntil = pausedUntil,
                        StatusText = $"Paused until {pausedUntil.ToLocalTime():HH:mm}"
                    });
                    RecordEvent(reason, "Enforcement skipped because it is paused.", true, null);
                    return;
                }

                settings.EnforcementPausedUntil = null;
                await SaveSettingsInternalAsync(cancellationToken).ConfigureAwait(false);
            }

            SetState(State with { Status = EnforcementStatus.Enforcing, PausedUntil = null, StatusText = "Enforcing selected devices", LastError = null });
            var devices = await _audioDeviceService.GetDevicesAsync(cancellationToken).ConfigureAwait(false);
            UpdateLastKnownDevices(settings, devices);

            var roles = settings.EnforceAllRolesEnabled ? AllRoles : BasicRoles;
            var outputResult = await EnforceDirectionAsync(AudioDirection.Output, settings, devices, roles, reason, cancellationToken).ConfigureAwait(false);
            var inputResult = await EnforceDirectionAsync(AudioDirection.Input, settings, devices, roles, reason, cancellationToken).ConfigureAwait(false);

            await SaveSettingsInternalAsync(cancellationToken).ConfigureAwait(false);

            var finalStatus = outputResult.Failed || inputResult.Failed
                ? EnforcementStatus.Error
                : outputResult.Waiting || inputResult.Waiting
                ? EnforcementStatus.WaitingForDevice
                : EnforcementStatus.Enforcing;
            var statusText = BuildStatusText(outputResult, inputResult);
            SetState(State with
            {
                Status = finalStatus,
                StatusText = statusText,
                LastEnforcementUtc = DateTimeOffset.UtcNow,
                LastError = finalStatus == EnforcementStatus.Error ? State.LastError : null
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enforcement failed.");
            SetState(State with
            {
                Status = EnforcementStatus.Error,
                StatusText = "Enforcement failed. Open diagnostics.",
                LastError = ex.Message,
                LastEnforcementUtc = DateTimeOffset.UtcNow
            });
            RecordEvent(reason, "Enforcement failed.", false, ex.Message);
        }
        finally
        {
            _enforcementGate.Release();
        }
    }

    private async Task<DirectionResult> EnforceDirectionAsync(
        AudioDirection direction,
        AppSettings settings,
        IReadOnlyList<AudioDeviceInfo> devices,
        IReadOnlyCollection<AudioRole> roles,
        EnforcementReason reason,
        CancellationToken cancellationToken)
    {
        var enabled = direction == AudioDirection.Output ? settings.EnforceOutputEnabled : settings.EnforceInputEnabled;
        var preferredId = direction == AudioDirection.Output ? settings.PreferredOutputEndpointId : settings.PreferredInputEndpointId;

        if (!enabled || string.IsNullOrWhiteSpace(preferredId))
        {
            RecordEvent(reason, $"{direction} enforcement skipped because no preferred device is configured.", true, null);
            return DirectionResult.Skipped;
        }

        var preferredDevice = devices.FirstOrDefault(device => string.Equals(device.EndpointId, preferredId, StringComparison.Ordinal));
        if (preferredDevice is null || preferredDevice.State != AudioDeviceState.Active)
        {
            RecordEvent(reason, $"Waiting for selected {direction.ToString().ToLowerInvariant()} device.", true, null);
            return DirectionResult.WaitingForDevice;
        }

        var alreadyCorrect = true;
        foreach (var role in roles)
        {
            var defaultDevice = await _audioDeviceService.GetDefaultDeviceAsync(direction, role, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(defaultDevice?.EndpointId, preferredId, StringComparison.Ordinal))
            {
                alreadyCorrect = false;
                break;
            }
        }

        if (alreadyCorrect)
        {
            RecordEvent(reason, $"{direction} device already matches the selected device.", true, null);
            return DirectionResult.NoChange;
        }

        var setSucceeded = await SetWithRetryAsync(preferredId, direction, roles, reason, cancellationToken).ConfigureAwait(false);
        if (!setSucceeded)
        {
            return DirectionResult.FailedResult;
        }

        _selfChangeUntilUtc = DateTimeOffset.UtcNow.Add(SelfChangeWindow);
        RecordEvent(reason, $"Set selected {direction.ToString().ToLowerInvariant()} device to {preferredDevice.DisplayName}.", true, null);
        return DirectionResult.ChangedResult;
    }

    private async Task<bool> SetWithRetryAsync(
        string endpointId,
        AudioDirection direction,
        IReadOnlyCollection<AudioRole> roles,
        EnforcementReason reason,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await _audioDeviceService.SetDefaultDeviceAsync(endpointId, direction, roles, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                _logger.LogWarning(ex, "Setting default {Direction} device failed on attempt {Attempt}.", direction, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        var message = lastError?.Message ?? "Unknown error";
        RecordEvent(reason, $"Failed to set selected {direction.ToString().ToLowerInvariant()} device.", false, message);
        SetState(State with { Status = EnforcementStatus.Error, StatusText = "Enforcement failed. Open diagnostics.", LastError = message });
        return false;
    }

    private static void UpdateLastKnownDevices(AppSettings settings, IReadOnlyList<AudioDeviceInfo> devices)
    {
        foreach (var device in devices)
        {
            var existing = settings.LastKnownDevices.FirstOrDefault(item => string.Equals(item.EndpointId, device.EndpointId, StringComparison.Ordinal));
            if (existing is null)
            {
                settings.LastKnownDevices.Add(new KnownAudioDevice
                {
                    EndpointId = device.EndpointId,
                    FriendlyName = device.FriendlyName,
                    Direction = device.Direction,
                    LastState = device.State,
                    LastSeenUtc = DateTimeOffset.UtcNow
                });
            }
            else
            {
                existing.FriendlyName = device.FriendlyName;
                existing.Direction = device.Direction;
                existing.LastState = device.State;
                existing.LastSeenUtc = DateTimeOffset.UtcNow;
            }
        }
    }

    private static string BuildStatusText(DirectionResult outputResult, DirectionResult inputResult)
    {
        if (outputResult.Waiting)
        {
            return "Waiting for selected output device";
        }

        if (inputResult.Waiting)
        {
            return "Waiting for selected input device";
        }

        if (outputResult.Failed || inputResult.Failed)
        {
            return "Enforcement failed. Open diagnostics.";
        }

        return outputResult.Changed || inputResult.Changed
            ? "Windows changed your audio device. DFCMAD changed it back."
            : "Enforcing selected devices";
    }

    private void SetState(EnforcementState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private async Task SaveSettingsInternalAsync(CancellationToken cancellationToken)
    {
        _savingInternalSettings = true;
        try
        {
            await _settingsService.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _savingInternalSettings = false;
        }
    }

    private void RecordEvent(EnforcementReason reason, string message, bool succeeded, string? error)
    {
        var item = new EnforcementEvent
        {
            TimestampUtc = DateTimeOffset.UtcNow,
            Reason = reason,
            Message = message,
            Succeeded = succeeded,
            Error = error
        };

        lock (_stateGate)
        {
            _recentEvents.Enqueue(item);
            while (_recentEvents.Count > 50)
            {
                _recentEvents.Dequeue();
            }
        }

        if (succeeded)
        {
            _logger.LogInformation("{Message}", message);
        }
        else
        {
            _logger.LogError("{Message} {Error}", message, error);
        }

        EnforcementEventRecorded?.Invoke(this, item);
    }

    private sealed record DirectionResult(bool Changed, bool Waiting, bool Failed)
    {
        public static readonly DirectionResult Skipped = new(false, false, false);
        public static readonly DirectionResult NoChange = new(false, false, false);
        public static readonly DirectionResult ChangedResult = new(true, false, false);
        public static readonly DirectionResult WaitingForDevice = new(false, true, false);
        public static readonly DirectionResult FailedResult = new(false, false, true);
    }
}
