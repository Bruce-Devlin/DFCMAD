using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using DFCMAD.Core.Models;
using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;

namespace DFCMAD.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IAudioDeviceService _audioDeviceService;
    private readonly IAudioEnforcementService _enforcementService;
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IAppPaths _paths;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Dispatcher _dispatcher;
    private AudioDeviceInfo? _selectedOutputDevice;
    private AudioDeviceInfo? _selectedInputDevice;
    private AudioDeviceInfo? _currentDefaultOutputDevice;
    private AudioDeviceInfo? _currentDefaultInputDevice;
    private EnforcementState _state = new() { StatusText = "Starting" };
    private string? _lastRefreshError;
    private bool _suppressSettingSave;
    private bool _suppressSelectionSave;
    private string _lastEventText = "Last action: Windows default matches lock";

    public MainViewModel(
        ISettingsService settingsService,
        IAudioDeviceService audioDeviceService,
        IAudioEnforcementService enforcementService,
        IStartupRegistrationService startupRegistrationService,
        IAppPaths paths,
        ILogger<MainViewModel> logger,
        Dispatcher dispatcher)
    {
        _settingsService = settingsService;
        _audioDeviceService = audioDeviceService;
        _enforcementService = enforcementService;
        _startupRegistrationService = startupRegistrationService;
        _paths = paths;
        _logger = logger;
        _dispatcher = dispatcher;

        SetOutputCommand = new AsyncRelayCommand(SetOutputAsync, () => SelectedOutputDevice is not null);
        SetInputCommand = new AsyncRelayCommand(SetInputAsync, () => SelectedInputDevice is not null);
        EnforceNowCommand = new AsyncRelayCommand(token => _enforcementService.EnforceNowAsync(EnforcementReason.Manual, token));
        PauseCommand = new AsyncRelayCommand(token => _enforcementService.PauseAsync(TimeSpan.FromMinutes(5), token));
        ResumeCommand = new AsyncRelayCommand(token => _enforcementService.ResumeAsync(token));
        TogglePauseCommand = new AsyncRelayCommand(TogglePauseAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        FinishSetupCommand = new AsyncRelayCommand(FinishSetupAsync);
        OpenLogsCommand = new RelayCommand(() => OpenLogsRequested?.Invoke(this, EventArgs.Empty));
        OpenDiagnosticsCommand = new RelayCommand(() => DiagnosticsRequested?.Invoke(this, EventArgs.Empty));
        HideCommand = new RelayCommand(() => HideRequested?.Invoke(this, EventArgs.Empty));

        _enforcementService.StateChanged += (_, state) => RunOnUi(() =>
        {
            State = state;
            RaiseStateSummaryProperties();
        });

        _enforcementService.EnforcementEventRecorded += (_, item) => RunOnUi(() =>
        {
            LastEventText = $"{item.TimestampUtc.ToLocalTime():HH:mm} - {item.Message}";
        });
    }

    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = [];
    public ObservableCollection<AudioDeviceInfo> InputDevices { get; } = [];

    public AudioDeviceInfo? SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (SetProperty(ref _selectedOutputDevice, value))
            {
                ((AsyncRelayCommand)SetOutputCommand).RaiseCanExecuteChanged();
                if (!_suppressSelectionSave && value is not null)
                {
                    _ = SetOutputAsync(CancellationToken.None);
                }
            }
        }
    }

    public AudioDeviceInfo? SelectedInputDevice
    {
        get => _selectedInputDevice;
        set
        {
            if (SetProperty(ref _selectedInputDevice, value))
            {
                ((AsyncRelayCommand)SetInputCommand).RaiseCanExecuteChanged();
                if (!_suppressSelectionSave && value is not null)
                {
                    _ = SetInputAsync(CancellationToken.None);
                }
            }
        }
    }

    public EnforcementState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusLabel));
                RaiseStateSummaryProperties();
                RaiseDeviceSummaryProperties();
            }
        }
    }

    public bool IsFirstRunVisible => !_settingsService.Current.FirstRunCompleted;

    public string StatusText => State.StatusText;

    public string StatusLabel => State.Status switch
    {
        EnforcementStatus.Enforcing => "Locked",
        EnforcementStatus.Paused => "Paused",
        EnforcementStatus.WaitingForDevice => "Waiting for device",
        EnforcementStatus.Error => "Action needed",
        _ => "Stopped"
    };

    public string HeaderStatusText => State.Status switch
    {
        EnforcementStatus.Enforcing => "Audio device lock is active",
        EnforcementStatus.Paused => State.PausedUntil is null
            ? "Audio device lock is paused"
            : $"Audio device lock is paused until {State.PausedUntil.Value.ToLocalTime():HH:mm}",
        EnforcementStatus.WaitingForDevice => "Waiting for a selected device to return",
        EnforcementStatus.Error => "Action needed. Open diagnostics for details.",
        _ => "Audio device lock is starting"
    };

    public string LockedOutputText => string.IsNullOrWhiteSpace(_settingsService.Current.PreferredOutputDisplayNameSnapshot)
        ? "Not selected"
        : _settingsService.Current.PreferredOutputDisplayNameSnapshot;

    public string LockedInputText => string.IsNullOrWhiteSpace(_settingsService.Current.PreferredInputDisplayNameSnapshot)
        ? "Not selected"
        : _settingsService.Current.PreferredInputDisplayNameSnapshot;

    public string PreferredOutputSummary => string.IsNullOrWhiteSpace(_settingsService.Current.PreferredOutputDisplayNameSnapshot)
        ? "Preferred: not selected"
        : $"Preferred: {_settingsService.Current.PreferredOutputDisplayNameSnapshot}";

    public string PreferredInputSummary => string.IsNullOrWhiteSpace(_settingsService.Current.PreferredInputDisplayNameSnapshot)
        ? "Preferred: not selected"
        : $"Preferred: {_settingsService.Current.PreferredInputDisplayNameSnapshot}";

    public string CurrentDefaultOutputSummary => _currentDefaultOutputDevice is null
        ? "Windows default: none"
        : $"Windows default: {_currentDefaultOutputDevice.FriendlyName}";

    public string CurrentDefaultInputSummary => _currentDefaultInputDevice is null
        ? "Windows default: none"
        : $"Windows default: {_currentDefaultInputDevice.FriendlyName}";

    public string OutputAvailabilityStatus => BuildAvailabilityText(AudioDirection.Output);
    public string InputAvailabilityStatus => BuildAvailabilityText(AudioDirection.Input);

    public string OutputDefaultName => _currentDefaultOutputDevice?.FriendlyName ?? "No Windows default";
    public string InputDefaultName => _currentDefaultInputDevice?.FriendlyName ?? "No Windows default";
    public string OutputStatusLabel => BuildDeviceStatus(AudioDirection.Output);
    public string InputStatusLabel => BuildDeviceStatus(AudioDirection.Input);
    public string OutputHelpText => BuildDeviceHelpText(AudioDirection.Output);
    public string InputHelpText => BuildDeviceHelpText(AudioDirection.Input);
    public bool HasOutputHelp => !string.IsNullOrWhiteSpace(OutputHelpText);
    public bool HasInputHelp => !string.IsNullOrWhiteSpace(InputHelpText);

    public bool HasOutputDevices => OutputDevices.Count > 0;
    public bool HasInputDevices => InputDevices.Count > 0;
    public bool IsOutputEmpty => OutputDevices.Count == 0;
    public bool IsInputEmpty => InputDevices.Count == 0;
    public string OutputEmptyText => LastRefreshError is null ? "No output devices found." : $"Device refresh failed: {LastRefreshError}";
    public string InputEmptyText => LastRefreshError is null ? "No input devices found." : $"Device refresh failed: {LastRefreshError}";

    public bool EnforceOutputEnabled
    {
        get => _settingsService.Current.EnforceOutputEnabled;
        set
        {
            if (_settingsService.Current.EnforceOutputEnabled == value)
            {
                return;
            }

            _settingsService.Current.EnforceOutputEnabled = value;
            OnPropertyChanged();
            _ = SaveAndEnforceAsync();
        }
    }

    public bool EnforceInputEnabled
    {
        get => _settingsService.Current.EnforceInputEnabled;
        set
        {
            if (_settingsService.Current.EnforceInputEnabled == value)
            {
                return;
            }

            _settingsService.Current.EnforceInputEnabled = value;
            OnPropertyChanged();
            _ = SaveAndEnforceAsync();
        }
    }

    public bool StartWithWindowsEnabled
    {
        get => _settingsService.Current.StartWithWindowsEnabled;
        set
        {
            if (_settingsService.Current.StartWithWindowsEnabled == value)
            {
                return;
            }

            _settingsService.Current.StartWithWindowsEnabled = value;
            OnPropertyChanged();
            try
            {
                if (value)
                {
                    _startupRegistrationService.Enable();
                }
                else
                {
                    _startupRegistrationService.Disable();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update startup registration.");
            }

            _ = _settingsService.SaveAsync(CancellationToken.None);
        }
    }

    public bool EnforceAllRolesEnabled
    {
        get => _settingsService.Current.EnforceAllRolesEnabled;
        set
        {
            if (_settingsService.Current.EnforceAllRolesEnabled == value)
            {
                return;
            }

            _settingsService.Current.EnforceAllRolesEnabled = value;
            OnPropertyChanged();
            _ = SaveAndEnforceAsync();
        }
    }

    public string LastEnforcementSummary => State.LastEnforcementUtc is null
        ? "Last locked: never"
        : $"Last locked: {State.LastEnforcementUtc.Value.ToLocalTime():HH:mm}";

    public string LastWindowsChangeSummary => State.LastDetectedWindowsChangeUtc is null
        ? "No Windows changes detected"
        : $"Windows changed audio at {State.LastDetectedWindowsChangeUtc.Value.ToLocalTime():HH:mm}";

    public bool IsPaused => State.Status == EnforcementStatus.Paused;

    public string PauseButtonText => IsPaused ? "Resume now" : "Pause for 5 min";

    public string LastEventText
    {
        get => _lastEventText;
        private set => SetProperty(ref _lastEventText, value);
    }

    public string AppVersionSummary => $"v{AppVersionInfo.Version}";

    public string? LastRefreshError
    {
        get => _lastRefreshError;
        private set
        {
            if (SetProperty(ref _lastRefreshError, value))
            {
                OnPropertyChanged(nameof(OutputEmptyText));
                OnPropertyChanged(nameof(InputEmptyText));
            }
        }
    }

    public ICommand SetOutputCommand { get; }
    public ICommand SetInputCommand { get; }
    public ICommand EnforceNowCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand TogglePauseCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand FinishSetupCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand OpenDiagnosticsCommand { get; }
    public ICommand HideCommand { get; }

    public event EventHandler? DiagnosticsRequested;
    public event EventHandler? OpenLogsRequested;
    public event EventHandler? HideRequested;
    public event EventHandler? FirstSetupCompleted;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _suppressSettingSave = true;
        try
        {
            var startupEnabled = false;
            try
            {
                startupEnabled = _startupRegistrationService.IsEnabled();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read startup registration state.");
            }

            _settingsService.Current.StartWithWindowsEnabled = startupEnabled;
            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            State = _enforcementService.State;
        }
        finally
        {
            _suppressSettingSave = false;
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var devices = await _audioDeviceService.GetDevicesAsync(cancellationToken).ConfigureAwait(true);
            var outputDevices = AddRememberedUnavailableDevices(devices, AudioDirection.Output);
            var inputDevices = AddRememberedUnavailableDevices(devices, AudioDirection.Input);

            ReplaceDevices(OutputDevices, outputDevices);
            ReplaceDevices(InputDevices, inputDevices);

            _currentDefaultOutputDevice = await _audioDeviceService.GetDefaultDeviceAsync(AudioDirection.Output, AudioRole.Console, cancellationToken).ConfigureAwait(true);
            _currentDefaultInputDevice = await _audioDeviceService.GetDefaultDeviceAsync(AudioDirection.Input, AudioRole.Console, cancellationToken).ConfigureAwait(true);

            _suppressSelectionSave = true;
            try
            {
                SelectedOutputDevice = OutputDevices.FirstOrDefault(device => device.EndpointId == _settingsService.Current.PreferredOutputEndpointId)
                                       ?? OutputDevices.FirstOrDefault(device => device.IsDefault)
                                       ?? OutputDevices.FirstOrDefault();
                SelectedInputDevice = InputDevices.FirstOrDefault(device => device.EndpointId == _settingsService.Current.PreferredInputEndpointId)
                                      ?? InputDevices.FirstOrDefault(device => device.IsDefault)
                                      ?? InputDevices.FirstOrDefault();
            }
            finally
            {
                _suppressSelectionSave = false;
            }

            LastRefreshError = null;
            RaiseDeviceSummaryProperties();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LastRefreshError = ex.Message;
            _logger.LogError(ex, "Device refresh failed.");
        }
    }

    private async Task SetOutputAsync(CancellationToken cancellationToken)
    {
        if (SelectedOutputDevice is null)
        {
            return;
        }

        _settingsService.Current.PreferredOutputEndpointId = SelectedOutputDevice.EndpointId;
        _settingsService.Current.PreferredOutputDisplayNameSnapshot = SelectedOutputDevice.FriendlyName;
        await _settingsService.SaveAsync(cancellationToken).ConfigureAwait(true);
        await _enforcementService.EnforceNowAsync(EnforcementReason.SettingsChanged, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task SetInputAsync(CancellationToken cancellationToken)
    {
        if (SelectedInputDevice is null)
        {
            return;
        }

        _settingsService.Current.PreferredInputEndpointId = SelectedInputDevice.EndpointId;
        _settingsService.Current.PreferredInputDisplayNameSnapshot = SelectedInputDevice.FriendlyName;
        await _settingsService.SaveAsync(cancellationToken).ConfigureAwait(true);
        await _enforcementService.EnforceNowAsync(EnforcementReason.SettingsChanged, cancellationToken).ConfigureAwait(true);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task FinishSetupAsync(CancellationToken cancellationToken)
    {
        if (SelectedOutputDevice is not null && string.IsNullOrWhiteSpace(_settingsService.Current.PreferredOutputEndpointId))
        {
            _settingsService.Current.PreferredOutputEndpointId = SelectedOutputDevice.EndpointId;
            _settingsService.Current.PreferredOutputDisplayNameSnapshot = SelectedOutputDevice.FriendlyName;
        }

        if (SelectedInputDevice is not null && string.IsNullOrWhiteSpace(_settingsService.Current.PreferredInputEndpointId))
        {
            _settingsService.Current.PreferredInputEndpointId = SelectedInputDevice.EndpointId;
            _settingsService.Current.PreferredInputDisplayNameSnapshot = SelectedInputDevice.FriendlyName;
        }

        _settingsService.Current.FirstRunCompleted = true;
        await _settingsService.SaveAsync(cancellationToken).ConfigureAwait(true);
        await _enforcementService.EnforceNowAsync(EnforcementReason.Manual, cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(IsFirstRunVisible));
        RaiseDeviceSummaryProperties();
        FirstSetupCompleted?.Invoke(this, EventArgs.Empty);
    }

    private async Task TogglePauseAsync(CancellationToken cancellationToken)
    {
        if (IsPaused)
        {
            await _enforcementService.ResumeAsync(cancellationToken).ConfigureAwait(true);
        }
        else
        {
            await _enforcementService.PauseAsync(TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task SaveAndEnforceAsync()
    {
        if (_suppressSettingSave)
        {
            return;
        }

        await _settingsService.SaveAsync(CancellationToken.None).ConfigureAwait(true);
        await _enforcementService.EnforceNowAsync(EnforcementReason.SettingsChanged, CancellationToken.None).ConfigureAwait(true);
    }

    private List<AudioDeviceInfo> AddRememberedUnavailableDevices(IReadOnlyList<AudioDeviceInfo> devices, AudioDirection direction)
    {
        var known = devices.Where(device => device.Direction == direction)
            .OrderByDescending(device => device.State == AudioDeviceState.Active)
            .ThenBy(device => device.FriendlyName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var remembered in _settingsService.Current.LastKnownDevices.Where(device => device.Direction == direction))
        {
            if (known.Any(device => device.EndpointId == remembered.EndpointId))
            {
                continue;
            }

            known.Add(new AudioDeviceInfo
            {
                EndpointId = remembered.EndpointId,
                FriendlyName = $"{remembered.FriendlyName} (unavailable)",
                Direction = remembered.Direction,
                State = remembered.LastState == AudioDeviceState.Active ? AudioDeviceState.NotPresent : remembered.LastState
            });
        }

        EnsurePreferredPlaceholder(known, direction);
        return known;
    }

    private void EnsurePreferredPlaceholder(List<AudioDeviceInfo> devices, AudioDirection direction)
    {
        var preferredId = direction == AudioDirection.Output
            ? _settingsService.Current.PreferredOutputEndpointId
            : _settingsService.Current.PreferredInputEndpointId;
        if (string.IsNullOrWhiteSpace(preferredId) || devices.Any(device => device.EndpointId == preferredId))
        {
            return;
        }

        var name = direction == AudioDirection.Output
            ? _settingsService.Current.PreferredOutputDisplayNameSnapshot
            : _settingsService.Current.PreferredInputDisplayNameSnapshot;
        devices.Add(new AudioDeviceInfo
        {
            EndpointId = preferredId,
            FriendlyName = $"{(string.IsNullOrWhiteSpace(name) ? "Selected device" : name)} (unavailable)",
            Direction = direction,
            State = AudioDeviceState.NotPresent,
            IsPreferred = true
        });
    }

    private static void ReplaceDevices(ObservableCollection<AudioDeviceInfo> target, IEnumerable<AudioDeviceInfo> devices)
    {
        target.Clear();
        foreach (var device in devices)
        {
            target.Add(device);
        }
    }

    private string BuildAvailabilityText(AudioDirection direction)
    {
        var preferredId = direction == AudioDirection.Output
            ? _settingsService.Current.PreferredOutputEndpointId
            : _settingsService.Current.PreferredInputEndpointId;
        if (string.IsNullOrWhiteSpace(preferredId))
        {
            return "Availability: not selected";
        }

        var list = direction == AudioDirection.Output ? OutputDevices : InputDevices;
        var device = list.FirstOrDefault(item => item.EndpointId == preferredId);
        return device?.State == AudioDeviceState.Active
            ? "Availability: active"
            : "Availability: selected device is unavailable";
    }

    private string BuildDeviceStatus(AudioDirection direction)
    {
        var enforceEnabled = direction == AudioDirection.Output ? EnforceOutputEnabled : EnforceInputEnabled;
        if (!enforceEnabled)
        {
            return "Off";
        }

        var preferredId = direction == AudioDirection.Output
            ? _settingsService.Current.PreferredOutputEndpointId
            : _settingsService.Current.PreferredInputEndpointId;
        if (string.IsNullOrWhiteSpace(preferredId))
        {
            return "Not selected";
        }

        var list = direction == AudioDirection.Output ? OutputDevices : InputDevices;
        var currentDefault = direction == AudioDirection.Output ? _currentDefaultOutputDevice : _currentDefaultInputDevice;
        var preferred = list.FirstOrDefault(item => item.EndpointId == preferredId);
        if (preferred?.State != AudioDeviceState.Active)
        {
            return "Waiting for device";
        }

        return currentDefault?.EndpointId == preferredId ? "Locked" : "Action needed";
    }

    private string BuildDeviceHelpText(AudioDirection direction)
    {
        var status = BuildDeviceStatus(direction);
        var defaultName = direction == AudioDirection.Output ? OutputDefaultName : InputDefaultName;
        return status switch
        {
            "Not selected" => direction == AudioDirection.Output
                ? "Choose the speaker or headphone device Windows should keep."
                : "Choose the microphone device Windows should keep.",
            "Waiting for device" => $"Windows is currently using {defaultName}. DFCMAD will restore your preferred device when it reconnects.",
            "Action needed" => "Windows default does not match the selected lock. Use Enforce now to restore it immediately.",
            "Off" => "This lock is turned off.",
            _ => string.Empty
        };
    }

    private void RaiseStateSummaryProperties()
    {
        OnPropertyChanged(nameof(HeaderStatusText));
        OnPropertyChanged(nameof(LastEnforcementSummary));
        OnPropertyChanged(nameof(LastWindowsChangeSummary));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(PauseButtonText));
    }

    private void RaiseDeviceSummaryProperties()
    {
        OnPropertyChanged(nameof(PreferredOutputSummary));
        OnPropertyChanged(nameof(PreferredInputSummary));
        OnPropertyChanged(nameof(CurrentDefaultOutputSummary));
        OnPropertyChanged(nameof(CurrentDefaultInputSummary));
        OnPropertyChanged(nameof(OutputAvailabilityStatus));
        OnPropertyChanged(nameof(InputAvailabilityStatus));
        OnPropertyChanged(nameof(OutputDefaultName));
        OnPropertyChanged(nameof(InputDefaultName));
        OnPropertyChanged(nameof(OutputStatusLabel));
        OnPropertyChanged(nameof(InputStatusLabel));
        OnPropertyChanged(nameof(OutputHelpText));
        OnPropertyChanged(nameof(InputHelpText));
        OnPropertyChanged(nameof(HasOutputHelp));
        OnPropertyChanged(nameof(HasInputHelp));
        OnPropertyChanged(nameof(LockedOutputText));
        OnPropertyChanged(nameof(LockedInputText));
        OnPropertyChanged(nameof(HasOutputDevices));
        OnPropertyChanged(nameof(HasInputDevices));
        OnPropertyChanged(nameof(IsOutputEmpty));
        OnPropertyChanged(nameof(IsInputEmpty));
        OnPropertyChanged(nameof(OutputEmptyText));
        OnPropertyChanged(nameof(InputEmptyText));
        OnPropertyChanged(nameof(IsFirstRunVisible));
        OnPropertyChanged(nameof(StartWithWindowsEnabled));
        OnPropertyChanged(nameof(EnforceOutputEnabled));
        OnPropertyChanged(nameof(EnforceInputEnabled));
        OnPropertyChanged(nameof(EnforceAllRolesEnabled));
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    public void OpenLogFolder()
    {
        Directory.CreateDirectory(_paths.LogDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = _paths.LogDirectory,
            UseShellExecute = true
        });
    }
}
