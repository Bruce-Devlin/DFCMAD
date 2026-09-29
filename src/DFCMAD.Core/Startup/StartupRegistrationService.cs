using DFCMAD.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Runtime.Versioning;

namespace DFCMAD.Core.Startup;

public interface IRunRegistry
{
    string? GetValue(string name);
    void SetValue(string name, string value);
    void DeleteValue(string name);
}

[SupportedOSPlatform("windows")]
public sealed class CurrentUserRunRegistry : IRunRegistry
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? GetValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(name) as string;
    }

    public void SetValue(string name, string value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void DeleteValue(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    public const string RegistryValueName = "DFCMAD";
    private readonly IRunRegistry _registry;
    private readonly ILogger<StartupRegistrationService> _logger;
    private readonly Func<string> _executablePathProvider;

    public StartupRegistrationService(IRunRegistry registry, ILogger<StartupRegistrationService> logger)
        : this(registry, logger, () => Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0])
    {
    }

    public StartupRegistrationService(IRunRegistry registry, ILogger<StartupRegistrationService> logger, Func<string> executablePathProvider)
    {
        _registry = registry;
        _logger = logger;
        _executablePathProvider = executablePathProvider;
    }

    public bool IsEnabled()
    {
        var existing = _registry.GetValue(RegistryValueName);
        return string.Equals(existing, BuildCommand(_executablePathProvider()), StringComparison.OrdinalIgnoreCase);
    }

    public void Enable()
    {
        var command = BuildCommand(_executablePathProvider());
        _registry.SetValue(RegistryValueName, command);
        _logger.LogInformation("Startup registration enabled with command {Command}", command);
    }

    public void Disable()
    {
        _registry.DeleteValue(RegistryValueName);
        _logger.LogInformation("Startup registration disabled.");
    }

    public static string BuildCommand(string executablePath)
    {
        var escaped = executablePath.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\" --tray";
    }
}
