using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace MT.PhotoViewer.Controls;

/// <summary>
/// Zoom / pan / rotate destekli görüntüleyici (Guide §12, §16–§20).
///
/// Dönüşüm sırası: Rotate(merkez) → Scale → Translate.
/// Zoom her zaman mouse imlecinin bulunduğu noktayı sabit tutar.
/// </summary>
public partial class ZoomableImage : UserControl
{
    /// <summary>Guide §16 — desteklenen zoom kademeleri.</summary>
    public static readonly double[] ZoomLevels =
    {
        0.10, 0.25, 0.50, 0.75, 1.00, 1.25, 1.50, 2.00, 3.00, 5.00, 10.00
    };

    private const double MinZoom = 0.02;
    private const double MaxZoom = 10.0;

    private Point _dragStart;
    private Point _dragOrigin;
    private bool _isDragging;
    private bool _suppressZoomCallback;

    public ZoomableImage()
    {
        InitializeComponent();

        SizeChanged += (_, _) =>
        {
            if (IsFitted) FitToScreen();
            else ClampTranslation();
        };
    }

    #region Dependency properties

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(BitmapSource), typeof(ZoomableImage),
            new PropertyMetadata(null, OnSourceChanged));

    public BitmapSource? Source
    {
        get => (BitmapSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty PreviewSourceProperty =
        DependencyProperty.Register(nameof(PreviewSource), typeof(BitmapSource), typeof(ZoomableImage),
            new PropertyMetadata(null, (d, _) => ((ZoomableImage)d).UpdateDisplaySource()));

    /// <summary>
    /// <see cref="Source"/>'un küçültülmüş kopyası. Mevcut zoom'da yeterli piksel
    /// taşıdığı sürece bu çizilir; yakınlaştırınca tam çözünürlüğe geçilir.
    /// Geometri (zoom/pan hesapları) her zaman <see cref="Source"/> boyutuna göredir.
    /// </summary>
    public BitmapSource? PreviewSource
    {
        get => (BitmapSource?)GetValue(PreviewSourceProperty);
        set => SetValue(PreviewSourceProperty, value);
    }

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(ZoomableImage),
            new FrameworkPropertyMetadata(1.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnZoomChanged));

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public static readonly DependencyProperty RotationProperty =
        DependencyProperty.Register(nameof(Rotation), typeof(int), typeof(ZoomableImage),
            new PropertyMetadata(0, OnRotationChanged));

    public int Rotation
    {
        get => (int)GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    public static readonly DependencyProperty IsFittedProperty =
        DependencyProperty.Register(nameof(IsFitted), typeof(bool), typeof(ZoomableImage),
            new PropertyMetadata(true));

    /// <summary>Görsel şu anda "ekrana sığdır" durumunda mı?</summary>
    public bool IsFitted
    {
        get => (bool)GetValue(IsFittedProperty);
        private set => SetValue(IsFittedProperty, value);
    }

    public static readonly DependencyProperty SmoothZoomProperty =
        DependencyProperty.Register(nameof(SmoothZoom), typeof(bool), typeof(ZoomableImage),
            new PropertyMetadata(true));

    public bool SmoothZoom
    {
        get => (bool)GetValue(SmoothZoomProperty);
        set => SetValue(SmoothZoomProperty, value);
    }

    public static readonly DependencyProperty WheelZoomEnabledProperty =
        DependencyProperty.Register(nameof(WheelZoomEnabled), typeof(bool), typeof(ZoomableImage),
            new PropertyMetadata(true));

    public bool WheelZoomEnabled
    {
        get => (bool)GetValue(WheelZoomEnabledProperty);
        set => SetValue(WheelZoomEnabledProperty, value);
    }

    public static readonly DependencyProperty HighQualityProperty =
        DependencyProperty.Register(nameof(HighQuality), typeof(bool), typeof(ZoomableImage),
            new PropertyMetadata(true, OnHighQualityChanged));

    public bool HighQuality
    {
        get => (bool)GetValue(HighQualityProperty);
        set => SetValue(HighQualityProperty, value);
    }

    #endregion

    /// <summary>Kullanıcı wheel/çift tık ile zoom yaptığında tetiklenir.</summary>
    public event Action<double>? ZoomChanged;

    /// <summary>Mouse ile yatay sürükleme yerine tıklama olduğunda (pan yapılmadı).</summary>
    public event Action? Clicked;

    private double ContentWidth => Source?.PixelWidth ?? 0;
    private double ContentHeight => Source?.PixelHeight ?? 0;

    /// <summary>Döndürme sonrası görünen genişlik/yükseklik.</summary>
    private Size RotatedSize
    {
        get
        {
            int angle = ((Rotation % 360) + 360) % 360;
            return angle is 90 or 270
                ? new Size(ContentHeight, ContentWidth)
                : new Size(ContentWidth, ContentHeight);
        }
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ZoomableImage)d;

        if (e.NewValue is BitmapSource bmp)
        {
            // Stretch=Fill + piksel boyutu: görsel, gömülü DPI'dan bağımsız olarak
            // her zaman 1 piksel = 1 birim ölçeğinde çizilir. Önizleme çizilirken de
            // boyut tam çözünürlüğünkidir; Fill onu aynı alana yayar.
            self.Img.Width = bmp.PixelWidth;
            self.Img.Height = bmp.PixelHeight;
            self.Rotate.CenterX = bmp.PixelWidth / 2.0;
            self.Rotate.CenterY = bmp.PixelHeight / 2.0;
        }

        self.UpdateDisplaySource();
    }

    /// <summary>
    /// Verilen zoom'da önizlemenin pikselleri ekrana yetiyorsa önizlemeyi, yetmiyorsa
    /// tam çözünürlüğü çizer. Böylece sığdırılmış görünümde GPU'ya ekran boyutunda
    /// küçük bir doku gider; geçişler anlık olur.
    /// </summary>
    private void UpdateDisplaySource(double? zoom = null)
    {
        BitmapSource? full = Source;
        BitmapSource? display = full;

        if (full is not null && PreviewSource is { } preview && !ReferenceEquals(preview, full))
        {
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double neededWidth = (zoom ?? Scale.ScaleX) * pixelsPerDip * full.PixelWidth;

            if (neededWidth <= preview.PixelWidth + 0.5)
                display = preview;
        }

        if (!ReferenceEquals(Img.Source, display))
            Img.Source = display;
    }

    private static void OnZoomChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ZoomableImage)d;
        if (self._suppressZoomCallback) return;

        // Dışarıdan (ör. toolbar) atanan zoom: merkeze göre uygula.
        self.SetZoom((double)e.NewValue, self.ViewCenter, animate: false, fitted: false);
    }

    private static void OnRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ZoomableImage)d;
        self.Rotate.Angle = ((int)e.NewValue % 360 + 360) % 360;

        if (self.IsFitted) self.FitToScreen();
        else self.CenterOrClamp();
    }

    private static void OnHighQualityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ZoomableImage)d;
        RenderOptions.SetBitmapScalingMode(self.Img,
            (bool)e.NewValue ? BitmapScalingMode.HighQuality : BitmapScalingMode.LowQuality);
    }

    private Point ViewCenter => new(ActualWidth / 2, ActualHeight / 2);

    #region Public API

    /// <summary>Guide §12 — oranı koruyarak kullanılabilir alana sığdırır. %100'ün üzerine çıkmaz.</summary>
    public void FitToScreen()
    {
        if (ContentWidth <= 0 || ActualWidth <= 0) return;

        Size size = RotatedSize;
        double scale = Math.Min(ActualWidth / size.Width, ActualHeight / size.Height);
        scale = Math.Min(scale, 1.0);   // küçük görseller bulanıklaşmasın

        (double tx, double ty) = CenteredTranslation(scale);

        IsFitted = true;
        Apply(scale, tx, ty, animate: false);
    }

    /// <summary>Guide §17 — Ctrl+1: gerçek boyut.</summary>
    public void ActualSize()
    {
        if (ContentWidth <= 0) return;

        SetZoom(1.0, ViewCenter, animate: SmoothZoom, fitted: false);
    }

    /// <summary>Guide §19 — çift tıklama: Ekrana Sığdır ⇄ %100.</summary>
    public void ToggleFit()
    {
        if (IsFitted) ActualSize();
        else FitToScreen();
    }

    public void ZoomIn() => StepZoom(+1, ViewCenter);

    public void ZoomOut() => StepZoom(-1, ViewCenter);

    /// <summary>
    /// Yeni bir görsel açıldığında başlangıç durumuna alır (Guide §31 — başlangıç zoom davranışı).
    /// <paramref name="fit"/> false olsa bile pencereye sığmayan görseller sığdırılır.
    /// </summary>
    public void Reset(bool fit)
    {
        StopAnimations();
        Rotate.Angle = 0;

        Size size = RotatedSize;

        if (fit || size.Width > ActualWidth || size.Height > ActualHeight)
        {
            FitToScreen();
            return;
        }

        IsFitted = false;
        (double tx, double ty) = CenteredTranslation(1.0);
        Apply(1.0, tx, ty, animate: false);
    }

    #endregion

    #region Zoom mechanics

    private void StepZoom(int direction, Point anchor)
    {
        double current = Zoom;
        double target;

        if (direction > 0)
        {
            target = ZoomLevels.FirstOrDefault(z => z > current + 0.0001);
            if (target == 0) target = Math.Min(MaxZoom, current * 1.25);
        }
        else
        {
            target = ZoomLevels.LastOrDefault(z => z < current - 0.0001);
            if (target == 0) target = Math.Max(MinZoom, current / 1.25);
        }

        SetZoom(target, anchor, animate: SmoothZoom, fitted: false);
    }

    /// <summary>
    /// Guide §16 — zoom merkezi imlecin noktasıdır; bakılan bölge ekrandan kaçmaz.
    /// </summary>
    private void SetZoom(double newZoom, Point anchor, bool animate, bool fitted)
    {
        if (ContentWidth <= 0) return;

        newZoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        double oldZoom = Scale.ScaleX;
        if (Math.Abs(newZoom - oldZoom) < 0.0001) return;

        double factor = newZoom / oldZoom;

        // Anchor noktası sabit kalacak şekilde yeni öteleme.
        double tx = anchor.X - factor * (anchor.X - Translate.X);
        double ty = anchor.Y - factor * (anchor.Y - Translate.Y);
        (tx, ty) = ClampTo(newZoom, tx, ty);

        IsFitted = fitted;
        Apply(newZoom, tx, ty, animate);
    }

    private void Apply(double zoom, double tx, double ty, bool animate)
    {
        StopAnimations();

        if (animate)
        {
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(110);

            // Animasyon boyunca her iki uçtaki zoom'a da yetecek kaynak çizilir;
            // uzaklaşma bitince önizlemeye geri dönülür.
            UpdateDisplaySource(Math.Max(Scale.ScaleX, zoom));

            Animate(Scale, ScaleTransform.ScaleXProperty, zoom, duration, ease, () => UpdateDisplaySource());
            Animate(Scale, ScaleTransform.ScaleYProperty, zoom, duration, ease);
            Animate(Translate, TranslateTransform.XProperty, tx, duration, ease);
            Animate(Translate, TranslateTransform.YProperty, ty, duration, ease);
        }
        else
        {
            Scale.ScaleX = zoom;
            Scale.ScaleY = zoom;
            Translate.X = tx;
            Translate.Y = ty;

            UpdateDisplaySource();
        }

        _suppressZoomCallback = true;
        Zoom = zoom;
        _suppressZoomCallback = false;

        // Animasyon sürerken Scale.ScaleX hâlâ başlangıç değerindedir; hedefi bildir.
        ZoomChanged?.Invoke(zoom);
    }

    /// <summary>
    /// FillBehavior.Stop + Completed ile animasyon biter bitmez değeri kalıcı yazar;
    /// böylece sürükleme sırasında transform'a doğrudan yazmak mümkün kalır.
    /// </summary>
    private static void Animate(
        Transform transform, DependencyProperty property, double to, TimeSpan duration, IEasingFunction ease,
        Action? completed = null)
    {
        var anim = new DoubleAnimation(to, duration)
        {
            EasingFunction = ease,
            FillBehavior = FillBehavior.Stop
        };

        anim.Completed += (_, _) =>
        {
            transform.BeginAnimation(property, null);
            transform.SetValue(property, to);
            completed?.Invoke();
        };

        transform.BeginAnimation(property, anim);
    }

    private void StopAnimations()
    {
        double zx = Scale.ScaleX, tx = Translate.X, ty = Translate.Y;

        Scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        Scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        Translate.BeginAnimation(TranslateTransform.XProperty, null);
        Translate.BeginAnimation(TranslateTransform.YProperty, null);

        Scale.ScaleX = zx;
        Scale.ScaleY = zx;
        Translate.X = tx;
        Translate.Y = ty;
    }

    /// <summary>
    /// İçerik görünümden küçükse ortalar, büyükse kenarların içeri kaçmasını engeller.
    /// Döndürme merkez etrafında yapıldığı için oluşan sabit kayma da hesaba katılır.
    /// </summary>
    private (double X, double Y) ClampTo(double zoom, double tx, double ty)
    {
        Size size = RotatedSize;
        double w = size.Width * zoom;
        double h = size.Height * zoom;

        double offsetX = (ContentWidth - size.Width) / 2 * zoom;
        double offsetY = (ContentHeight - size.Height) / 2 * zoom;

        tx = w <= ActualWidth
            ? (ActualWidth - w) / 2 - offsetX
            : Math.Clamp(tx, ActualWidth - w - offsetX, -offsetX);

        ty = h <= ActualHeight
            ? (ActualHeight - h) / 2 - offsetY
            : Math.Clamp(ty, ActualHeight - h - offsetY, -offsetY);

        return (tx, ty);
    }

    private void CenterOrClamp()
    {
        if (ContentWidth <= 0) return;

        (Translate.X, Translate.Y) = ClampTo(Scale.ScaleX, Translate.X, Translate.Y);
    }

    private void ClampTranslation() => CenterOrClamp();

    private (double X, double Y) CenteredTranslation(double zoom)
    {
        Size size = RotatedSize;

        double offsetX = (ContentWidth - size.Width) / 2 * zoom;
        double offsetY = (ContentHeight - size.Height) / 2 * zoom;

        return ((ActualWidth - size.Width * zoom) / 2 - offsetX,
                (ActualHeight - size.Height * zoom) / 2 - offsetY);
    }

    #endregion

    #region Input

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        if (!WheelZoomEnabled || Source is null) return;

        StepZoom(e.Delta > 0 ? +1 : -1, e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (Source is null) return;

        if (e.ClickCount == 2)
        {
            ToggleFit();
            e.Handled = true;
            return;
        }

        StopAnimations();
        _dragStart = e.GetPosition(this);
        _dragOrigin = new Point(Translate.X, Translate.Y);
        _isDragging = true;
        CaptureMouse();
        UpdateCursor(dragging: true);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging)
        {
            UpdateCursor(dragging: false);
            return;
        }

        Point p = e.GetPosition(this);
        Translate.X = _dragOrigin.X + (p.X - _dragStart.X);
        Translate.Y = _dragOrigin.Y + (p.Y - _dragStart.Y);
        CenterOrClamp();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (!_isDragging) return;

        _isDragging = false;
        ReleaseMouseCapture();
        UpdateCursor(dragging: false);

        Point p = e.GetPosition(this);
        if (Math.Abs(p.X - _dragStart.X) < 3 && Math.Abs(p.Y - _dragStart.Y) < 3)
            Clicked?.Invoke();
    }

    /// <summary>Guide §18 — içerik ekrandan büyükse el imleci gösterilir.</summary>
    private void UpdateCursor(bool dragging)
    {
        Size size = RotatedSize;
        double z = Scale.ScaleX;
        bool pannable = size.Width * z > ActualWidth + 1 || size.Height * z > ActualHeight + 1;

        Cursor = pannable
            ? (dragging ? Cursors.ScrollAll : Cursors.Hand)
            : Cursors.Arrow;
    }

    #endregion
}
