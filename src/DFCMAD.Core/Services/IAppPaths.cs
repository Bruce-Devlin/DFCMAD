namespace DFCMAD.Core.Services;

public interface IAppPaths
{
    string AppDataDirectory { get; }
    string SettingsFilePath { get; }
    string LogDirectory { get; }
}

public sealed class AppPaths : IAppPaths
{
    public AppPaths(string? appDataDirectory = null)
    {
        AppDataDirectory = appDataDirectory
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DFCMAD");
        SettingsFilePath = Path.Combine(AppDataDirectory, "settings.json");
        LogDirectory = Path.Combine(AppDataDirectory, "Logs");
    }

    public string AppDataDirectory { get; }
    public string SettingsFilePath { get; }
    public string LogDirectory { get; }
}
