using System.Collections.ObjectModel;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed class ServerDetailsPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }
    public ServerItemViewModel Server { get; }
    public ObservableCollection<ServerRequirementViewModel> Requirements => Shell.SelectedServerRequirements;
    public ObservableCollection<ServerRequirementViewModel> MissingItems => Shell.SelectedServerMissingItems;
    public ServerRequirementViewModel? ScenarioRequirement => Shell.SelectedServerScenario;

    public ServerDetailsPageViewModel(MainWindowViewModel shell, ServerItemViewModel server)
    {
        Shell = shell;
        Server = server;
    }
}
