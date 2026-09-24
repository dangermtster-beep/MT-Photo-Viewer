using System.IO;
using System.Windows;
using Microsoft.Win32;
using MT.PhotoViewer.Services;

namespace MT.PhotoViewer.Views;

public partial class CopyDialog : Window
{
    public CopyDialog(string sourcePath)
    {
        InitializeComponent();

        FileNameBox.Text = FileService.SuggestCopyName(sourcePath);
        FolderBox.Text = Path.GetDirectoryName(sourcePath) ?? string.Empty;

        BrowseButton.Click += (_, _) =>
        {
            var dialog = new OpenFolderDialog { Title = "Konum Seçin", InitialDirectory = FolderBox.Text };
            if (dialog.ShowDialog(this) == true)
                FolderBox.Text = dialog.FolderName;
        };

        SaveButton.Click += (_, _) => Commit();

        Loaded += (_, _) =>
        {
            FileNameBox.Focus();
            FileNameBox.Select(0, Path.GetFileNameWithoutExtension(FileNameBox.Text).Length);
        };
    }

    public CopyRequest? Result { get; private set; }

    private void Commit()
    {
        string name = FileNameBox.Text.Trim();
        string folder = FolderBox.Text.Trim();

        if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(this, "Geçerli bir dosya adı girin.", "Kopya Oluştur",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            MessageBox.Show(this, "Geçerli bir klasör seçin.", "Kopya Oluştur",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string target = Path.Combine(folder, name);

        if (File.Exists(target) &&
            MessageBox.Show(this, "Bu dosya zaten var. Üzerine yazılsın mı?", "Kopya Oluştur",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        Result = new CopyRequest(name, folder);
        DialogResult = true;
    }
}
