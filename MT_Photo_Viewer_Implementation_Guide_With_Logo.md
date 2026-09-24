# MT Photo Viewer — Implementation Guide
**Platform:** Windows Desktop  
**Technology:** C# + WPF + .NET 8  
**Architecture:** MVVM  
**Project Name:** `MT.PhotoViewer`  
**UI Direction:** Windows Fotoğraf Görüntüleyicisi mantığı + modern, sade, koyu arayüz  
**Thumbnail Şeridi:** YOK


<p align="center">
  <img src="Assets/Logo/MT_PhotoViewer_Logo_Banner.png" alt="MT Photo Viewer Logo" width="900"/>
</p>

> **Branding:** Ana uygulama logosu `Assets/Logo/MT_PhotoViewer_Logo.png` dosyasıdır.  
> Üst başlık çubuğunda logo **24×24 px** olarak, hemen yanında **MT Photo Viewer** metni ile kullanılmalıdır.

---

---

# 1. Proje Amacı

MT Photo Viewer; Windows Fotoğraf Görüntüleyicisi kadar hızlı ve sade, ancak daha modern görünümlü bir masaüstü fotoğraf görüntüleme uygulamasıdır.

Ana tasarım prensipleri:

- Fotoğraf ekranın mümkün olan en büyük alanını kullanmalıdır.
- Alt veya yan tarafta thumbnail şeridi bulunmamalıdır.
- Üstte klasik Windows Fotoğraf Görüntüleyicisi mantığında bir navbar bulunmalıdır.
- Navbar menüleri:
  - **Dosya**
  - **Yazdır**
  - **E-posta**
  - **Yaz**
  - **Aç**
- Alt bölümde yalnızca minimal, yüzen bir kontrol çubuğu olmalıdır.
- Dark tema varsayılan olmalıdır.
- Light tema ve Windows sistem teması ayrıca desteklenmelidir.
- Uygulama hızlı açılmalı ve büyük fotoğraflarda takılmamalıdır.
- Bir resim açıldığında aynı klasördeki diğer resimler otomatik algılanmalıdır.
- Önceki/sonraki resimler için thumbnail gerekmeden klavye ve yön butonları kullanılmalıdır.

---

# 2. Hedef Dosya Formatları

İlk sürümde:

- `.jpg`
- `.jpeg`
- `.png`
- `.bmp`
- `.gif`
- `.webp`
- `.tif`
- `.tiff`
- `.ico`

İkinci aşamada:

- `.heic`
- `.heif`
- `.avif`
- RAW formatları:
  - `.cr2`
  - `.cr3`
  - `.nef`
  - `.arw`
  - `.dng`

---

# 3. Ana UI Tasarımı

```text
┌─────────────────────────────────────────────────────────────────────────┐
│ Road_001.jpg — MT Photo Viewer                               ─  □  ×   │
├─────────────────────────────────────────────────────────────────────────┤
│ Dosya ▼    Yazdır ▼    E-posta    Yaz ▼    Aç ▼                       │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│                                                                         │
│                                                                         │
│                         FOTOĞRAF ALANI                                  │
│                                                                         │
│     ‹                                                           ›       │
│                                                                         │
│                                                                         │
│                                                                         │
│                                                                         │
│                 ┌───────────────────────────────────┐                   │
│                 │  −   100%   +   ⛶   ↶   ↷   ⋯  │                   │
│                 └───────────────────────────────────┘                   │
└─────────────────────────────────────────────────────────────────────────┘
```

---

# 4. Pencere Yapısı

Ana pencere:

`MainWindow.xaml`

Katman yapısı:

```text
MainWindow
│
├── Custom Title Bar
├── Top Navigation Bar
├── Image Viewer Area
│   ├── Image Canvas
│   ├── Previous Button
│   ├── Next Button
│   └── Loading Indicator
│
├── Floating Bottom Toolbar
├── Right Info Panel
└── Context Menu
```

---

# 5. Üst Title Bar

Sol tarafta marka alanı:

```text
[MT Logo 24×24]  MT Photo Viewer
```

Bir fotoğraf açık olduğunda:

```text
[MT Logo]  Road_Project_001.jpg — MT Photo Viewer
```

Logo dosyası:

