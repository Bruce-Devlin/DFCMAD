using DFCMAD.Core.Models;

namespace DFCMAD.Core.Services;

public interface IAudioEnforcementService
{
    EnforcementState State { get; }
    IReadOnlyList<EnforcementEvent> RecentEvents { get; }

    event EventHandler<EnforcementState>? StateChanged;
    event EventHandler<EnforcementEvent>? EnforcementEventRecorded;

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task EnforceNowAsync(EnforcementReason reason, CancellationToken cancellationToken);
    Task PauseAsync(TimeSpan duration, CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);
}
