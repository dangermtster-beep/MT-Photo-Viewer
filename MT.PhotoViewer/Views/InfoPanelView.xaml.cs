using System.Windows;
using System.Windows.Controls;

namespace MT.PhotoViewer.Views;

public partial class InfoPanelView : UserControl
{
    public InfoPanelView()
    {
        InitializeComponent();
        CloseButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CloseRequested;
}
