using Avalonia.Media.Imaging;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class ServerRequirementViewModel
{
    public ServerRequirement Source { get; }
    public string WorkshopId => Source.WorkshopId;
    public string Title => Source.Title;
    public string Subtitle => Source.Subtitle;
    public bool IsInstalled => Source.IsInstalled;
    public bool IsScenario => Source.IsScenario;
    public string StatusText => IsInstalled ? "Installed" : "Missing";
    public string KindText => IsScenario ? "Scenario" : "Mod";
    public Bitmap? ThumbnailImage { get; }

    public ServerRequirementViewModel(ServerRequirement source)
    {
        Source = source;
        ThumbnailImage = ViewModelImageLoader.TryLoadBitmap(source.ThumbnailPath);
    }
}
