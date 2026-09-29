namespace DFCMAD.Core.Services;

public interface IDiagnosticsService
{
    Task<string> BuildDiagnosticsAsync(CancellationToken cancellationToken);
}
