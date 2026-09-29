using DFCMAD.Core.Models;
using DFCMAD.Core.Services;

namespace DFCMAD.Tests.Fakes;

internal sealed class FakeEnforcementService : IAudioEnforcementService
{
    public EnforcementState State { get; set; } = new() { Status = EnforcementStatus.Enforcing, StatusText = "Enforcing selected devices" };
    public List<EnforcementEvent> Events { get; } = [];
    public IReadOnlyList<EnforcementEvent> RecentEvents => Events;

    public event EventHandler<EnforcementState>? StateChanged;
    public event EventHandler<EnforcementEvent>? EnforcementEventRecorded;

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task EnforceNowAsync(EnforcementReason reason, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task PauseAsync(TimeSpan duration, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task ResumeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void RaiseStateChanged() => StateChanged?.Invoke(this, State);
    public void RaiseEvent(EnforcementEvent item) => EnforcementEventRecorded?.Invoke(this, item);
}
