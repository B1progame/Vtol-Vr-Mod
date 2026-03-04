namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class DashboardPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public DashboardPageViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
    }
}
