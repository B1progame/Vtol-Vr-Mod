namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class BackupsPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public BackupsPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
