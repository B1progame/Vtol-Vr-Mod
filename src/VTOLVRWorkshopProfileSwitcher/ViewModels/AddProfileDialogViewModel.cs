using CommunityToolkit.Mvvm.ComponentModel;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class AddProfileDialogViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool activateAllMods;

    [ObservableProperty]
    private string profileName = string.Empty;

    [ObservableProperty]
    private string notes = string.Empty;
}

public sealed record AddProfileDialogResult(string Name, string Notes, bool ActivateAllMods);
