namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class ProfileModEntryViewModel
{
    public string WorkshopId { get; }
    public string Name { get; }
    public bool IsInstalled { get; }
    public bool IsActiveInProfile { get; }
    public bool IsDependencyOnly { get; }
    public string ProfileStateText => IsDependencyOnly ? "Auto Dependency" : $"Active In Profile: {IsActiveInProfile}";
    public ModItemViewModel? InstalledMod { get; }

    public ProfileModEntryViewModel(
        string workshopId,
        string name,
        bool isInstalled,
        bool isActiveInProfile,
        bool isDependencyOnly,
        ModItemViewModel? installedMod)
    {
        WorkshopId = workshopId;
        Name = name;
        IsInstalled = isInstalled;
        IsActiveInProfile = isActiveInProfile;
        IsDependencyOnly = isDependencyOnly;
        InstalledMod = installedMod;
    }
}
