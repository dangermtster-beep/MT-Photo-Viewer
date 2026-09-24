# MT Photo Viewer

<p align="center">
  <img src="MT.PhotoViewer/Assets/Logo/MT_PhotoViewer_Logo_Banner.png" alt="MT Photo Viewer" width="720"/>
</p>

Windows Fotoğraf Görüntüleyicisi kadar sade, daha modern bir masaüstü fotoğraf görüntüleyici.
`MT_Photo_Viewer_Implementation_Guide_With_Logo.md` dosyasındaki tasarım dokümanına göre geliştirilmiştir.

**C# · .NET 8 · WPF · MVVM · WPF-UI (Fluent kontroller)**

---

## Derleme ve çalıştırma

```bash
dotnet run --project MT.PhotoViewer
```

## Sürüm çıkarma ve güncelleme

Sürümün **tek kaynağı** `MT.PhotoViewer/MT.PhotoViewer.csproj` içindeki `<Version>`'dır; kurulum,
Hakkında ekranı ve güncelleme kontrolü hep oradan okur. Değişiklik geçmişi: [CHANGELOG.md](CHANGELOG.md).

**Yeni sürüm (ör. 1.0.1 → 1.0.2):**

1. `MT.PhotoViewer/Assets/ReleaseNotes.json` dosyasının **en üstüne** yeni sürümün notlarını ekle
   (kullanıcı bunu "Yenilikler" penceresinde görür).
