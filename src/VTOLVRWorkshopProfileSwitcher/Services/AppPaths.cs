using System;
using System.IO;

namespace VTOLVRWorkshopProfileSwitcher.Services;

public sealed class AppPaths
{
    public string BaseDir { get; }
    public string ProfilesDir { get; }
    public string BackupsDir { get; }
    public string LogsDir { get; }
    public string SettingsFile { get; }
    public string SessionLogFile { get; }
    public string LogFile => SessionLogFile;

    public AppPaths()
    {
        BaseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VTOLVR-WorkshopProfiles");
        ProfilesDir = Path.Combine(BaseDir, "profiles");
        BackupsDir = Path.Combine(BaseDir, "backups");
        LogsDir = Path.Combine(BaseDir, "logs");
        SettingsFile = Path.Combine(BaseDir, "settings.json");

        Directory.CreateDirectory(BaseDir);
        Directory.CreateDirectory(ProfilesDir);
        Directory.CreateDirectory(BackupsDir);
        Directory.CreateDirectory(LogsDir);

        var sessionStamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
        SessionLogFile = Path.Combine(LogsDir, $"app-{sessionStamp}.log");
    }
}
