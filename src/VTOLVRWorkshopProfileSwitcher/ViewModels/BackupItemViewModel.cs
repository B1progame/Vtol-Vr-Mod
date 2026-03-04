using System;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class BackupItemViewModel
{
    public string FilePath { get; }
    public string FileName { get; }
    public DateTime TimestampUtc { get; }
    public string DisplayTimestamp => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    public BackupItemViewModel(string filePath, DateTime timestampUtc)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileName(filePath);
        TimestampUtc = timestampUtc;
    }
}