2. Sürümü artırıp kurulumu derle:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -Bump patch
   ```

   `patch` hata düzeltmesi (1.0.2), `minor` yeni özellik (1.1.0), `major` büyük değişiklik (2.0.0).
3. GitHub Releases'a yayınla:

   ```bash
   powershell -ExecutionPolicy Bypass -File tools/publish-release.ps1
   ```

Script, **notu yazılmamış** ya da **öncekinden düşük** bir sürümü derlemeyi reddeder. Çıktı:
`setup/output/MT_Photo_Viewer_Setup_<sürüm>.exe` (~53 MB, .NET 8 gömülü) ve `version.json`.

**Kullanıcı tarafında:** uygulama açılışta (Ayarlar'dan kapatılabilir) GitHub'daki en son
`version.json`'u okur; yeni sürüm varsa başlık çubuğunda **"Güncelleme var · vX.Y.Z"** rozeti çıkar.
Tıklanınca yenilikler gösterilir, "Şimdi Güncelle" kurulumu indirir, **SHA-256 ile doğrular**,
sessiz kurar ve uygulamayı yeniden açar. Güncellemeden sonraki ilk açılışta "Yenilikler" penceresi
bir kez gösterilir. Güncelleme adresi csproj'daki `<GitHubRepo>` ayarından gelir (depo herkese açık olmalı).

Hedef makinede .NET 8 Desktop Runtime zaten varsa çok daha küçük bir kurulum için:

```bash
powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1 -FrameworkDependent
```

Kurulum yaptıkları (hepsi kaldırıldığında geri alınır):

- `%ProgramFiles%\MT Photo Viewer` altına kurar, Başlat menüsü kısayolu ekler (masaüstü kısayolu isteğe bağlı)
- **ProgID** `MT.PhotoViewer.Image` ve `Applications\MT.PhotoViewer.exe` kaydı → *Birlikte Aç* listesinde görünür
- Desteklenen 9 uzantı için `OpenWithProgids` — varsayılan uygulamayı **gasp etmez**
- `RegisteredApplications` + `Capabilities` → Ayarlar › Varsayılan uygulamalar'da listelenir
- **Sağ tık menüsü:** resim dosyalarında *"MT Photo Viewer ile Aç"*, klasörlerde ve klasör boşluğunda *"MT Photo Viewer ile Görüntüle"*
- `App Paths` kaydı → Win+R ile `MT.PhotoViewer` yazılarak açılır

> Windows 11'de klasik sağ tık verb'leri **"Diğer seçenekleri göster"** (Shift+F10) altında çıkar.
> Üstteki yeni menüde görünmesi ancak MSIX paketi + `IExplorerCommand` ile mümkündür.

---

Sadece uygulamayı yayınlamak için:

```bash
dotnet publish MT.PhotoViewer -c Release -r win-x64 --self-contained false
```

Komut satırından bir fotoğrafla açmak için:

```bash
MT.PhotoViewer.exe "D:\Images\road.jpg"
```

---

## Klavye kısayolları

| İşlem | Kısayol |
|---|---|
| Dosya Aç | `Ctrl + O` |
| Kopyala | `Ctrl + C` |
| Yazdır | `Ctrl + P` |
| Önceki / Sonraki | `←` / `→` (veya `Space`, mouse yan tuşları) |
| Yakınlaştır / Uzaklaştır | `+` / `-` (veya mouse wheel) |
| Ekrana Sığdır | `Ctrl + 0` |
| %100 Gerçek Boyut | `Ctrl + 1` |
| Sağa / Sola Döndür | `R` / `Shift + R` |
| Slayt Gösterisi başlat / durdur | `F5` |
| Tam Ekran / Çıkış | `F11` / `Esc` |
| Sil (Geri Dönüşüm Kutusu) | `Delete` |
| Özellikler | `Alt + Enter` |
| Fotoğraf Bilgileri paneli | `I` |
| Hakkında | `F1` |

Fotoğraf üzerinde çift tıklama **Ekrana Sığdır ⇄ %100** arasında geçiş yapar.
Sol tuşla sürükleme pan yapar. Zoom her zaman imlecin bulunduğu noktayı sabit tutar.

---

## Desteklenen formatlar

`.jpg` `.jpeg` `.png` `.bmp` `.gif` `.webp` `.tif` `.tiff` `.ico`

WEBP, Windows codec'i gerekmeden **SixLabors.ImageSharp** ile çözülür. `.heic`, `.avif` ve RAW
uzantıları klasör indexine dahil edilir; açılabilmeleri ilgili Windows codec'inin kurulu olmasına bağlıdır.

---

## Proje yapısı

```
MT.PhotoViewer/
├── App.xaml(.cs)              DI konteyneri, tema yükleme, komut satırı argümanı
├── MainWindow.xaml(.cs)       Title bar, navbar, fotoğraf alanı, tam ekran, kısayollar
├── Assets/Logo/               Logo (24×24 ikon, banner, .ico)
├── Models/                    ImageFile, ImageMetadata, AppSettings
├── ViewModels/                MainViewModel, InfoPanelViewModel, SettingsViewModel
├── Views/                     InfoPanel, Settings, About, Print, Copy, Email, Properties
├── Controls/                  ZoomableImage (zoom / pan / rotate)
├── Services/                  ImageLoader, FolderScanner, Metadata, Clipboard,
│                              Print, File, Email, Wallpaper, Theme, Settings, Ui
├── Helpers/                   NaturalSortComparer, Converters, MonitorHelper, CommandGuard
└── Themes/                    Dark, Light, Menu / Button / Toolbar stilleri
```

Dosya işlemleri yalnızca `Services/` katmanında yapılır; görsel decode UI thread'i bloke etmez;
hatalar `CommandGuard` üzerinden kullanıcıya sade mesajlarla gösterilir (stack trace gösterilmez).

---

## Notlar

- **Thumbnail şeridi yoktur** — fotoğraf ana içeriktir, kontroller ikincildir.
- Bir resim açıldığında klasördeki diğer görseller doğal ad sıralamasıyla indexlenir
  (`1.jpg, 2.jpg, 10.jpg`), komşu görseller (ileri 2, geri 1) arka planda ön yüklenir.
- Her görsel iki boyutta hazırlanır: ekranda sığdırılmış görünümde **monitör boyutunda bir
  önizleme** çizilir, yakınlaştırınca tam çözünürlüğe geçilir. Decode, EXIF dönüşü ve
  küçültme tamamen arka planda yapılır; geçişte UI thread'i yalnızca hazır pikselleri çizer.
- `FileSystemWatcher` ile klasördeki değişiklikler listeye yansır.
- Silme işlemi her zaman Geri Dönüşüm Kutusu'na taşır, kalıcı silme yapmaz.
- **EXIF Orientation** (etiket 274) decode sırasında piksellere uygulanır — telefonla çekilen
  dikey fotoğraflar yan yatmadan, orijinal hâliyle açılır. Aynı düzeltme duvar kağıdı ve
  e-posta eki üretiminde de uygulanır.
- **Slayt gösterisi** (`F5` veya toolbar'daki ▷) tam ekranda başlar, klasörün sonuna gelince
  başa sarar; `Esc` hem gösteriyi durdurur hem tam ekrandan çıkar. Bekleme süresi
  Ayarlar → Görüntüleme altından 1–30 saniye arasında ayarlanır.
- Ayarlar `%AppData%\MT.PhotoViewer\settings.json` içinde saklanır.
- **WPF-UI** yalnızca standart kontrolleri (TextBox, ComboBox, CheckBox, RadioButton, Slider,
  ScrollBar, ToolTip) Fluent görünüme çevirir. Title bar, navbar, floating toolbar ve fotoğraf
  alanı rehberdeki renklerle (§29–30) elle stillendirilmiş haliyle kalır; tema değişiminde
  `ThemeService` hem kendi sözlüğünü hem de `ApplicationThemeManager`'ı senkron tutar.
- Logo varyantları `tools/make-assets.ps1` ile `logo.png` kaynağından üretilir.

---

Developed by **Metin TUNÇER**