```text
Assets/Logo/MT_PhotoViewer_Logo.png
```

WPF önerisi:

```xml
<StackPanel Orientation="Horizontal"
            VerticalAlignment="Center"
            Margin="10,0,0,0">
    <Image Source="/Assets/Logo/MT_PhotoViewer_Logo.png"
           Width="24"
           Height="24"
           Stretch="Uniform"/>
    <TextBlock Text="MT Photo Viewer"
               Margin="8,0,0,0"
               VerticalAlignment="Center"
               FontWeight="SemiBold"/>
</StackPanel>
```

> Title bar içinde geniş banner kullanılmamalıdır. Başlık çubuğunda **compact MT icon** kullanılacaktır. Geniş logo banner'ı yalnızca README/MD dokümanı, About ekranı ve isteğe bağlı splash ekranında kullanılacaktır.


Pencere başlığı:

```text
Road_Project_001.jpg — MT Photo Viewer
```

Dosya açık değilse:

```text
MT Photo Viewer
```

Sağ tarafta standart pencere kontrolleri:

```text
—   □   ×
```

İsteğe bağlı olarak custom title bar kullanılabilir.

Önerilen yükseklik:

```text
TitleBar Height = 34 px
Navbar Height   = 38 px
```

---

# 6. Navbar

Navbar klasik Windows Fotoğraf Görüntüleyicisi davranışını koruyacak ancak görsel olarak modernleştirilecektir.

```text
Dosya ▼    Yazdır ▼    E-posta    Yaz ▼    Aç ▼
```

Navbar:

- Hafif saydam
- İnce alt border
- Hover efekti
- Dark ve Light temaya uyumlu
- Mouse ile kolay erişilebilir
- Touch hedefleri minimum 34–38 px

---

# 7. Dosya Menüsü

```text
Dosya
────────────────────────────
Sil                       Del
Kopya Oluştur...
Kopyala                   Ctrl+C
Özellikler                Alt+Enter
────────────────────────────
Çıkış
```

## 7.1 Sil

Aktif resmi Windows Geri Dönüşüm Kutusu'na gönder.

Direkt kalıcı silme yapılmamalıdır.

Silme öncesinde isteğe bağlı onay:

```text
"Bu resmi Geri Dönüşüm Kutusu'na taşımak istiyor musunuz?"
```

## 7.2 Kopya Oluştur

Aşağıdaki pencere açılmalıdır:

```text
Kopya Oluştur

Dosya Adı:
Road_001_Copy.jpg

Konum:
D:\Resimler\Proje\

[ Gözat ]          [ İptal ] [ Kaydet ]
```

## 7.3 Kopyala

Aktif resmi Windows panosuna kopyalar.

Kısayol:

```text
Ctrl + C
```

## 7.4 Özellikler

Windows dosya özellikleri penceresini açabilir veya uygulamanın kendi modern bilgi penceresi kullanılabilir.

Kısayol:

```text
Alt + Enter
```

## 7.5 Çıkış

Uygulamayı kapatır.

---

# 8. Yazdır Menüsü

```text
Yazdır
────────────────────────────
Yazdır...                  Ctrl+P
Hızlı Yazdır
Sayfa Ayarları
Yazıcı Ayarları
```

## Yazdır ekranı

Seçenekler:

- Yazıcı seçimi
- Kağıt:
  - A4
  - A3
  - Letter
- Dikey
- Yatay
- Sayfaya sığdır
- Gerçek boyut
- Ortala
- Kenar boşlukları
- Baskı önizleme

---

# 9. E-posta

Navbar üzerinde doğrudan:

```text
E-posta
```

Tıklanınca küçük dialog:

```text
Fotoğraf Boyutu

(•) Orijinal
( ) Büyük
( ) Orta
( ) Küçük

Tahmini boyut: 4.8 MB

[ İptal ] [ E-posta Oluştur ]
```

Varsayılan Windows e-posta istemcisi açılabilir.

İlk sürümde desteklenemiyorsa menü pasif bırakılabilir ancak UI'da yer almalıdır.

---

# 10. Yaz Menüsü

Windows Fotoğraf Görüntüleyicisi'ndeki eski "Yaz" mantığı modernleştirilecektir.

