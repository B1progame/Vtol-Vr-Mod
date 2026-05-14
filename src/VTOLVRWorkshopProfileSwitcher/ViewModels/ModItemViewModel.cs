using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VTOLVRWorkshopProfileSwitcher.Models;

namespace VTOLVRWorkshopProfileSwitcher.ViewModels;

public sealed partial class ModItemViewModel : ObservableObject, IDisposable
{
    private const int ThumbnailDecodeWidth = 384;

    public WorkshopMod Source { get; }

    [ObservableProperty]
    private bool isEnabled;

    [ObservableProperty]
    private bool isSelected;

    public string WorkshopId => Source.WorkshopId;
    public string ModName => Source.DisplayName;
    public string FolderName => Source.FolderName;
    public string DownloadCountText => Source.DownloadCount is > 0
        ? $"{Source.DownloadCount.Value.ToString("N0", CultureInfo.InvariantCulture)} downloads"
        : "downloads: n/a";
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

    public IAsyncRelayCommand DeleteCommand { get; }

    private readonly Func<ModItemViewModel, Task>? _onDelete;
    private Bitmap? _thumbnailImage;
    private ViewModelImageLoader.BitmapLease? _thumbnailLease;
    private bool _thumbnailLoadAttempted;
    private bool _disposed;

    public ModItemViewModel(WorkshopMod source, Func<ModItemViewModel, Task>? onDelete = null)
    {
        Source = source;
        isEnabled = source.IsEnabled;
        _onDelete = onDelete;
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
    }

    private async Task DeleteAsync()
    {
        if (_onDelete is not null)
        {
            await _onDelete(this);
        }
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
