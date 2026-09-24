using System.Windows.Media.Imaging;

namespace MT.PhotoViewer.Models;

/// <summary>
/// Çözülmüş bir görsel.
/// <para><see cref="Full"/>: tam çözünürlük — yakınlaştırma, kopyalama ve yazdırma için.</para>
/// <para><see cref="Preview"/>: ekran boyutuna küçültülmüş kopya — ekrana sığdırılmış görünümde
/// çizilir; GPU'ya 48 MB yerine birkaç MB yüklendiği için geçişler takılmaz.
/// Görsel zaten küçükse <see cref="Full"/> ile aynı nesnedir.</para>
/// Her ikisine de EXIF Orientation uygulanmış ve donmuştur (Freeze).
/// </summary>
public sealed record LoadedImage(BitmapSource Full, BitmapSource Preview);
