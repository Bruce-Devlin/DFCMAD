namespace DFCMAD.Core.Models;

public sealed class AppSettings
{
    public string AppVersion { get; set; } = AppVersionInfo.Version;
    public bool FirstRunCompleted { get; set; }
    public string? PreferredOutputEndpointId { get; set; }
    public string? PreferredOutputDisplayNameSnapshot { get; set; }
    public string? PreferredInputEndpointId { get; set; }
    public string? PreferredInputDisplayNameSnapshot { get; set; }
    public bool EnforceOutputEnabled { get; set; } = true;
    public bool EnforceInputEnabled { get; set; } = true;
    public bool EnforceAllRolesEnabled { get; set; } = true;
    public bool StartWithWindowsEnabled { get; set; }
    public DateTimeOffset? EnforcementPausedUntil { get; set; }
    public FallbackSettings Fallback { get; set; } = new();
    public List<KnownAudioDevice> LastKnownDevices { get; set; } = [];
    public UiPreferences UiPreferences { get; set; } = new();
    public DiagnosticsPreferences DiagnosticsPreferences { get; set; } = new();
}

public sealed class FallbackSettings
{
    public FallbackMode OutputFallbackMode { get; set; } = FallbackMode.None;
    public string? OutputFallbackEndpointId { get; set; }
    public string? OutputFallbackDisplayNameSnapshot { get; set; }
    public FallbackMode InputFallbackMode { get; set; } = FallbackMode.None;
    public string? InputFallbackEndpointId { get; set; }
    public string? InputFallbackDisplayNameSnapshot { get; set; }
}

public sealed class KnownAudioDevice
{
    public string EndpointId { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public AudioDirection Direction { get; set; }
    public AudioDeviceState LastState { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UiPreferences
{
    public bool UseDarkTheme { get; set; } = true;
}

public sealed class DiagnosticsPreferences
{
    public bool IncludeEndpointIds { get; set; } = true;
    public int EventCount { get; set; } = 20;
}
