namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class ServersPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public ServersPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
