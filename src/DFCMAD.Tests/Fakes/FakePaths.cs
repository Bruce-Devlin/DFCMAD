using DFCMAD.Core.Services;

namespace DFCMAD.Tests.Fakes;

internal sealed class FakePaths : IAppPaths
{
    public FakePaths(string root)
    {
        AppDataDirectory = root;
        SettingsFilePath = Path.Combine(root, "settings.json");
        LogDirectory = Path.Combine(root, "Logs");
    }

    public string AppDataDirectory { get; }
    public string SettingsFilePath { get; }
    public string LogDirectory { get; }
}