```text
Yaz
────────────────────────────
Diske Yaz
USB Belleğe Kopyala
Klasöre Kopyala
```

CD/DVD desteği opsiyoneldir.

Ana kullanım:

- USB hedef seçimi
- Klasör hedef seçimi
- Dosyayı kopyalama

---

# 11. Aç Menüsü

```text
Aç
────────────────────────────
Dosya Aç...                Ctrl+O
Klasör Aç...
────────────────────────────
Paint ile Aç
Varsayılan Uygulamayla Aç
Uygulama Seç...
────────────────────────────
Dosya Konumunu Aç
```

## Dosya Aç

`OpenFileDialog`

Filtre:

```text
Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp;*.tif;*.tiff;*.ico
```

## Klasör Aç

Klasördeki desteklenen tüm görseller indexlenir.

Thumbnail gösterilmez.

---

# 12. Fotoğraf Görüntüleme Alanı

Fotoğraf alanı uygulamanın ana bölümüdür.

Arka plan:

```text
Dark:
#111214

Light:
#F2F2F2
```

Fotoğraf:

- Merkezde
- Oranı korunarak
- Varsayılan `Uniform`
- Kırpılmadan
- Kullanılabilir maksimum alana sığdırılmalı

---

# 13. Önceki / Sonraki Butonları

Sol ve sağ kenarda transparan navigation butonları:

```text
‹                    ›
```

Davranış:

- Mouse fotoğraf alanına geldiğinde görünür.
- Mouse uzaklaşınca düşük opacity.
- Fullscreen modunda birkaç saniye sonra tamamen gizlenir.

Klavye:

```text
← Önceki
→ Sonraki
```

---

# 14. Klasör Indexleme

Bir resim açıldığında örneğin:

```text
D:\ProjeFoto\Road_005.jpg
```

program aynı klasördeki diğer görselleri otomatik toplamalıdır.

Örnek:

```text
Road_001.jpg
Road_002.jpg
Road_003.png
Road_004.webp
Road_005.jpg
Road_006.jpg
```

Sıralama varsayılan olarak:

```text
Natural File Name Sort
```

Örnek:

```text
1.jpg
2.jpg
3.jpg
10.jpg
```

şeklinde olmalıdır.

---

# 15. Alt Floating Toolbar

Alt kontrol barı:

```text
−     100%     +     ⛶     ↶     ↷     ⋯
```

Konum:

- Alt merkez
- Fotoğrafın üzerinde floating
- Rounded corner
- Hafif transparency
- Hafif shadow

Önerilen görünüm:

```text
Background = #DD202226
CornerRadius = 12
Height = 44
```

---

# 16. Zoom Sistemi

Destek:

```text
10%
25%
50%
75%
100%
125%
150%
200%
300%
500%
1000%
```

Mouse Wheel:

```text
Wheel Up   = Zoom In
Wheel Down = Zoom Out
```

Zoom merkezi:

**Mouse imlecinin bulunduğu nokta**

olmalıdır.

Yani kullanıcının baktığı bölge zoom sırasında ekrandan kaçmamalıdır.

---

# 17. Zoom Kısayolları

```text
+              Zoom In
-              Zoom Out
Ctrl + +       Zoom In
Ctrl + -       Zoom Out
Ctrl + 0       Ekrana Sığdır
Ctrl + 1       %100 Gerçek Boyut
```

---

# 18. Pan / Sürükleme

Fotoğraf ekrandan büyük olduğunda:

```text
Sol Mouse + Drag
```

ile fotoğraf hareket ettirilebilmelidir.

Mouse cursor:

```text
Hand / Grab
```

olabilir.

---

# 19. Çift Tıklama

Fotoğraf üzerinde çift tıklama:

```text
Fit To Screen ⇄ 100%
```

arasında geçiş yapmalıdır.

---

# 20. Döndürme

Toolbar:

```text
↶ Sola Döndür
↷ Sağa Döndür
```

Kısayollar:

```text
R          Sağa Döndür
Shift + R  Sola Döndür
```

Döndürme:

```text
90°
180°
270°
0°
```

---

# 21. Tam Ekran

Kısayol:

```text
F11
```

Fullscreen modunda:

- Title bar gizlenir
- Navbar gizlenir
- Floating toolbar bir süre sonra gizlenir
- Mouse hareketinde toolbar tekrar görünür
- Siyah arka plan
- Fotoğraf ortalanır

