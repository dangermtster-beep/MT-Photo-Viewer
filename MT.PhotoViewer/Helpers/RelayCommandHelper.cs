using System.Windows;

namespace MT.PhotoViewer.Helpers;

/// <summary>
/// Merkezî hata yönetimi (Guide §50). Kullanıcıya stack trace gösterilmez,
/// sadece anlaşılır bir mesaj verilir.
/// </summary>
public static class CommandGuard
{
    public static event Action<Exception>? ErrorLogged;

    public static void Run(Action action, string userMessage)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Report(ex, userMessage);
        }
    }

    public static async Task RunAsync(Func<Task> action, string userMessage)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Report(ex, userMessage);
        }
    }

    public static void Report(Exception ex, string userMessage)
    {
        ErrorLogged?.Invoke(ex);
        System.Diagnostics.Debug.WriteLine(ex);

        MessageBox.Show(
            userMessage,
            "MT Photo Viewer",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
