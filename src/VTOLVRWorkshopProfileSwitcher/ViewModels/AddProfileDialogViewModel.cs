using CommunityToolkit.Mvvm.ComponentModel;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class AddProfileDialogViewModel : ViewModelBase
{
    public System.Collections.ObjectModel.ObservableCollection<ProfileIconOption> ProfileIconOptions { get; } =
        ProfileIconCatalog.Options;

    [ObservableProperty]
    private bool activateAllMods;

    [ObservableProperty]
    private string profileName = string.Empty;

    [ObservableProperty]
    private string notes = string.Empty;

    [ObservableProperty]
    private ProfileIconOption selectedProfileIcon = ProfileIconCatalog.GetOption(ProfileIconCatalog.DefaultIconName);
}

public sealed record AddProfileDialogResult(string Name, string Notes, bool ActivateAllMods, string IconKind);