Çıkış:

```text
Esc
```

---

# 22. `⋯` Menüsü

Floating toolbar son butonu:

```text
⋯
```

Açıldığında:

```text
Fotoğrafı Aç
Farklı Kaydet
Kopyala
Kopya Oluştur
────────────────────────
Duvar Kağıdı Yap
Dosya Konumunu Aç
────────────────────────
Fotoğraf Bilgileri
Düzenle
────────────────────────
Yazdır
────────────────────────
Sil
────────────────────────
Ayarlar
Hakkında
```

---

# 23. Sağ Tık Context Menu

Fotoğraf üzerinde sağ tık:

```text
Önceki
Sonraki
──────────────────────
Ekrana Sığdır
Gerçek Boyut
Yakınlaştır
Uzaklaştır
──────────────────────
Sola Döndür
Sağa Döndür
──────────────────────
Kopyala
Kopya Oluştur
Farklı Kaydet
──────────────────────
Yazdır
──────────────────────
Dosya Konumunu Aç
Özellikler
──────────────────────
Sil
```

---

# 24. Fotoğraf Bilgi Paneli

Sağdan kayan panel.

```text
┌────────────────────────────┐
│ FOTOĞRAF BİLGİLERİ      × │
│                            │
│ Road_Project_001.jpg       │
│                            │
│ 6048 × 4032                │
│ JPEG                       │
│ 12.8 MB                    │
│                            │
│ Kamera                     │
│ Canon EOS R6               │
│                            │
│ ISO                        │
│ 100                        │
│                            │
│ Pozlama                    │
│ 1/250 sec                  │
│                            │
│ Diyafram                   │
│ f/5.6                      │
│                            │
│ Odak                       │
│ 35 mm                      │
│                            │
│ Tarih                      │
│ 15.09.2026 14:35           │
│                            │
│ Konum                      │
│ GPS varsa göster           │
└────────────────────────────┘
```

Panel genişliği:

```text
320–360 px
```

---

# 25. EXIF Metadata

Önerilen NuGet:

```text
MetadataExtractor
```

Okunabilecek alanlar:

- Camera Make
- Camera Model
- Date Taken
- ISO
- Exposure
- Aperture
- Focal Length
- Orientation
- GPS
- DPI
- Color Profile

---

# 26. Açılış Ekranı

Fotoğraf açık değilse:

```text
                       MT
                  PHOTO VIEWER

           Fotoğraflarınızı hızlı,
          sade ve kaliteli görüntüleyin.

                 [ FOTOĞRAF AÇ ]

        veya resmi buraya sürükleyin

 JPG • PNG • WEBP • BMP • GIF • TIFF
```

Arka plan sade olmalıdır.

Thumbnail, galeri veya son dosyalar listesi gösterilmemelidir.

---

# 27. Drag & Drop

Ana pencereye resim sürüklendiğinde otomatik açılsın.

Örnek:

```csharp
AllowDrop="True"
```

Destek:

- Tek dosya
- Klasör
- Çoklu dosya

Çoklu dosyada listedeki ilk resim açılır.

---

# 28. Tema Sistemi

Tema seçenekleri:

```text
Dark
Light
Windows Sistem Teması
```

Varsayılan:

```text
Dark
```

---

# 29. Dark Tema

Ana renkler:

```text
Window Background : #111214
Navbar Background : #1C1E22
Toolbar Background: #E6222428
Text Primary      : #F4F4F4
Text Secondary    : #B6BAC2
Border            : #30333A
Hover             : #2A2D33
```

Accent:

Windows Accent Color kullanılabilir.

---

# 30. Light Tema

```text
Window Background : #F5F5F5
Navbar Background : #FFFFFF
Toolbar Background: #EEFFFFFF
Text Primary      : #181818
Text Secondary    : #666666
Border            : #D6D6D6
Hover             : #ECECEC
```

---

# 31. Ayarlar

