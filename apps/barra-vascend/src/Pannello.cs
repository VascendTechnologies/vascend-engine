using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BarraVascend;

// Chi apre i pannelli: la barra (pannelli a destra, staccati) o l'isola (il pannello nasce dall'isola stessa).
interface IOspite
{
    Schermo Schermo { get; }
    N.RECT RettangoloFinale { get; }
    int CentroSchermo(FrameworkElement el);
    N.RECT RettangoloDi(FrameworkElement el);
    bool PannelliSotto { get; }
    bool PannelliDestra => false;
    bool PannelliSinistra => false;
    bool Attaccato { get; }
    void Coperto(bool si);
}

// Pannello in vetro ghiacciato con angoli a pixel. Si apre con un morph: parte dal rettangolo
// dell'elemento cliccato e si espande fino alla sua misura, poi torna lì quando si chiude.
class Pannello : Window, IFinestraPixel
{
    public readonly IOspite Ospite;
    public readonly FrameworkElement Ancora;
    public readonly bool Attivabile;
    public IntPtr Hwnd;
    public event Action Chiuso;
    readonly Cornice cornice;
    readonly UIElement contenuto;
    readonly ScaleTransform scalaContenuto = new(1, 1);
    readonly TranslateTransform spostaContenuto = new();
    double scala = 1;
    int unitaPx = 2;
    bool chiuso, distrutto, avvisato, animando;
    N.RECT origine, attuale, da, a;
    bool haOrigine;
    DateTime inizio;
    double durata;
    int fase; // 1 apertura, 2 cambio misura, 3 chiusura

    public double UnitaDip => unitaPx / scala;

