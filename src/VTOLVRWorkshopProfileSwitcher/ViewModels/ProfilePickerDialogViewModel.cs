using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class ProfilePickerDialogViewModel : ViewModelBase
{
    public ObservableCollection<ProfileItemViewModel> Profiles { get; }
    public ProfileItemViewModel? SelectedProfile { get; set; }

    public ProfilePickerDialogViewModel(IReadOnlyList<ProfileItemViewModel> profiles, ProfileItemViewModel? selectedProfile)
    {
        Profiles = new ObservableCollection<ProfileItemViewModel>(profiles);
        SelectedProfile = selectedProfile ?? (Profiles.Count > 0 ? Profiles[0] : null);
    }
}
