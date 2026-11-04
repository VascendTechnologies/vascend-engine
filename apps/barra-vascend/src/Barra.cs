using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BarraVascend;

class Schermo
{
    public IntPtr H;
    public N.RECT Area;
    public string Nome;
    public bool Principale;
}

// Lineetta a sinistra delle app aperte: spessa un pixel della grafica, lime per quella in primo piano.
// Quando cambia stato si allunga o si accorcia con un'animazione.
class Indicatore : FrameworkElement
{
    public static readonly DependencyProperty AmpiezzaProperty = DependencyProperty.Register(nameof(Ampiezza), typeof(double), typeof(Indicatore),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public double Ampiezza { get => (double)GetValue(AmpiezzaProperty); set => SetValue(AmpiezzaProperty, value); }

    public int Stato
    {
        get => stato;
        set
        {
            if (stato == value) return;
            stato = value;
            double fine = stato == 0 ? 0 : stato == 1 ? 4 : 10;
            BeginAnimation(AmpiezzaProperty, new System.Windows.Media.Animation.DoubleAnimation(fine, TimeSpan.FromMilliseconds(260))
            { EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = .5, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
        }
    }
    int stato;
    public Indicatore()
    {
        IsHitTestVisible = false;
        // la lineetta sta dalla parte del bordo dello schermo
        switch (Barra.Lato)
        {
            case "destra": Width = 6; HorizontalAlignment = HorizontalAlignment.Right; break;
            case "alto": Height = 6; VerticalAlignment = VerticalAlignment.Top; break;
            case "basso": Height = 6; VerticalAlignment = VerticalAlignment.Bottom; break;
            default: Width = 6; HorizontalAlignment = HorizontalAlignment.Left; break;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
        int celle = (int)Math.Round(Ampiezza);
        if (celle <= 0) return;
        double l = celle * u;
        Brush b = stato == 2 ? Tema.LimePennello : stato == 3 ? Tema.Pennello(255, 170, 90, 1) : Tema.BiancoTenue;
        if (Barra.Orizzontale)
            dc.DrawRectangle(b, null, new Rect(Math.Round((ActualWidth - l) / 2 / u) * u, Barra.Lato == "basso" ? ActualHeight - u : 0, l, u));
        else
            dc.DrawRectangle(b, null, new Rect(Barra.Lato == "destra" ? ActualWidth - u : 0, Math.Round((ActualHeight - l) / 2 / u) * u, u, l));
    }
}

// Tasto di un'app: si comporta come il logo (evidenziazione che sfuma e pressione al clic), niente salti né rimbalzi.
class TastoApp : Tasto
{
    public Gruppo Gruppo;
    public readonly Image Img = new() { Width = 24, Height = 24 };
    public readonly Indicatore Ind = new();

    public TastoApp() : base(new Grid(), 46, 42)
    {
        var g = (Grid)Child;
        RenderOptions.SetBitmapScalingMode(Img, BitmapScalingMode.HighQuality);
        Img.Margin = Barra.Lato switch { "destra" => new Thickness(0, 0, 3, 0), "alto" => new Thickness(0, 3, 0, 0), "basso" => new Thickness(0, 0, 0, 3), _ => new Thickness(3, 0, 0, 0) };
        Margin = Barra.Orizzontale ? new Thickness(1, 0, 1, 0) : new Thickness(0, 1, 0, 1);
        if (Barra.Orizzontale) { Width = 42; Height = 46; }
        g.Children.Add(Img);
        g.Children.Add(Ind);
    }

    // Comparsa e uscita: solo una dissolvenza breve.
    public void Entra() => BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));

    public void Esci(Action fatto)
    {
        IsHitTestVisible = false;
        var a = new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
        a.Completed += (_, _) => fatto();
        BeginAnimation(OpacityProperty, a);
    }

    public void Imposta(Gruppo gr)
    {
        Gruppo = gr;
        Img.Source = gr.Icona;
        Ind.Stato = gr.Finestre.Count == 0 ? 0 : gr.Attenzione ? 3 : gr.Attiva ? 2 : 1;
        // con finestre aperte il titolo lo dà già l'anteprima
        ToolTip = gr.Finestre.Count == 0 ? Barra.Suggerimento(gr.Nome, true) : null;
    }
}

// La barra di uno schermo: in verticale sul bordo sinistro, sempre visibile, vetro ghiacciato e angoli a pixel.
// Si registra come barra di sistema, così le finestre massimizzate si fermano al suo bordo.
class Barra : Window, IFinestraPixel, IOspite
{
    public Schermo Schermo { get; }
    // il lato dello schermo scelto in Vascend Engine
    public static string Lato => Stile.Attuale.PosizioneBarra is "destra" or "alto" or "basso" ? Stile.Attuale.PosizioneBarra : "basso";
    public static bool Orizzontale => Lato is "alto" or "basso";
    public bool PannelliSotto => Lato == "alto";
    public bool PannelliDestra => Lato == "sinistra";
    public bool PannelliSinistra => Lato == "destra";
    public bool Attaccato => false;
    public void Coperto(bool si) { }

    public N.RECT RettangoloDi(FrameworkElement el)
    {
        try
        {
            var a = el.TransformToAncestor(this).Transform(new Point(0, 0));
            int x = xPx + (int)Math.Round(a.X * scala), y = yPx + (int)Math.Round(a.Y * scala);
            return new N.RECT { L = x, T = y, R = x + (int)Math.Round(el.ActualWidth * scala), B = y + (int)Math.Round(el.ActualHeight * scala) };
        }
        catch { return RettangoloFinale; }
    }
    public IntPtr Hwnd;
    double scala = 1;
    int unitaPx = 2, wPx, hPx, xPx, yPx, riservaPx = -1;
    double visib, obiettivo = 1;
    bool mostrata, registrata;
    const int Larghezza = 54, Margine = 6;
    const uint MessaggioBarra = N.WM_USER + 77;
    readonly Cornice cornice;
    readonly StackPanel app = new() { Orientation = Verso, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = Orizzontale ? VerticalAlignment.Center : VerticalAlignment.Top };
    static Orientation Verso => Orizzontale ? Orientation.Horizontal : Orientation.Vertical;
    readonly Dictionary<string, TastoApp> tasti = new();
    TextBlock ora, data, glifoRete, glifoVolume, glifoBatteria;
    Tasto tastoBatteria, tastoTray;
    TastoApp inAttesa;
    DispatcherTimer attesa;
    DateTime ultimoAggiorno;

    public double UnitaDip => unitaPx / scala;
    public N.RECT RettangoloFinale => new() { L = xPx, T = yPx, R = xPx + wPx, B = yPx + hPx };

    public Barra(Schermo s)
    {
        Schermo = s;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        Title = "BarraVascend";
        if (Orizzontale) { Height = Larghezza; Width = 800; } else { Width = Larghezza; Height = 800; }
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        // in verticale tre righe (inizio, app, fine), in orizzontale tre colonne
        var griglia = new Grid();
        for (int i = 0; i < 3; i++)
            if (Orizzontale) griglia.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
            else griglia.RowDefinitions.Add(new RowDefinition { Height = i == 1 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        void Metti(UIElement e, int i) { if (Orizzontale) Grid.SetColumn(e, i); else Grid.SetRow(e, i); }

        var alto = new StackPanel { Orientation = Verso, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = Orizzontale ? new Thickness(7, 0, 0, 0) : new Thickness(0, 7, 0, 0) };
        alto.Children.Add(TastoStart());
        alto.Children.Add(TastoCerca());
        alto.Children.Add(Separatore());
        griglia.Children.Add(alto);

        // le app scorrono con la rotella se non ci stanno tutte
        var scorri = Orizzontale
            ? new ScrollViewer { Content = app, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, HorizontalAlignment = HorizontalAlignment.Left }
            : new ScrollViewer { Content = app, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false };
        if (Orizzontale) scorri.PreviewMouseWheel += (_, e) => { scorri.ScrollToHorizontalOffset(scorri.HorizontalOffset - e.Delta / 2.0); e.Handled = true; };
        Metti(scorri, 1);
        griglia.Children.Add(scorri);

        var basso = new StackPanel { Orientation = Verso, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = Orizzontale ? new Thickness(0, 0, 7, 0) : new Thickness(0, 0, 0, 7) };
        basso.Children.Add(Separatore());
        tastoTray = Verticale(new Tasto(Tema.Glifo("", 12), 44, 30) { Sinistro = () => Apri(tastoTray, Contenuti.Tray()) });
        tastoTray.ToolTip = Suggerimento("Icone nascoste", true);
        basso.Children.Add(tastoTray);

        glifoRete = Tema.Glifo(Rete.Glifo(), 15);
        glifoVolume = Tema.Glifo(Audio.Glifo(), 15);
        Tasto tRete = null, tVol = null;
        tRete = Verticale(new Tasto(glifoRete, 44, 34) { Sinistro = () => Apri(tRete, Contenuti.Rete()) });
        tVol = Verticale(new Tasto(glifoVolume, 44, 34) { Sinistro = () => Apri(tVol, Contenuti.Volume()), Rotella = d => { Audio.Livello += d > 0 ? .02f : -.02f; AggiornaStato(); } });
        tVol.Centrale = () => { Audio.Muto = !Audio.Muto; AggiornaStato(); };
        basso.Children.Add(tRete);
        basso.Children.Add(tVol);
        if (Batteria.Presente)
        {
            glifoBatteria = Tema.Glifo(Batteria.Glifo(), 16);
            tastoBatteria = Verticale(new Tasto(glifoBatteria, 44, 34));
            tastoBatteria.Sinistro = () => Apri(tastoBatteria, Contenuti.Batteria());
            basso.Children.Add(tastoBatteria);
        }

        var orologio = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        ora = Tema.Scritta("", 12.5, null, FontWeights.SemiBold);
        ora.HorizontalAlignment = HorizontalAlignment.Center;
        System.Windows.Documents.Typography.SetNumeralAlignment(ora, FontNumeralAlignment.Tabular);
        data = Tema.Scritta("", 10, Tema.BiancoTenue);
        data.HorizontalAlignment = HorizontalAlignment.Center;
        orologio.Children.Add(ora);
        orologio.Children.Add(data);
        Tasto tOra = null;
        tOra = Verticale(new Tasto(orologio, 46, 40) { Sinistro = () => Apri(tOra, Contenuti.Calendario()) });
        basso.Children.Add(tOra);
        var campana = new Grid { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        campana.Children.Add(Tema.Glifo("\uEA8F", 15));
        contatore = Tema.Scritta("", 9, Tema.Pennello(20, 24, 28, 1), FontWeights.Bold);
        contatore.Margin = new Thickness(3, 0, 3, 0);
        bollino = new Border { Background = Tema.LimePennello, Child = contatore, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -5, -9, 0), Visibility = Visibility.Collapsed };
        campana.Children.Add(bollino);
        Tasto notifiche = null;
        notifiche = Verticale(new Tasto(campana, 44, 34) { Sinistro = () => Apri(notifiche, global::BarraVascend.Notifiche.Pannello()) });
        tastoNotifiche = notifiche;
        notifiche.ToolTip = Suggerimento("Notifiche", true);
        basso.Children.Add(notifiche);
        Metti(basso, 2);
        griglia.Children.Add(basso);

        cornice = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.Ghiaccio, Bordo = Tema.Bordo, Child = griglia };
        cornice.MouseRightButtonUp += (_, e) => { if (e.OriginalSource == cornice || e.OriginalSource is Grid or StackPanel) Apri(cornice, Contenuti.MenuBarra(), CentroDa(e)); };
        Content = cornice;

        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(Hwnd);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            src.AddHook(Gancio);
            N.StileStrumento(Hwnd, true);
            N.Vetro(Hwnd);
            N.SetWindowRgn(Hwnd, N.CreateRectRgn(0, 0, 0, 0), false);
        };
        Closed += (_, _) => Libera();
        App.Io.Finestre.Cambiati += AggiornaApp;
        Rete.Cambiata += AggiornaStato;
        global::BarraVascend.Notifiche.Cambiate += AggiornaContatore;
        AggiornaApp();
        AggiornaOra();
    }

    static Tasto Verticale(Tasto t)
    {
        t.Margin = Orizzontale ? new Thickness(1, 0, 1, 0) : new Thickness(0, 1, 0, 1);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        t.VerticalAlignment = VerticalAlignment.Center;
        return t;
    }

    // Lineetta fra i gruppi, spessa un pixel della grafica, di traverso rispetto alla barra.
    FrameworkElement Separatore() => Orizzontale
        ? new Border { Width = 2, Height = 22, Background = Tema.Pennello(255, 255, 255, .14), Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center }
        : new Border { Height = 2, Width = 22, Background = Tema.Pennello(255, 255, 255, .14), Margin = new Thickness(0, 6, 0, 6), HorizontalAlignment = HorizontalAlignment.Center };

    public static ToolTip Suggerimento(string testo, bool aDestra = false)
    {
        var t = new ToolTip
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HasDropShadow = false,
            Padding = new Thickness(0),
            Content = new Cornice
            {
                Profilo = Gradini.Piccolo,
                Fondo = Tema.Pennello(28, 40, 52, .94),
                Bordo = Tema.Pennello(225, 242, 255, .30),
                Child = new TextBlock { Text = testo, Foreground = Tema.Bianco, FontFamily = Tema.Testo, FontSize = 12, Margin = new Thickness(10, 5, 10, 6), MaxWidth = 360, TextTrimming = TextTrimming.CharacterEllipsis },
            },
        };
        t.Template = new ControlTemplate(typeof(ToolTip)) { VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)) };
        // dalla barra il suggerimento esce verso il centro dello schermo
        if (aDestra)
            switch (Lato)
            {
                case "destra": t.Placement = System.Windows.Controls.Primitives.PlacementMode.Left; t.HorizontalOffset = -12; break;
                case "alto": t.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; t.VerticalOffset = 12; break;
                case "basso": t.Placement = System.Windows.Controls.Primitives.PlacementMode.Top; t.VerticalOffset = -12; break;
                default: t.Placement = System.Windows.Controls.Primitives.PlacementMode.Right; t.HorizontalOffset = 12; break;
            }
        return t;
    }

    Tasto tastoStart, tastoNotifiche;
    string testoBatteria;
    TextBlock contatore;
    Border bollino;
    public void ApriNotifiche() => Apri(tastoNotifiche, global::BarraVascend.Notifiche.Pannello());

    void AggiornaContatore()
    {
        int n = global::BarraVascend.Notifiche.Elenco.Count;
        bool prima = bollino.Visibility == Visibility.Visible;
        contatore.Text = n > 9 ? "9+" : n.ToString();
        bollino.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (n > 0 && !prima)
        {
            // il bollino nasce con un piccolo rimbalzo
            var sc = new ScaleTransform(0, 0);
            bollino.RenderTransform = sc;
            bollino.RenderTransformOrigin = new Point(.5, .5);
            var a = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320)) { EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = .6, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, a);
        }
    }
    public void ApriStart() => Apri(tastoStart, global::BarraVascend.Start.Crea());

    Tasto TastoStart()
    {
        // la sagoma della volpe colorata col ghiaccio della barra: bianco in alto, azzurro tenue in basso
        var sagoma = new ImageBrush(new BitmapImage(new Uri(System.IO.Path.Combine(App.Cartella, "volpe.png")))) { Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(sagoma, BitmapScalingMode.HighQuality);
        var img = new System.Windows.Shapes.Rectangle
        {
            Width = 22,
            Height = 23,
            OpacityMask = sagoma,
            Fill = new LinearGradientBrush(Stile.Scala(Stile.Colore(Stile.Attuale.Ghiaccio), 1.85), Stile.Colore(Stile.Attuale.Ghiaccio), 90),
        };
        Tasto t = null;
        t = Verticale(new Tasto(img, 44, 40) { Sinistro = ApriStart, Destro = () => Apri(t, Contenuti.MenuStart()) });
        tastoStart = t;
        t.ToolTip = Suggerimento("Start", true);
        return t;
    }

    Tasto TastoCerca()
    {
        var t = Verticale(new Tasto(Tema.Glifo("", 15), 44, 38) { Sinistro = () => Cerca.Apri(Schermo) });
        t.ToolTip = Suggerimento("Cerca  (Alt+Spazio)", true);
        return t;
    }

    IntPtr Gancio(IntPtr h, int m, IntPtr w, IntPtr l, ref bool gestito)
    {
        if (m == N.WM_MOUSEACTIVATE) { gestito = true; return (IntPtr)3; }
        if (m == N.WM_DPICHANGED) Dispatcher.BeginInvoke(Posiziona);
        // la shell avvisa quando le altre barre cambiano: si rimette al suo posto
        if (m == MessaggioBarra && (int)w == 1) Dispatcher.BeginInvoke(() => { riservaPx = -1; Riserva(); });
        return IntPtr.Zero;
    }

    public void Avvia()
    {
        Show();
        N.ShowWindow(Hwnd, N.SW_HIDE);
        Posiziona();
    }

    // Dimensioni in pixel veri dello schermo: 54 DIP di larghezza, 6 DIP dai bordi, alta tutto lo schermo.
    public void Posiziona()
    {
        if (Hwnd == IntPtr.Zero) return;
        N.GetDpiForMonitor(Schermo.H, 0, out uint dx, out _);
        double s = dx / 96.0;
        bool cambiata = Math.Abs(s - scala) > .001;
        scala = s;
        unitaPx = Math.Max(2, (int)Math.Round(2 * scala, MidpointRounding.AwayFromZero));
        var a = Schermo.Area;
        int margine = (int)Math.Round(Margine * scala), spessore = (int)Math.Round(Larghezza * scala);
        if (Orizzontale) { wPx = a.W - 2 * margine; hPx = spessore; xPx = a.L + margine; yPx = Lato == "alto" ? a.T + margine : a.B - margine - hPx; }
        else { wPx = spessore; hPx = a.H - 2 * margine; yPx = a.T + margine; xPx = Lato == "sinistra" ? a.L + margine : a.R - margine - wPx; }
        Muovi();
        if (mostrata) Riserva();
        if (cambiata) Pannello.Ridisegna(this);
    }

    // Toglie alle finestre la striscia a sinistra occupata dalla barra (come fa la barra di Windows).
    int Spessore => (Orizzontale ? hPx : wPx) + 2 * (int)Math.Round(Margine * scala);

    void Riserva()
    {
        int voluta = Spessore;
        if (voluta == riservaPx) return;
        uint lato = Lato switch { "destra" => N.ABE_RIGHT, "alto" => N.ABE_TOP, "basso" => N.ABE_BOTTOM, _ => N.ABE_LEFT };
        var d = new N.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.APPBARDATA>(), hWnd = Hwnd, uCallbackMessage = MessaggioBarra, uEdge = lato };
        if (!registrata) { N.SHAppBarMessage(N.ABM_NEW, ref d); registrata = true; Ricorda(Hwnd); }
        var a = Schermo.Area;
        d.rc = Lato switch
        {
            "destra" => new N.RECT { L = a.R - voluta, T = a.T, R = a.R, B = a.B },
            "alto" => new N.RECT { L = a.L, T = a.T, R = a.R, B = a.T + voluta },
            "basso" => new N.RECT { L = a.L, T = a.B - voluta, R = a.R, B = a.B },
            _ => new N.RECT { L = a.L, T = a.T, R = a.L + voluta, B = a.B },
        };
        N.SHAppBarMessage(N.ABM_QUERYPOS, ref d);
        switch (Lato)
        {
            case "destra": d.rc.L = d.rc.R - voluta; break;
            case "alto": d.rc.B = d.rc.T + voluta; break;
            case "basso": d.rc.T = d.rc.B - voluta; break;
            default: d.rc.R = d.rc.L + voluta; break;
        }
        N.SHAppBarMessage(N.ABM_SETPOS, ref d);
        riservaPx = voluta;
        Applica();
    }

    // L'area di lavoro con la striscia della barra tolta dal suo lato; null se è già così.
    N.RECT? AreaVoluta(N.RECT lavoro)
    {
        var a = Schermo.Area;
        var r = lavoro;
        switch (Lato)
        {
            case "destra": if (lavoro.R <= a.R - riservaPx) return null; r.R = a.R - riservaPx; break;
            case "alto": if (lavoro.T >= a.T + riservaPx) return null; r.T = a.T + riservaPx; break;
            case "basso": if (lavoro.B <= a.B - riservaPx) return null; r.B = a.B - riservaPx; break;
            default: if (lavoro.L >= a.L + riservaPx) return null; r.L = a.L + riservaPx; break;
        }
        return r;
    }

    // Explorer ricalcola l'area di lavoro con molto ritardo: intanto la si stringe subito a mano
    // (SPI_SETWORKAREA con avviso, così le finestre massimizzate si spostano).
    void Applica()
    {
        var mi = new N.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.MONITORINFOEX>() };
        if (riservaPx <= 0 || !N.GetMonitorInfo(Schermo.H, ref mi)) return;
        if (AreaVoluta(mi.rcWork) is not N.RECT r) return;
        N.SystemParametersInfo(0x2F, 0, ref r, 0);
        // l'avviso alle finestre salta quelle bloccate: con SPIF_SENDCHANGE una sola app appesa ferma la barra
        System.Threading.Tasks.Task.Run(() => N.SendMessageTimeout((IntPtr)0xFFFF, 0x1A, (IntPtr)0x2F, IntPtr.Zero, 2, 1000, out _));
    }

    // Le barre chiuse a forza restano registrate nella shell, che allora tarda a ricalcolare lo spazio:
    // si annotano gli handle e al giro dopo si tolgono quelli rimasti.
    static readonly string FileBarre = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BarraVascend", "barre.txt");

    static void Ricorda(IntPtr h)
    {
        try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FileBarre)); System.IO.File.AppendAllText(FileBarre, (long)h + "\n"); } catch { }
    }

    public static void TogliVecchie()
    {
        try
        {
            if (!System.IO.File.Exists(FileBarre)) return;
            foreach (var r in System.IO.File.ReadAllLines(FileBarre))
            {
                if (!long.TryParse(r, out var v) || N.IsWindow((IntPtr)v)) continue;
                var d = new N.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.APPBARDATA>(), hWnd = (IntPtr)v };
                N.SHAppBarMessage(N.ABM_REMOVE, ref d);
            }
            System.IO.File.Delete(FileBarre);
        }
        catch { }
    }

    void Libera()
    {
        if (!registrata) return;
        registrata = false;
        var d = new N.APPBARDATA { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.APPBARDATA>(), hWnd = Hwnd };
        N.SHAppBarMessage(N.ABM_REMOVE, ref d);
        // restituisce subito la striscia alle finestre
        var mi = new N.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.MONITORINFOEX>() };
        if (riservaPx > 0 && N.GetMonitorInfo(Schermo.H, ref mi))
        {
            var r = mi.rcWork;
            var a = Schermo.Area;
            bool nostra = Lato switch { "destra" => r.R == a.R - riservaPx, "alto" => r.T == a.T + riservaPx, "basso" => r.B == a.B - riservaPx, _ => r.L == a.L + riservaPx };
            if (nostra)
            {
                switch (Lato) { case "destra": r.R = a.R; break; case "alto": r.T = a.T; break; case "basso": r.B = a.B; break; default: r.L = a.L; break; }
                N.SystemParametersInfo(0x2F, 0, ref r, 0);
                N.SendMessageTimeout((IntPtr)0xFFFF, 0x1A, (IntPtr)0x2F, IntPtr.Zero, 2, 1000, out _);
            }
        }
        riservaPx = -1;
    }

    double Spostamento => (1 - Facilita(visib)) * (Spessore + 2);
    static double Facilita(double t) => 1 - Math.Pow(1 - t, 3);

    void Muovi()
    {
        // a schermo intero la barra scivola fuori dal suo bordo
        int d = (int)Math.Round(Spostamento);
        int x = xPx + Lato switch { "sinistra" => -d, "destra" => d, _ => 0 }, y = yPx + Lato switch { "alto" => -d, "basso" => d, _ => 0 };
        N.SetWindowPos(Hwnd, N.HWND_TOPMOST, x, y, wPx, hPx, N.SWP_NOACTIVATE);
        // la parte che esce dallo schermo non deve comparire su quello accanto
        var a = Schermo.Area;
        var forma = Gradini.Regione(wPx, hPx, unitaPx, Gradini.Barra);
        var taglio = N.CreateRectRgn(Math.Max(0, a.L - x), Math.Max(0, a.T - y), Math.Min(wPx, a.R - x), Math.Min(hPx, a.B - y));
        N.CombineRgn(forma, forma, taglio, 1);
        N.DeleteObject(taglio);
        N.SetWindowRgn(Hwnd, forma, true);
    }

    bool SchermoIntero()
    {
        var fg = N.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == Hwnd) return false;
        var c = N.Classe(fg);
        if (c is "WorkerW" or "Progman" or "Shell_TrayWnd") return false;
        // le finestre massimizzate non contano: solo il vero schermo intero (giochi, video, F11)
        if (N.IsZoomed(fg)) return false;
        if (!N.GetWindowRect(fg, out var r)) return false;
        var a = Schermo.Area;
        return r.L <= a.L && r.T <= a.T && r.R >= a.R && r.B >= a.B;
    }

    // Sempre visibile: si fa da parte solo davanti a un'app a schermo intero, scivolando a sinistra.
    DateTime controllo, verifica;

    // La barra vuole il giro veloce mentre scivola, mentre il mouse ci sta sopra o aspetta un'anteprima.
    public bool Occupata(N.POINT p)
    {
        if (visib != obiettivo || inAttesa != null) return true;
        var r = RettangoloFinale;
        return mostrata && p.X >= r.L - 2 && p.X < r.R + 2 && p.Y >= r.T && p.Y < r.B;
    }

    public void Tick(N.POINT p, double dt)
    {
        var adesso = DateTime.Now;
        // se la shell ha perso la riserva (riavvio di Explorer, barra vecchia chiusa male) la si rifà
        if (mostrata && (adesso - verifica).TotalSeconds >= 3)
        {
            verifica = adesso;
            var mi = new N.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.MONITORINFOEX>() };
            if (riservaPx > 0 && N.GetMonitorInfo(Schermo.H, ref mi) && AreaVoluta(mi.rcWork) != null) App.Fase = "applica " + Schermo.Nome; Applica();
        }
        if ((adesso - controllo).TotalMilliseconds >= 150)
        {
            controllo = adesso;
            App.Fase = "schermo intero"; obiettivo = SchermoIntero() ? 0 : 1;
            if (obiettivo == 0 && App.Io.Pannello is Pannello pan && pan.Aperto && pan.Ospite == this) pan.Chiudi();
        }

        if (visib != obiettivo)
        {
            double passo = dt / (obiettivo > visib ? 260.0 : 160.0);
            visib = obiettivo > visib ? Math.Min(1, visib + passo) : Math.Max(0, visib - passo);
            if (visib > 0 && !mostrata)
            {
                mostrata = true;
                Muovi();
                N.ShowWindow(Hwnd, N.SW_SHOWNOACTIVATE);
                // la shell conta solo le barre visibili: si riserva lo spazio dopo essere comparsa
                App.Fase = "riserva " + Schermo.Nome; Riserva();
                App.Fase = "stato"; AggiornaStato();
            }
            if (visib <= 0 && mostrata)
            {
                mostrata = false;
                N.ShowWindow(Hwnd, N.SW_HIDE);
            }
            else Muovi();
        }

        App.Fase = "muovi/anteprime"; if (mostrata) Anteprime(p);
        if (mostrata && (DateTime.Now - ultimoAggiorno).TotalMilliseconds > 1000)
        {
            ultimoAggiorno = DateTime.Now;
            App.Fase = "ora+stato"; AggiornaOra();
            AggiornaStato();
        }
    }

    static bool MenuAperto()
    {
        var m = N.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "#32768", null);
        return m != IntPtr.Zero && N.IsWindowVisible(m);
    }

    void AggiornaOra()
    {
        var it = CultureInfo.GetCultureInfo("it-IT");
        var adesso = DateTime.Now;
        ora.Text = adesso.ToString("HH:mm", it);
        data.Text = adesso.ToString("d MMM", it).Replace(".", "");
    }

    void AggiornaStato()
    {
        glifoRete.Text = Rete.Glifo();
        glifoVolume.Text = Audio.Glifo();
        if (glifoBatteria != null)
        {
            glifoBatteria.Text = Batteria.Glifo();
            var testo = $"Batteria {Batteria.Percento}%" + (Batteria.InCarica ? " · in carica" : "");
            if (testo != testoBatteria) { testoBatteria = testo; tastoBatteria.ToolTip = Suggerimento(testo, true); }
        }
        tastoTray.Visibility = App.Io.Tray.Visibili().Any() ? Visibility.Visible : Visibility.Collapsed;
    }

    // Ricostruisce i tasti delle app solo dove serve, così l'anteprima aperta non sparisce.
    DateTime ultimoAggiornoApp = DateTime.MinValue;
    void AggiornaApp()
    {
        try { AggiornaAppDavvero(); } finally { ultimoAggiornoApp = DateTime.Now; }
    }

    void AggiornaAppDavvero()
    {
        var gruppi = App.Io.Finestre.Gruppi;
        var chiavi = new HashSet<string>(gruppi.Select(g => g.Chiave));
        foreach (var k in tasti.Keys.Where(k => !chiavi.Contains(k)).ToList())
        {
            var via = tasti[k];
            tasti.Remove(k);
            if (mostrata) via.Esci(() => app.Children.Remove(via));
            else app.Children.Remove(via);
        }
        for (int i = 0; i < gruppi.Count; i++)
        {
            var g = gruppi[i];
            bool nuovo = false;
            if (!tasti.TryGetValue(g.Chiave, out var t))
            {
                t = NuovoTasto();
                tasti[g.Chiave] = t;
                nuovo = true;
            }
            t.Imposta(g);
            // i tasti che stanno uscendo restano dove sono; gli altri prendono l'ordine dei gruppi
            var vivi = app.Children.OfType<TastoApp>().Where(x => x.IsHitTestVisible).ToList();
            if (i >= vivi.Count || vivi[i] != t)
            {
                app.Children.Remove(t);
                vivi.Remove(t);
                int dove = i < vivi.Count ? app.Children.IndexOf(vivi[i]) : app.Children.Count;
                app.Children.Insert(dove, t);
            }
            if (nuovo && mostrata && ultimoAggiornoApp != DateTime.MinValue) t.Entra();
        }
        if (App.Io.Pannello is Pannello p && p.Aperto && p.Ospite == this && p.Ancora is TastoApp ta && !p.Attivabile)
        {
            if (ta.Gruppo.Finestre.Count == 0 || !tasti.ContainsValue(ta)) p.Chiudi();
            else if (p.Tag is string firma && firma != ta.Gruppo.Firma) ApriAnteprima(ta);
        }
    }

    TastoApp NuovoTasto()
    {
        var t = new TastoApp();
        t.Sinistro = () =>
        {
            var g = t.Gruppo;
            if (App.Io.Pannello is Pannello p && !p.Attivabile) p.Chiudi();
            if (g.Finestre.Count == 0) { g.Fissata?.Avvia(); return; }
            if (g.Finestre.Count == 1)
            {
                var h = g.Finestre[0].H;
                if (N.GetForegroundWindow() == h && !N.IsIconic(h)) N.ShowWindow(h, N.SW_MINIMIZE);
                else N.Attiva(h);
                return;
            }
            // più finestre: passa alla successiva, partendo da quella in primo piano
            int i = g.Finestre.FindIndex(f => f.H == N.GetForegroundWindow());
            N.Attiva(g.Finestre[(i + 1) % g.Finestre.Count].H);
        };
        t.Centrale = () => Contenuti.NuovaIstanza(t.Gruppo);
        t.Destro = () => { attesa?.Stop(); inAttesa = null; Apri(t, Contenuti.MenuApp(t.Gruppo)); };
        // L'anteprima esce solo se il mouse resta quasi fermo sull'icona: passando sopra la barra non si apre nulla.
        // Ogni movimento di qualche pixel fa ripartire l'attesa.
        Point fermo = default;
        void Aspetta()
        {
            if (t.Gruppo.Finestre.Count == 0) return;
            if (App.Io.Pannello is Pannello p && p.Aperto && p.Attivabile) return;
            inAttesa = t;
            bool gia = App.Io.Pannello is Pannello q && q.Aperto && !q.Attivabile && q.Ospite == this;
            if (attesa == null)
            {
                attesa = new DispatcherTimer();
                attesa.Tick += (_, _) =>
                {
                    attesa.Stop();
                    if (inAttesa is TastoApp x && x.IsMouseOver) ApriAnteprima(x);
                };
            }
            attesa.Stop();
            attesa.Interval = TimeSpan.FromMilliseconds(gia ? 250 : 550);
            attesa.Start();
        }
        t.MouseEnter += (_, e) => { fermo = e.GetPosition(t); Aspetta(); };
        t.MouseMove += (_, e) =>
        {
            var q = e.GetPosition(t);
            if (Math.Abs(q.X - fermo.X) + Math.Abs(q.Y - fermo.Y) < 4) return;
            fermo = q;
            // con l'anteprima di questo tasto già aperta non serve riaspettare
            if (App.Io.Pannello is Pannello p && p.Aperto && p.Ancora == t) return;
            Aspetta();
        };
        t.MouseLeave += (_, _) => { if (inAttesa == t) { inAttesa = null; attesa?.Stop(); } };
        return t;
    }

    void ApriAnteprima(TastoApp t)
    {
        // un menu o un pannello aperto col clic ha la precedenza sull'anteprima
        if (App.Io.Pannello is Pannello m && m.Aperto && m.Attivabile) return;
        if (App.Io.Pannello is Pannello p && p.Aperto && !p.Attivabile && p.Ancora == t && p.Tag as string == t.Gruppo.Firma) return;
        var pan = Contenuti.Anteprima(this, t);
        if (pan != null) Mostra(pan);
    }

    // L'anteprima si chiude quando il mouse non è né sul suo tasto né sopra di lei.
    DateTime fuoriDa = DateTime.MaxValue;
    void Anteprime(N.POINT p)
    {
        if (App.Io.Pannello is not Pannello pan || !pan.Aperto || pan.Attivabile || pan.Ospite != this) return;
        bool su = pan.Contiene(p) || pan.Ancora.IsMouseOver || (inAttesa != null && inAttesa.IsMouseOver);
        if (su) { fuoriDa = DateTime.MaxValue; return; }
        if (fuoriDa == DateTime.MaxValue) fuoriDa = DateTime.Now;
        else if ((DateTime.Now - fuoriDa).TotalMilliseconds > 280) { pan.Chiudi(); fuoriDa = DateTime.MaxValue; }
    }

    public void Apri(FrameworkElement ancora, UIElement contenuto, double? cx = null)
    {
        var vecchio = App.Io.Pannello;
        if (vecchio != null && vecchio.Aperto && vecchio.Ancora == ancora && vecchio.Attivabile) { vecchio.Chiudi(); return; }
        if (contenuto == null) return;
        Mostra(new Pannello(this, ancora, contenuto) { Tag = cx });
    }

    public void Mostra(Pannello p)
    {
        App.Io.Sveglia();
        App.Io.Pannello?.Chiudi();
        App.Io.Pannello = p;
        p.Chiuso += () => { if (App.Io.Pannello == p) App.Io.Pannello = null; };
        p.Apri();
    }

    double CentroDa(System.Windows.Input.MouseEventArgs e) => Orizzontale ? e.GetPosition(this).X : e.GetPosition(this).Y;

    // Centro in pixel veri di un elemento (o di un punto scelto) lungo la barra: ordinata in verticale, ascissa in orizzontale.
    public int CentroSchermo(FrameworkElement el)
    {
        int inizio = Orizzontale ? xPx : yPx;
        if (App.Io.Pannello is Pannello p && p.Ancora == el && p.Tag is double v) return inizio + (int)Math.Round(v * scala);
        try
        {
            var c = el.TransformToAncestor(this).Transform(new Point(el.ActualWidth / 2, el.ActualHeight / 2));
            return inizio + (int)Math.Round((Orizzontale ? c.X : c.Y) * scala);
        }
        catch { return Orizzontale ? xPx + wPx / 2 : yPx + hPx / 2; }
    }

    public N.RECT? RettangoloApp(IntPtr h)
    {
        if (!mostrata) return null;
        foreach (var t in tasti.Values)
            if (t.Gruppo != null && t.Gruppo.Finestre.Any(f => f.H == h)) return RettangoloDi(t);
        return null;
    }

    public void Scollega()
    {
        Libera();
        App.Io.Finestre.Cambiati -= AggiornaApp;
        Rete.Cambiata -= AggiornaStato;
        global::BarraVascend.Notifiche.Cambiate -= AggiornaContatore;
    }
}