    public Pannello(IOspite barra, FrameworkElement ancora, UIElement contenuto, bool attivabile = true)
    {
        Ospite = barra;
        Ancora = ancora;
        Attivabile = attivabile;
        this.contenuto = contenuto;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = attivabile;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        Title = "BarraVascend pannello";
        var gruppo = new TransformGroup();
        gruppo.Children.Add(scalaContenuto);
        gruppo.Children.Add(spostaContenuto);
        contenuto.RenderTransform = gruppo;
        contenuto.RenderTransformOrigin = barra.PannelliDestra ? new Point(0, .5) : barra.PannelliSinistra ? new Point(1, .5) : new Point(.5, barra.PannelliSotto ? 0 : 1);
        contenuto.Opacity = 0;
        cornice = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.GhiaccioPannello, Bordo = Tema.Bordo, Child = contenuto, Area = new Rect(0, 0, 0, 0) };
        Content = cornice;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(Hwnd);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            src.AddHook(Gancio);
            N.StileStrumento(Hwnd, !attivabile);
            N.Vetro(Hwnd);
            N.SetWindowRgn(Hwnd, N.CreateRectRgn(0, 0, 0, 0), false);
            Scala();
        };
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(Posiziona);
        Deactivated += (_, _) => { if (Attivabile) Chiudi(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Chiudi(); };
        Closed += (_, _) => { distrutto = true; Fine(); };
    }

    void Fine()
    {
        chiuso = true;
        if (avvisato) return;
        avvisato = true;
        Chiuso?.Invoke();
    }

    void Scala()
    {
        var mon = N.MonitorFromWindow(Hwnd, 2);
        N.GetDpiForMonitor(mon, 0, out uint dx, out _);
        scala = dx / 96.0;
        unitaPx = Math.Max(2, (int)Math.Round(2 * scala, MidpointRounding.AwayFromZero));
    }

    IntPtr Gancio(IntPtr h, int m, IntPtr w, IntPtr l, ref bool gestito)
    {
        if (m == N.WM_DPICHANGED) Dispatcher.BeginInvoke(() => { Scala(); Ridisegna(this); Posiziona(); });
        if (m == N.WM_MOUSEACTIVATE && !Attivabile) { gestito = true; return (IntPtr)3; }
        return IntPtr.Zero;
    }

    public static void Ridisegna(DependencyObject o)
    {
        if (o is UIElement u) u.InvalidateVisual();
        int n = VisualTreeHelper.GetChildrenCount(o);
        for (int i = 0; i < n; i++) Ridisegna(VisualTreeHelper.GetChild(o, i));
    }

    public void Apri()
    {
        var helper = new WindowInteropHelper(this);
        helper.EnsureHandle();
        // prima sullo schermo giusto, poi visibile: così prende subito la scala di quello schermo
        var r = Ospite.RettangoloFinale;
        int x0 = Ospite.PannelliDestra ? r.R + 20 : Ospite.PannelliSinistra ? r.L - 200 : r.L + 20, y0 = Ospite.PannelliDestra || Ospite.PannelliSinistra ? r.T + 20 : Ospite.PannelliSotto ? r.T + 4 : r.T - 200;
        N.SetWindowPos(Hwnd, N.HWND_TOPMOST, x0, y0, 0, 0, N.SWP_NOSIZE | N.SWP_NOACTIVATE);
        Scala();
        Show();
        if (Attivabile) { N.Attiva(Hwnd); Activate(); }
        Posiziona();
    }

    // Misura finale: centrata sull'ancora (o sull'isola), dentro lo schermo.
    public void Posiziona()
    {
        if (chiuso || Hwnd == IntPtr.Zero || !IsLoaded) return;
        N.GetWindowRect(Hwnd, out var me);
        int w = me.W, h = me.H;
        if (w <= 0 || h <= 0) return;
        var m = Ospite.Schermo.Area;
        var host = Ospite.RettangoloFinale;
        int gap = (int)Math.Round(8 * scala);
        int x, y;
        if (Ospite.PannelliDestra || Ospite.PannelliSinistra)
        {
            // a fianco della barra, centrato in altezza sul tasto
            x = Ospite.PannelliDestra ? host.R + gap : host.L - gap - w;
            y = Math.Clamp(Ospite.CentroSchermo(Ancora) - h / 2, m.T + 8, Math.Max(m.T + 8, m.B - 8 - h));
        }
        else
        {
            int cx = Ospite.Attaccato ? host.L + host.W / 2 : Ospite.CentroSchermo(Ancora);
            x = Math.Clamp(cx - w / 2, m.L + 8, m.R - 8 - w);
            y = Ospite.Attaccato ? host.T : Ospite.PannelliSotto ? host.B + gap : host.T - h - gap;
        }
        N.SetWindowPos(Hwnd, N.HWND_TOPMOST, x, y, 0, 0, N.SWP_NOSIZE | N.SWP_NOACTIVATE);
        var meta = new N.RECT { L = x, T = y, R = x + w, B = y + h };
        if (!haOrigine)
        {
            haOrigine = true;
            origine = Origine(meta);
            attuale = origine;
            Morph(origine, meta, 300, 1);
            if (Ospite.Attaccato) Ospite.Coperto(true);
        }
        else if (meta.L != a.L || meta.T != a.T || meta.R != a.R || meta.B != a.B)
        {
            origine = Origine(meta);
            Morph(attuale, meta, 220, fase == 1 && animando ? 1 : 2);
        }
    }

    // Da dove nasce il pannello: l'isola intera, oppure il tasto della barra appoggiato al bordo del pannello.
    N.RECT Origine(N.RECT meta)
    {
        int minimo = unitaPx * 10;
        N.RECT o;
        if (Ospite.Attaccato) o = Ospite.RettangoloFinale;
        else
        {
            var t = Ospite.RettangoloDi(Ancora);
            int alto = Math.Max(minimo, t.H), largo = Math.Max(minimo, t.W);
            o = Ospite.PannelliDestra
                ? new N.RECT { L = meta.L, R = meta.L + largo, T = t.T, B = t.B }
                : Ospite.PannelliSinistra
                ? new N.RECT { L = meta.R - largo, R = meta.R, T = t.T, B = t.B }
                : Ospite.PannelliSotto
                ? new N.RECT { L = t.L, R = t.R, T = meta.T, B = meta.T + alto }
                : new N.RECT { L = t.L, R = t.R, T = meta.B - alto, B = meta.B };
        }
        if (o.W < minimo) { int c = (o.L + o.R) / 2; o.L = c - minimo / 2; o.R = c + minimo / 2; }
        if (o.H < minimo) { int c = (o.T + o.B) / 2; o.T = c - minimo / 2; o.B = c + minimo / 2; }
        o.L = Math.Clamp(o.L, meta.L, meta.R - minimo);
        o.R = Math.Clamp(o.R, o.L + minimo, meta.R);
        o.T = Math.Clamp(o.T, meta.T, meta.B - minimo);
        o.B = Math.Clamp(o.B, o.T + minimo, meta.B);
        return o;
    }

    void Morph(N.RECT da0, N.RECT a0, double ms, int f)
    {
        da = da0;
        a = a0;
        durata = ms;
        fase = f;
        inizio = DateTime.Now;
        if (!animando)
        {
            animando = true;
            CompositionTarget.Rendering += Fotogramma;
        }
        Fotogramma(null, EventArgs.Empty);
    }

    static double Lerp(double x, double y, double t) => x + (y - x) * t;

    void Fotogramma(object s, EventArgs e)
    {
        if (distrutto) { Ferma(); return; }
        double t = Math.Clamp((DateTime.Now - inizio).TotalMilliseconds / durata, 0, 1);
        double k = fase == 3 ? t * t * t : 1 - Math.Pow(1 - t, 4);
        attuale = new N.RECT
        {
            L = (int)Math.Round(Lerp(da.L, a.L, k)),
            T = (int)Math.Round(Lerp(da.T, a.T, k)),
            R = (int)Math.Round(Lerp(da.R, a.R, k)),
            B = (int)Math.Round(Lerp(da.B, a.B, k)),
        };
        N.GetWindowRect(Hwnd, out var fin);
        int x = attuale.L - fin.L, y = attuale.T - fin.T, w = attuale.W, h = attuale.H;
        if (w > 0 && h > 0)
        {
            var rgn = Gradini.Regione(w, h, unitaPx, Gradini.Barra);
            N.OffsetRgn(rgn, x, y);
            N.SetWindowRgn(Hwnd, rgn, true);
            cornice.Area = new Rect(x / scala, y / scala, w / scala, h / scala);
        }
        // il contenuto compare quando la forma è quasi aperta e sparisce per primo in chiusura
        if (fase == 1)
        {
            double c = Math.Clamp((t - .3) / .7, 0, 1);
            c = 1 - Math.Pow(1 - c, 3);
            contenuto.Opacity = c;
            scalaContenuto.ScaleX = scalaContenuto.ScaleY = .96 + .04 * c;
            if (Ospite.PannelliDestra) spostaContenuto.X = -8 * (1 - c);
            else if (Ospite.PannelliSinistra) spostaContenuto.X = 8 * (1 - c);
            else spostaContenuto.Y = (Ospite.PannelliSotto ? -8 : 8) * (1 - c);
        }
        else if (fase == 3)
        {
            double c = Math.Clamp(1 - t * 2.2, 0, 1);
            contenuto.Opacity = c;
            scalaContenuto.ScaleX = scalaContenuto.ScaleY = .96 + .04 * c;
        }
        if (t >= 1)
        {
            Ferma();
            if (fase == 3)
            {
                if (Ospite.Attaccato) Ospite.Coperto(false);
                try { Close(); } catch { }
            }
            else
            {
                contenuto.Opacity = 1;
                scalaContenuto.ScaleX = scalaContenuto.ScaleY = 1;
                spostaContenuto.X = spostaContenuto.Y = 0;
            }
        }
    }

    void Ferma()
    {
        if (!animando) return;
        animando = false;
        CompositionTarget.Rendering -= Fotogramma;
    }

    // Lo spazio fra barra e pannello conta come parte del pannello (l'anteprima non si chiude attraversandolo).
    public bool Contiene(N.POINT p)
    {
        if (chiuso || Hwnd == IntPtr.Zero) return false;
        N.GetWindowRect(Hwnd, out var r);
        var o = Ospite.RettangoloFinale;
        if (Ospite.PannelliDestra) return p.Y >= r.T && p.Y < r.B && p.X >= o.R - 2 && p.X < r.R;
        if (Ospite.PannelliSinistra) return p.Y >= r.T && p.Y < r.B && p.X >= r.L && p.X < o.L + 2;
        return p.X >= r.L && p.X < r.R && (Ospite.PannelliSotto ? p.Y >= o.B - 2 && p.Y < r.B : p.Y >= r.T && p.Y < o.T + 2);
    }

    public bool Aperto => !chiuso;

    public void Chiudi()
    {
        if (chiuso) return;
        Fine();
        if (Hwnd == IntPtr.Zero || !haOrigine || distrutto)
        {
            if (Ospite.Attaccato) Ospite.Coperto(false);
            try { Close(); } catch { }
            return;
        }
        Morph(attuale, origine, 170, 3);
    }
}
