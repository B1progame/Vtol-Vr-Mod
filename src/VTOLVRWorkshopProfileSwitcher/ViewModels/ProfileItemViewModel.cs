using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Material.Icons;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class ProfileItemViewModel : ObservableObject
{
    public ModProfile Source { get; }

    public string Name => Source.Name;
    public string Notes => Source.Notes;
    public string Summary => $"{Source.EnabledMods.Count} enabled";
    public MaterialIconKind IconKind => ProfileIconCatalog.GetKind(Source.IconKind);

    public ObservableCollection<string> EnabledMods { get; }

    public ProfileItemViewModel(ModProfile source)
    {
        Source = source;
        EnabledMods = new ObservableCollection<string>(source.EnabledMods);
    }
}
