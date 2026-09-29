using DFCMAD.Core.Services;
using DFCMAD.Core.Startup;

namespace DFCMAD.Tests.Fakes;

internal sealed class FakeRunRegistry : IRunRegistry
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetValue(string name) => Values.TryGetValue(name, out var value) ? value : null;

    public void SetValue(string name, string value) => Values[name] = value;

    public void DeleteValue(string name) => Values.Remove(name);
}

internal sealed class FakeStartupRegistrationService : IStartupRegistrationService
{
    public bool Enabled { get; set; }

    public bool IsEnabled() => Enabled;
    public void Enable() => Enabled = true;
    public void Disable() => Enabled = false;
}
