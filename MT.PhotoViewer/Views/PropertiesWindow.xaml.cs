using System.Windows;
using MT.PhotoViewer.Models;
using MT.PhotoViewer.ViewModels;

namespace MT.PhotoViewer.Views;

public partial class PropertiesWindow : Window
{
    public PropertiesWindow(ImageMetadata metadata)
    {
        InitializeComponent();

        var vm = new InfoPanelViewModel();
        vm.Load(metadata);

        Panel.DataContext = vm;
        Panel.CloseRequested += (_, _) => Close();

        Title = $"{metadata.FileName} — Özellikler";
    }
}
