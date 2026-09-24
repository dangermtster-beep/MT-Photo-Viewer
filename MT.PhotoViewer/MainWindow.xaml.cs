using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MT.PhotoViewer.Helpers;
using MT.PhotoViewer.Models;
using MT.PhotoViewer.Services;
using MT.PhotoViewer.ViewModels;

namespace MT.PhotoViewer;

public partial class MainWindow : Window
{
    private const double InfoPanelWidth = 340;   // Guide §24: 320–360 px

    private readonly MainViewModel _vm;
    private readonly SettingsService _settings;
    private readonly ThemeService _theme;
    private readonly DispatcherTimer _hideTimer;

    private WindowState _preFullscreenState = WindowState.Normal;
    private Rect _preFullscreenBounds;
    private ResizeMode _preFullscreenResizeMode = ResizeMode.CanResize;
    private bool _controlsHidden;

    public MainWindow(MainViewModel vm, SettingsService settings, ThemeService theme)
    {
        _vm = vm;
        _settings = settings;
        _theme = theme;

        InitializeComponent();
        DataContext = vm;

        MonitorHelper.HookMaximizeFix(this, () => vm.IsFullscreen);

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        _hideTimer.Tick += (_, _) => HideControls();

        Viewer.ZoomChanged += OnViewerZoomChanged;
        InfoPanel.CloseRequested += (_, _) => _vm.IsInfoPanelOpen = false;

        vm.FitRequested += () => Viewer.FitToScreen();
        vm.ActualSizeRequested += () => Viewer.ActualSize();
        vm.ZoomInRequested += () => Viewer.ZoomIn();
        vm.ZoomOutRequested += () => Viewer.ZoomOut();
        vm.ResetViewRequested += OnResetView;
        vm.ExitRequested += Close;
        vm.PropertyChanged += OnViewModelPropertyChanged;

        _settings.SettingsChanged += OnSettingsChanged;

        Loaded += OnLoaded;
        Closing += OnClosing;
        StateChanged += OnStateChanged;

        Drop += OnDrop;
        DragOver += OnDragOver;
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseDown += OnPreviewMouseDown;
    }

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySettings(_settings.Current);
        RestoreWindowPlacement(_settings.Current);
        UpdateMaximizeGlyph();
        UpdateNavButtons();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPlacement();
        _settings.Save();
        _vm.Shutdown();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        UpdateMaximizeGlyph();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ApplyMaximizedPadding);
    }

    /// <summary>
    /// Ekran kaplandığında Windows, pencere dikdörtgenini yeniden boyutlandırma çerçevesi
    /// kadar ekranın dışına taşırır. Custom title bar kullanıldığı için bu payı
    /// içerik kenar boşluğu olarak telafi ederiz.
    /// </summary>
    private void ApplyMaximizedPadding()
    {
        if (WindowState != WindowState.Maximized || _vm.IsFullscreen)
        {
            RootBorder.Margin = new Thickness(0);
            return;
        }

        Rect work = MonitorHelper.GetWorkAreaBounds(this);
        double x = Math.Max(0, Math.Round((ActualWidth - work.Width) / 2));
        double y = Math.Max(0, Math.Round((ActualHeight - work.Height) / 2));

        RootBorder.Margin = new Thickness(x, y, x, y);
    }

    private void UpdateMaximizeGlyph() =>
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "" : "";

    /// <summary>Guide §43 — komut satırından gelen dosyayı açar.</summary>
    public Task OpenStartupPathAsync(string path) => _vm.OpenPathAsync(path);

    #endregion

    #region Settings & theme

    private void OnSettingsChanged(AppSettings settings)
    {
        _theme.Apply(settings.Theme);
        ApplySettings(settings);
    }

    private void ApplySettings(AppSettings settings)
    {
        Viewer.WheelZoomEnabled = settings.MouseWheelZoom;
        Viewer.SmoothZoom = settings.SmoothZoom;
        Viewer.HighQuality = settings.HighQualityInterpolation;

        PhotoArea.Background = settings.PhotoBackground switch
        {
            "Black" => Brushes.Black,
            "White" => Brushes.White,
            "Checker" => BuildCheckerBrush(),
            _ => (Brush)FindResource("PhotoBackgroundBrush")
        };

        if (!settings.AutoHideControls)
        {
            _hideTimer.Stop();
            ShowControls();
        }
    }

    private static Brush BuildCheckerBrush()
    {
        var geometry = new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x33)),
            null,
            new RectangleGeometry(new Rect(0, 0, 16, 16)));

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0x20, 0x22, 0x26)), null,
            new RectangleGeometry(new Rect(0, 0, 32, 32))));
        drawing.Children.Add(geometry);
        drawing.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x33)), null,
            new RectangleGeometry(new Rect(16, 16, 16, 16))));

        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, 32, 32),
            ViewportUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }

    private void RestoreWindowPlacement(AppSettings settings)
    {
        if (!settings.RememberWindowPosition) return;
        if (double.IsNaN(settings.WindowLeft) || double.IsNaN(settings.WindowTop)) return;

        // Sanal ekranın tamamına bakılır; aksi halde ikincil monitördeki konum
        // "ekran dışında" sayılıp her açılışta atılırdı.
        var desktop = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var placement = new Rect(
            settings.WindowLeft, settings.WindowTop,
            Math.Max(MinWidth, settings.WindowWidth),
            Math.Max(MinHeight, settings.WindowHeight));

        // Başlık çubuğundan tutulabilecek kadar bir bölüm görünür kalmalı.
        Rect visible = Rect.Intersect(desktop, placement);
        if (visible.IsEmpty || visible.Width < 120 || visible.Height < 60) return;

        Left = settings.WindowLeft;
        Top = settings.WindowTop;
        Width = Math.Max(MinWidth, settings.WindowWidth);
        Height = Math.Max(MinHeight, settings.WindowHeight);

        if (settings.WindowMaximized)
            WindowState = WindowState.Maximized;
    }

    private void SaveWindowPlacement()
    {
        AppSettings s = _settings.Current;
        if (!s.RememberWindowPosition) return;

        s.WindowMaximized = WindowState == WindowState.Maximized;

        if (WindowState == WindowState.Normal)
        {
            s.WindowLeft = Left;
            s.WindowTop = Top;
            s.WindowWidth = Width;
            s.WindowHeight = Height;
        }
        else
        {
            s.WindowLeft = RestoreBounds.Left;
            s.WindowTop = RestoreBounds.Top;
            s.WindowWidth = RestoreBounds.Width;
            s.WindowHeight = RestoreBounds.Height;
        }
    }

    #endregion

    #region ViewModel bridge

    private void OnViewerZoomChanged(double zoom) => _vm.ReportZoom(zoom);

    private void OnResetView()
    {
        // Yeni görsel yüklendikten sonra layout'un oturması gerekir.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Viewer.Reset(_settings.Current.StartupZoom == StartupZoomMode.FitToScreen);
            UpdateNavButtons();
            FadeInImage();
        });
    }

    /// <summary>Guide §48 — çok hafif geçiş; gecikme yaratmaz.</summary>
    private void FadeInImage()
    {
        var fade = new DoubleAnimation(0.35, 1.0, TimeSpan.FromMilliseconds(120))
        {
            FillBehavior = FillBehavior.Stop
        };
        fade.Completed += (_, _) => Viewer.Opacity = 1;
        Viewer.BeginAnimation(OpacityProperty, fade);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsFullscreen):
                ApplyFullscreen(_vm.IsFullscreen);
                break;

            case nameof(MainViewModel.IsInfoPanelOpen):
                AnimateInfoPanel(_vm.IsInfoPanelOpen);
                break;

            case nameof(MainViewModel.CurrentIndex):
            case nameof(MainViewModel.HasImage):
                UpdateNavButtons();
                break;
        }
    }

    private void UpdateNavButtons()
    {
        bool show = _vm.HasImage && _vm.FolderImages.Count > 1;
        PrevButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        NextButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    #region Fullscreen & auto-hide (Guide §21)

    private void ApplyFullscreen(bool on)
    {
        if (on)
        {
            _preFullscreenState = WindowState;
            _preFullscreenResizeMode = ResizeMode;
            _preFullscreenBounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;

            TitleRow.Height = new GridLength(0);
            NavbarRow.Height = new GridLength(0);
            RootBorder.Margin = new Thickness(0);
            RootBorder.Background = Brushes.Black;
            PhotoArea.Background = Brushes.Black;

            // Gerçek tam ekran: görev çubuğu dahil monitörün tamamı.
            Rect bounds = MonitorHelper.GetFullScreenBounds(this);
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
            Topmost = true;

            RestartHideTimer();
        }
        else
        {
            _hideTimer.Stop();
            ShowControls();

            TitleRow.Height = new GridLength(34);
            NavbarRow.Height = new GridLength(38);
            RootBorder.SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            ApplySettings(_settings.Current);

            Topmost = false;
            ResizeMode = _preFullscreenResizeMode;

            if (!_preFullscreenBounds.IsEmpty)
            {
                Left = _preFullscreenBounds.Left;
                Top = _preFullscreenBounds.Top;
                Width = _preFullscreenBounds.Width;
                Height = _preFullscreenBounds.Height;
            }

            WindowState = _preFullscreenState;
        }
    }

    private void PhotoArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (_controlsHidden) ShowControls();
        if (_vm.IsFullscreen && _settings.Current.AutoHideControls) RestartHideTimer();
    }

    private void RestartHideTimer()
    {
        if (!_settings.Current.AutoHideControls) return;

        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void HideControls()
    {
        _hideTimer.Stop();
        if (!_vm.IsFullscreen) return;

        _controlsHidden = true;
        Fade(FloatingToolbar, 0, 150);
        Fade(PrevButton, 0, 150);
        Fade(NextButton, 0, 150);
        PhotoArea.Cursor = Cursors.None;
    }

    private void ShowControls()
    {
        _controlsHidden = false;
        Fade(FloatingToolbar, 1, 150);
        Fade(PrevButton, 0.55, 150);
        Fade(NextButton, 0.55, 150);
        PhotoArea.Cursor = null;
    }

    private static void Fade(UIElement element, double to, int ms)
    {
        var anim = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { FillBehavior = FillBehavior.Stop };
        anim.Completed += (_, _) => element.Opacity = to;
        element.BeginAnimation(OpacityProperty, anim);
    }

    #endregion

    #region Info panel (Guide §47 — 180 ms)

    private void AnimateInfoPanel(bool open)
    {
        var anim = new GridLengthAnimation
        {
            From = InfoColumn.Width,
            To = new GridLength(open ? InfoPanelWidth : 0),
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        InfoColumn.BeginAnimation(ColumnDefinition.WidthProperty, anim);
    }

    #endregion

    #region Title bar

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    #endregion

    #region Menu handlers

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = (ContextMenu)FindResource("MoreMenu");
        menu.DataContext = _vm;
        menu.PlacementTarget = MoreButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
        menu.IsOpen = true;
    }

    /// <summary>Guide §10 — takılı USB bellekler menüye canlı olarak doldurulur.</summary>
    private void UsbMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        UsbMenu.Items.Clear();

        IReadOnlyList<DriveInfo> drives = FileService.GetRemovableDrives();

        if (drives.Count == 0)
        {
            UsbMenu.Items.Add(new MenuItem
            {
                Header = "Takılı USB bellek bulunamadı",
                IsEnabled = false,
                Style = (Style)FindResource("MTMenuItem")
            });
            return;
        }

        foreach (DriveInfo drive in drives)
        {
            string label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? "USB Bellek" : drive.VolumeLabel;

            UsbMenu.Items.Add(new MenuItem
            {
                Header = $"{drive.Name.TrimEnd('\\')}  {label}",
                Command = _vm.CopyToFolderCommand,
                CommandParameter = drive.RootDirectory.FullName,
                Style = (Style)FindResource("MTMenuItem")
            });
        }
    }

    private void BurnToDisc_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentFilePath is null) return;

        // CD/DVD yazma opsiyoneldir (Guide §10): dosya Windows'un kendi yazma kuyruğuna bırakılır.
        CommandGuard.Run(() =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_vm.CurrentFilePath}\"",
                UseShellExecute = true
            });
        }, "Explorer açılamadı.");
    }

    private void PrinterSettings_Click(object sender, RoutedEventArgs e) =>
        CommandGuard.Run(() =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "shell32.dll,Control_RunDLL printers",
                UseShellExecute = true
            });
        }, "Yazıcı ayarları açılamadı.");

    #endregion

    #region Input (Guide §27, §32)

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        // Çoklu dosyada listedeki ilk resim açılır (Guide §27).
        string? target = paths.FirstOrDefault(p => Directory.Exists(p) || ImageExtensions.IsKnown(p));
        if (target is null) return;

        Activate();
        await _vm.OpenPathAsync(target);
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Mouse yan tuşlarıyla gezinme.
        if (e.ChangedButton == MouseButton.XButton1 && _vm.PreviousCommand.CanExecute(null))
            _vm.PreviousCommand.Execute(null);
        else if (e.ChangedButton == MouseButton.XButton2 && _vm.NextCommand.CanExecute(null))
            _vm.NextCommand.Execute(null);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        bool alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        switch (key)
        {
            case Key.O when ctrl:
                Execute(_vm.OpenFileCommand); break;

            case Key.C when ctrl:
                Execute(_vm.CopyCommand); break;

            case Key.P when ctrl:
                Execute(_vm.PrintCommand); break;

            case Key.Left:
                Execute(_vm.PreviousCommand); break;

            case Key.Right:
                Execute(_vm.NextCommand); break;

            case Key.Space:
                Execute(_vm.NextCommand); break;

            case Key.Add or Key.OemPlus:
                Execute(_vm.ZoomInCommand); break;

            case Key.Subtract or Key.OemMinus:
                Execute(_vm.ZoomOutCommand); break;

            case Key.D0 or Key.NumPad0 when ctrl:
                Execute(_vm.FitToScreenCommand); break;

            case Key.D1 or Key.NumPad1 when ctrl:
                Execute(_vm.ActualSizeCommand); break;

            case Key.R:
                Execute(shift ? _vm.RotateLeftCommand : _vm.RotateRightCommand); break;

            case Key.F5:
                Execute(_vm.ToggleSlideshowCommand); break;

            case Key.F11:
                Execute(_vm.ToggleFullscreenCommand); break;

            case Key.Escape when _vm.IsSlideshowRunning:
                Execute(_vm.ToggleSlideshowCommand); break;

            case Key.Escape when _vm.IsFullscreen:
                Execute(_vm.ExitFullscreenCommand); break;

            case Key.Delete:
                Execute(_vm.DeleteCommand); break;

            case Key.Enter when alt:
                Execute(_vm.ShowPropertiesCommand); break;

            case Key.I:
                Execute(_vm.ToggleInfoPanelCommand); break;

            case Key.F1:
                Execute(_vm.ShowAboutCommand); break;

            default:
                return;
        }

        e.Handled = true;

        void Execute(ICommand command)
        {
            if (command.CanExecute(null)) command.Execute(null);
        }
    }

    #endregion
}

/// <summary>GridLength animasyonu WPF'te hazır gelmez; bilgi paneli kaydırması için gerekir.</summary>
public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty =
        DependencyProperty.Register(nameof(From), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty =
        DependencyProperty.Register(nameof(To), typeof(GridLength), typeof(GridLengthAnimation));

    public static readonly DependencyProperty EasingFunctionProperty =
        DependencyProperty.Register(nameof(EasingFunction), typeof(IEasingFunction), typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction
    {
        get => (IEasingFunction?)GetValue(EasingFunctionProperty);
        set => SetValue(EasingFunctionProperty, value);
    }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore() => new GridLengthAnimation();

    public override object GetCurrentValue(
        object defaultOriginValue, object defaultDestinationValue, AnimationClock clock)
    {
        double from = From.Value;
        double to = To.Value;
        double progress = clock.CurrentProgress ?? 1;

        if (EasingFunction is not null)
            progress = EasingFunction.Ease(progress);

        return new GridLength(from + (to - from) * progress, GridUnitType.Pixel);
    }
}
