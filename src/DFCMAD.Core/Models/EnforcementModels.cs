namespace DFCMAD.Core.Models;

public sealed record EnforcementState
{
    public EnforcementStatus Status { get; init; } = EnforcementStatus.Stopped;
    public bool IsPaused => Status == EnforcementStatus.Paused;
    public DateTimeOffset? PausedUntil { get; init; }
    public DateTimeOffset? LastEnforcementUtc { get; init; }
    public DateTimeOffset? LastDetectedWindowsChangeUtc { get; init; }
    public string StatusText { get; init; } = "Stopped";
    public string? LastError { get; init; }
}

public sealed record EnforcementEvent
{
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
    public EnforcementReason Reason { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string? Error { get; init; }
}
