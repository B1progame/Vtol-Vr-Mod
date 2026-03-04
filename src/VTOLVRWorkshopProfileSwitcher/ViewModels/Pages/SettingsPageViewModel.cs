namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class SettingsPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public SettingsPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
