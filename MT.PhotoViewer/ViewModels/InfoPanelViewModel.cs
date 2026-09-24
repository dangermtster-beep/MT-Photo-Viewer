using CommunityToolkit.Mvvm.ComponentModel;
using MT.PhotoViewer.Models;

namespace MT.PhotoViewer.ViewModels;

/// <summary>Sağdan kayan fotoğraf bilgi paneli (Guide §24).</summary>
public partial class InfoPanelViewModel : ObservableObject
{
    [ObservableProperty]
    private ImageMetadata? metadata;

    [ObservableProperty]
    private bool hasExif;

    public void Load(ImageMetadata meta)
    {
        Metadata = meta;
        HasExif = meta.Camera is not null
                  || meta.Iso is not null
                  || meta.Exposure is not null
                  || meta.Aperture is not null
                  || meta.FocalLength is not null;
    }

    public void Clear()
    {
        Metadata = null;
        HasExif = false;
    }
}
