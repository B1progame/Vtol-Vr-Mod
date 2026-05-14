using Avalonia.Media.Imaging;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed class ServerRequirementViewModel : IDisposable
{
    private const int ThumbnailDecodeWidth = 384;

    public ServerRequirement Source { get; }
    public string WorkshopId => Source.WorkshopId;
    public string Title => Source.Title;
    public string Subtitle => Source.Subtitle;
    public bool IsInstalled => Source.IsInstalled;
    public bool IsScenario => Source.IsScenario;
    public string StatusText => IsInstalled ? "Installed" : "Missing";
    public string KindText => IsScenario ? "Scenario" : "Mod";
    public Bitmap? ThumbnailImage
    {
        get
        {
            if (!_thumbnailLoadAttempted)
            {
                _thumbnailLoadAttempted = true;
                _thumbnailLease = ViewModelImageLoader.TryAcquireBitmap(Source.ThumbnailPath, ThumbnailDecodeWidth);
                _thumbnailImage = _thumbnailLease?.Bitmap;
            }

            return _thumbnailImage;
        }
    }
    private Bitmap? _thumbnailImage;
    private ViewModelImageLoader.BitmapLease? _thumbnailLease;
    private bool _thumbnailLoadAttempted;
    private bool _disposed;

    public ServerRequirementViewModel(ServerRequirement source)
    {
        Source = source;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _thumbnailLease?.Dispose();
        _thumbnailLease = null;
        _thumbnailImage = null;
    }
}