```text
Ayarlar

Görünüm
  Tema
  Fotoğraf arka planı
  Kontrolleri otomatik gizle

Görüntüleme
  Başlangıç zoom davranışı
  Mouse wheel zoom
  Smooth zoom
  Yüksek kaliteli interpolation

Dosyalar
  Varsayılan uygulama
  Desteklenen formatlar

Performans
  Büyük resimler için ön yükleme
  Cache boyutu

Genel
  Son pencere konumunu hatırla
  Güncelleme kontrolü
```

---

# 32. Klavye Kısayolları

| İşlem | Kısayol |
|---|---|
| Dosya Aç | `Ctrl + O` |
| Kopyala | `Ctrl + C` |
| Yazdır | `Ctrl + P` |
| Önceki | `←` |
| Sonraki | `→` |
| Zoom In | `+` |
| Zoom Out | `-` |
| Ekrana Sığdır | `Ctrl + 0` |
| %100 | `Ctrl + 1` |
| Sağa Döndür | `R` |
| Sola Döndür | `Shift + R` |
| Tam Ekran | `F11` |
| Fullscreen Çıkış | `Esc` |
| Sil | `Delete` |
| Özellikler | `Alt + Enter` |

---

# 33. Teknoloji Stack

```text
C#
.NET 8
WPF
MVVM
CommunityToolkit.Mvvm
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Configuration
MetadataExtractor
SixLabors.ImageSharp
```

Opsiyonel:

```text
SkiaSharp
```

---

# 34. Proje Klasör Yapısı

```text
MT.PhotoViewer/
│
├── App.xaml
├── App.xaml.cs
│
├── MainWindow.xaml
├── MainWindow.xaml.cs
│
├── MT.PhotoViewer.csproj
│
├── Assets/
│   ├── Logo/
│   │   ├── MT_PhotoViewer_Logo.png
│   │   ├── MT_PhotoViewer_Logo_Small.png
│   │   ├── MT_PhotoViewer_Logo_Banner.png
│   │   └── MT_PhotoViewer_Logo.ico
│   │
│   └── Icons/
│       ├── open.svg
│       ├── print.svg
│       ├── copy.svg
│       ├── delete.svg
│       ├── rotate-left.svg
│       ├── rotate-right.svg
│       ├── zoom-in.svg
│       ├── zoom-out.svg
│       ├── fit.svg
│       ├── fullscreen.svg
│       └── more.svg
│
├── Models/
│   ├── ImageFile.cs
│   ├── ImageMetadata.cs
│   └── AppSettings.cs
│
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── ImageViewerViewModel.cs
│   ├── InfoPanelViewModel.cs
│   └── SettingsViewModel.cs
│
├── Views/
│   ├── ImageViewerView.xaml
│   ├── InfoPanelView.xaml
│   ├── SettingsWindow.xaml
│   └── AboutWindow.xaml
│
├── Controls/
│   ├── ZoomableImage.xaml
│   ├── FloatingToolbar.xaml
│   └── NavigationButton.xaml
│
├── Services/
│   ├── ImageLoaderService.cs
│   ├── FolderScannerService.cs
│   ├── MetadataService.cs
│   ├── ClipboardService.cs
│   ├── PrintService.cs
│   ├── FileService.cs
│   ├── ThemeService.cs
│   └── SettingsService.cs
│
├── Helpers/
│   ├── NaturalSortComparer.cs
│   ├── RelayCommandHelper.cs
│   └── ImageExtensions.cs
│
└── Themes/
    ├── DarkTheme.xaml
    ├── LightTheme.xaml
    ├── MenuStyles.xaml
    ├── ButtonStyles.xaml
    └── ToolbarStyles.xaml
```

---

# 35. MVVM Ana ViewModel Alanları

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? currentFilePath;

    [ObservableProperty]
    private BitmapSource? currentImage;

    [ObservableProperty]
    private double zoom = 1.0;

    [ObservableProperty]
    private int rotation;

    [ObservableProperty]
    private bool isFullscreen;

    [ObservableProperty]
    private bool isInfoPanelOpen;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private int currentIndex;

    public ObservableCollection<string> FolderImages { get; } = new();
}
```

---

# 36. Temel Commands

```text
OpenFileCommand
OpenFolderCommand
PreviousCommand
NextCommand
ZoomInCommand
ZoomOutCommand
FitToScreenCommand
ActualSizeCommand
RotateLeftCommand
RotateRightCommand
CopyCommand
SaveCopyCommand
DeleteCommand
PrintCommand
ShowPropertiesCommand
OpenFileLocationCommand
OpenWithPaintCommand
ToggleFullscreenCommand
ToggleInfoPanelCommand
ExitCommand
```

---

# 37. Image Loader

Büyük fotoğrafların dosyayı kilitlememesi önemlidir.

Önerilen yaklaşım:

```csharp
using var stream = File.OpenRead(path);

