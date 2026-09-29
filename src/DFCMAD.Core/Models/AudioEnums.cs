namespace DFCMAD.Core.Models;

public enum AudioDirection
{
    Output,
    Input
}

public enum AudioRole
{
    Console,
    Multimedia,
    Communications
}

public enum AudioDeviceState
{
    Active,
    Disabled,
    NotPresent,
    Unplugged,
    Unknown
}

public enum AudioDeviceChangeKind
{
    Added,
    Removed,
    StateChanged,
    PropertyChanged,
    Refreshed
}

public enum EnforcementReason
{
    Startup,
    Manual,
    SettingsChanged,
    DeviceAdded,
    DeviceRemoved,
    DeviceStateChanged,
    DevicePropertyChanged,
    DefaultDeviceChanged,
    Resume,
    DisplayChanged,
    Timer,
    SelfChangeVerification
}

public enum EnforcementStatus
{
    Stopped,
    Enforcing,
    Paused,
    WaitingForDevice,
    Error
}

public enum FallbackMode
{
    None,
    UsePreviousWindowsDefault,
    UseConfiguredFallbackDevice
}
