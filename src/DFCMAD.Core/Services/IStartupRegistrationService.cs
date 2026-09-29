namespace DFCMAD.Core.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}
