namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class LogsPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public LogsPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
