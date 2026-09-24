using System.Windows;

namespace MT.PhotoViewer.Views;

public enum AboutAction
{
    None,
    ShowReleaseNotes,
    CheckForUpdates
}

public partial class AboutWindow : Window
{
    public AboutWindow(string version, bool updatesConfigured)
    {
        InitializeComponent();

        VersionText.Text = $"Sürüm {version}";
        CheckUpdatesButton.Visibility = updatesConfigured ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Pencere kapandıktan sonra çağıranın yapması istenen iş.</summary>
    public AboutAction RequestedAction { get; private set; }

    private void ReleaseNotes_Click(object sender, RoutedEventArgs e) => CloseWith(AboutAction.ShowReleaseNotes);

    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => CloseWith(AboutAction.CheckForUpdates);

    private void CloseWith(AboutAction action)
    {
        RequestedAction = action;
        Close();
    }
}
