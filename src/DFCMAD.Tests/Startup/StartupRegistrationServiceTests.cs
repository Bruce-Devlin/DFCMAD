using DFCMAD.Core.Startup;
using DFCMAD.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace DFCMAD.Tests.Startup;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildCommand_QuotesExecutablePath()
    {
        var command = StartupRegistrationService.BuildCommand(@"C:\Program Files\DFCMAD\DFCMAD.exe");

        Assert.Equal("\"C:\\Program Files\\DFCMAD\\DFCMAD.exe\" --tray", command);
    }

    [Fact]
    public void Enable_WritesRunKeyCommand()
    {
        var registry = new FakeRunRegistry();
        var service = new StartupRegistrationService(
            registry,
            NullLogger<StartupRegistrationService>.Instance,
            () => @"C:\Apps\DFCMAD.exe");

        service.Enable();

        Assert.True(service.IsEnabled());
        Assert.Equal("\"C:\\Apps\\DFCMAD.exe\" --tray", registry.Values[StartupRegistrationService.RegistryValueName]);
    }

    [Fact]
    public void Disable_RemovesRunKeyCommand()
    {
        var registry = new FakeRunRegistry();
        var service = new StartupRegistrationService(
            registry,
            NullLogger<StartupRegistrationService>.Instance,
            () => @"C:\Apps\DFCMAD.exe");

        service.Enable();
        service.Disable();

        Assert.False(service.IsEnabled());
        Assert.False(registry.Values.ContainsKey(StartupRegistrationService.RegistryValueName));
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenPathChanged()
    {
        var registry = new FakeRunRegistry();
        registry.SetValue(StartupRegistrationService.RegistryValueName, "\"C:\\Old\\DFCMAD.exe\" --tray");
        var service = new StartupRegistrationService(
            registry,
            NullLogger<StartupRegistrationService>.Instance,
            () => @"C:\New\DFCMAD.exe");

        Assert.False(service.IsEnabled());
    }
}
