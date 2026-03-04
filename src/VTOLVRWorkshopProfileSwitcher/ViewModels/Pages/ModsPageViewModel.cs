namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class ModsPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public ModsPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
