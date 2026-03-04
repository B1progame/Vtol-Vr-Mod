using System.Collections.ObjectModel;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels.Pages;

public sealed partial class ProfileDetailsPageViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }
    public ProfileItemViewModel Profile { get; }

    public ObservableCollection<ProfileModEntryViewModel> Mods => Shell.FilteredProfileMods;

    public ProfileDetailsPageViewModel(MainWindowViewModel shell, ProfileItemViewModel profile)
    {
        Shell = shell;
        Profile = profile;
    }
}
