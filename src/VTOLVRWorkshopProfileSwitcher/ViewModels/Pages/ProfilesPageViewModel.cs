namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class ProfilesPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public ProfilesPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
