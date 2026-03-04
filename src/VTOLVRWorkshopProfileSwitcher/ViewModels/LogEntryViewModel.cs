using System;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class LogEntryViewModel
{
    public string Text { get; }
    public bool IsWarning => Text.Contains("[WARNING]", System.StringComparison.OrdinalIgnoreCase);
    public bool IsInfo => Text.Contains("[INFO]", System.StringComparison.OrdinalIgnoreCase);

    public LogEntryViewModel(string text)
    {
        Text = text;
    }
}
