using System.Reflection;

namespace DFCMAD.Core.Models;

public static class AppVersionInfo
{
    public static string Version =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)
        ?? "1.0.0";
}