var bitmap = new BitmapImage();
bitmap.BeginInit();
bitmap.CacheOption = BitmapCacheOption.OnLoad;
bitmap.StreamSource = stream;
bitmap.EndInit();
bitmap.Freeze();
```

Böylece resim yüklendikten sonra dosya serbest bırakılır.

---

# 38. Asenkron Yükleme

Fotoğraf yükleme UI thread'i bloke etmemelidir.

```csharp
await Task.Run(() =>
{
    // image decode
});
```

Yükleme sırasında:

```text
ProgressRing
```

veya minimal spinner göster.

---

# 39. Ön Yükleme

Kullanıcı aktif olarak:

```text
Image_005.jpg
```

görüntülüyorsa:

```text
Image_004.jpg
Image_006.jpg
```

arka planda preload edilebilir.

Amaç:

```text
← / →
```

geçişlerini anlık hale getirmek.

---

# 40. Fotoğraf Silme Sonrası

Bir fotoğraf silindiğinde:

- Listeden kaldır.
- Eğer sonraki fotoğraf varsa onu aç.
- Yoksa önceki fotoğrafı aç.
- Klasörde resim kalmadıysa başlangıç ekranına dön.

---

# 41. File Watcher

Opsiyonel ancak önerilir:

```text
FileSystemWatcher
```

Açık klasörde:

- Dosya eklendi
- Dosya silindi
- Dosya yeniden adlandırıldı

durumlarında image listesi otomatik yenilensin.

---

# 42. Pencere Başlığı

Örnek:

```text
Road_Inspection_014.jpg — MT Photo Viewer
```

Zoom veya metadata pencere başlığına yazılmamalıdır.

---

# 43. Windows File Association

Program kurulduğunda kullanıcı isterse:

```text
.jpg
.jpeg
.png
.webp
.bmp
.tiff
```

dosyalarını MT Photo Viewer ile ilişkilendirebilsin.

Komut satırı desteği:

```text
MT.PhotoViewer.exe "D:\Images\road.jpg"
```

`App.xaml.cs` başlangıç argument'ini okuyup resmi açmalıdır.

---

# 44. Tek Instance

İkinci bir fotoğraf açıldığında yeni uygulama penceresi oluşturmak yerine mevcut MT Photo Viewer penceresine göndermek opsiyonel olarak desteklenebilir.

İkinci aşama özelliğidir.

---

# 45. Windows Explorer Entegrasyonu

Kurulum sırasında:

```text
Birlikte Aç → MT Photo Viewer
```

seçeneği desteklenmelidir.

Icon:

```text
MT_PhotoViewer_Logo.ico
```

---

# 46. About Ekranı

```text
MT PHOTO VIEWER

Fast • Minimal • Professional

Developed by
Metin TUNÇER

Version 1.0.0

.NET 8 / WPF
```

Telefon/e-posta zorunlu değildir.

---

# 47. UI Animasyonları

Animasyonlar kısa ve hafif olmalıdır.

Örnek:

```text
Info Panel Slide : 180 ms
Toolbar Fade     : 150 ms
Menu Fade        : 100 ms
Image Transition : 120 ms
```

Ağır animasyonlardan kaçınılmalıdır.

---

# 48. Resim Geçişi

Önceki / sonraki geçişte çok hafif:

```text
Fade Out → Fade In
```

uygulanabilir.

Ama gecikme yaratmamalıdır.

---

# 49. Başlangıç Performans Hedefi

Program:

```text
< 1 saniye
```

içinde görünür olmalıdır.

Fotoğraf açma hedefi:

```text
Normal JPEG/PNG: mümkün olduğunca anlık
```

---

# 50. Kod Kalitesi Kuralları

- UI içinde doğrudan dosya işlemi yapılmamalıdır.
- Dosya işlemleri Services katmanında tutulmalıdır.
- Async işlerde UI thread bloke edilmemelidir.
- Null safety kullanılmalıdır.
- Exception handling merkezi tutulmalıdır.
- Kullanıcıya teknik exception stack trace gösterilmemelidir.
- Log sistemi eklenebilir.

---

# 51. NuGet Önerileri

```text
CommunityToolkit.Mvvm
MetadataExtractor
SixLabors.ImageSharp
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Configuration.Json
```

Opsiyonel:

```text
SkiaSharp
Serilog
```

---

# 52. Uygulama Fazları

## Faz 1 — Temel Viewer

- MainWindow
- Navbar
- Dosya Aç
- Klasör indexleme
- Önceki / sonraki
- Zoom
- Pan
- Fit
- %100
- Rotate
- Fullscreen

## Faz 2 — Windows İşlevleri

- Copy
- Save Copy
- Delete
- Print
- Open With
- File Location
- Properties
- Drag & Drop

## Faz 3 — Metadata

- EXIF
- Info Panel
- GPS
- Kamera bilgileri

## Faz 4 — Tema / UI

- Dark
- Light
- System
- UI animations
- Toolbar auto-hide

## Faz 5 — Installer

- `.exe`
- Setup
- File Associations
- Start Menu Shortcut
- Desktop Shortcut

---

# 53. İlk Sürüm Kabul Kriterleri

Uygulama aşağıdakilerin tamamını yapıyorsa `v1.0` için hazır kabul edilir:

- [ ] JPG açıyor
- [ ] PNG açıyor
- [ ] WEBP açıyor
- [ ] Fotoğraf pencereye sığıyor
- [ ] Mouse wheel zoom çalışıyor
- [ ] Pan çalışıyor
- [ ] Önceki / sonraki çalışıyor
- [ ] Klasörü otomatik indexliyor
- [ ] Thumbnail yok
- [ ] Navbar doğru görünüyor
- [ ] Dosya menüsü çalışıyor
- [ ] Yazdır menüsü çalışıyor
- [ ] Aç menüsü çalışıyor
- [ ] Copy çalışıyor
- [ ] Save Copy çalışıyor
- [ ] Delete çalışıyor
- [ ] Rotate çalışıyor
- [ ] Fullscreen çalışıyor
- [ ] F11 / Esc çalışıyor
- [ ] Dark tema çalışıyor
- [ ] Light tema çalışıyor
- [ ] EXIF paneli çalışıyor
- [ ] Drag & Drop çalışıyor
- [ ] EXE argümanıyla resim açılıyor

---

# 54. Son UI Prensibi

MT Photo Viewer hiçbir zaman galeri uygulaması gibi görünmemelidir.

Ana fikir:

```text
FOTOĞRAF = ANA İÇERİK
KONTROLLER = İKİNCİL
```

Bu nedenle:

- Thumbnail yok.
- Büyük yan menü yok.
- Sürekli açık detay paneli yok.
- Fotoğraf alanını daraltan gereksiz kontroller yok.
- Menü ve toolbar sade tutulur.

---

# 55. Final UI

```text
┌────────────────────────────────────────────────────────────────────┐
│ Road_001.jpg — MT Photo Viewer                          ─  □  ×   │
├────────────────────────────────────────────────────────────────────┤
│ Dosya ▼   Yazdır ▼   E-posta   Yaz ▼   Aç ▼                      │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│                                                                    │
│                            IMAGE                                   │
│                                                                    │
│     ‹                                                        ›     │
│                                                                    │
│                                                                    │
│                                                                    │
│               ╭────────────────────────────────╮                   │
│               │ −  100%  +  ⛶  ↶  ↷  ⋯      │                   │
│               ╰────────────────────────────────╯                   │
└────────────────────────────────────────────────────────────────────┘
```

---

# 56. Geliştirme Notu

Öncelik sırası:

```text
1. HIZ
2. FOTOĞRAF KALİTESİ
3. KULLANIM KOLAYLIĞI
4. UI
5. EK ÖZELLİKLER
```

Program gereksiz özelliklerle şişirilmemelidir.

İlk hedef:

**Windows Fotoğraf Görüntüleyicisi kadar basit, daha modern ve daha güçlü bir MT Photo Viewer oluşturmak.**
