using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BarraVascend;

// Finestre che stanno sul desktop, sotto tutte le altre (isola e orologio).
static class Fondo
{
    [StructLayout(LayoutKind.Sequential)]
    struct WINDOWPOS { public IntPtr hwnd, after; public int x, y, cx, cy; public uint flags; }

    public static IntPtr Gancio(IntPtr h, int m, IntPtr w, IntPtr l, ref bool gestito)
    {
        if (m == 0x46)
        {
            var p = Marshal.PtrToStructure<WINDOWPOS>(l);
            p.after = (IntPtr)1;
            p.flags &= ~N.SWP_NOZORDER;
            Marshal.StructureToPtr(p, l, false);
        }
        if (m == N.WM_MOUSEACTIVATE) { gestito = true; return (IntPtr)3; }
        return IntPtr.Zero;
    }
}

// Linea sottile in fondo all'isola che mostra quanto della canzone è passato.
class Avanzamento : FrameworkElement
{
    // si ridisegna solo quando la parte piena cambia davvero di almeno una cella
    public double Valore
    {
        get => v;
        set
        {
            v = value;
            double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
            int c = (int)Math.Round(ActualWidth * v / u);
            if (c != celle) { celle = c; InvalidateVisual(); }
        }
    }
    double v;
    int celle = -1;
    public Avanzamento() { IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext dc)
    {
        double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
        dc.DrawRectangle(Tema.Pennello(235, 246, 255, .18), null, new Rect(0, 0, ActualWidth, u));
        dc.DrawRectangle(Tema.LimePennello, null, new Rect(0, 0, Math.Round(ActualWidth * v / u) * u, u));
    }
}

// Immagine con gli angoli tagliati a pixel.
class ImmaginePixel : FrameworkElement
{
    public ImageSource Sorgente { get => s; set { s = value; InvalidateVisual(); } }
    ImageSource s;
    public int[] Profilo = Gradini.Piccolo;
    protected override void OnRender(DrawingContext dc)
    {
        double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
        var forma = Gradini.Riempimento(ActualWidth, ActualHeight, u, Profilo);
        if (s == null) { dc.DrawGeometry(Tema.Pennello(255, 255, 255, .14), null, forma); return; }
        dc.PushClip(forma);
        dc.DrawImage(s, new Rect(0, 0, ActualWidth, ActualHeight));
        dc.Pop();
    }
}

// Titolo che scorre avanti e indietro quando non entra.
class Scorrevole : Canvas
{
    readonly TextBlock testo;
    readonly TranslateTransform sposta = new();
    readonly double max;
    public Scorrevole(double larghezza, double size, FontWeight peso, Brush colore)
    {
        max = larghezza;
        Height = size * 1.45;
        ClipToBounds = true;
        testo = Tema.Scritta("", size, colore, peso);
        testo.RenderTransform = sposta;
        Children.Add(testo);
    }

    public string Testo
    {
        get => testo.Text;
        set
        {
            if (testo.Text == value) return;
            testo.Text = value;
            Scorri();
        }
    }

    // Scorre due volte quando arriva un titolo nuovo, poi si ferma all'inizio: un'animazione infinita
    // terrebbe WPF a ridisegnare di continuo. Ripassandoci col mouse riparte.
    protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e) { base.OnMouseEnter(e); if (sposta.HasAnimatedProperties == false) Scorri(); }

    void Scorri()
    {
        testo.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = testo.DesiredSize.Width;
        Width = max;
        sposta.BeginAnimation(TranslateTransform.XProperty, null);
        sposta.X = 0;
        if (w > max)
        {
            double corsa = w - max + 6;
            var a = new DoubleAnimationUsingKeyFrames { RepeatBehavior = new RepeatBehavior(2) };
            // scorre piano: 30 fotogrammi al secondo bastano e dimezzano il lavoro
            Timeline.SetDesiredFrameRate(a, 30);
            var pausa = TimeSpan.FromSeconds(1.6);
            var viaggio = TimeSpan.FromSeconds(corsa / 28);
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(pausa)));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(-corsa, KeyTime.FromTimeSpan(pausa + viaggio), new SineEase { EasingMode = EasingMode.EaseInOut }));
            a.KeyFrames.Add(new DiscreteDoubleKeyFrame(-corsa, KeyTime.FromTimeSpan(pausa + viaggio + pausa)));
            a.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(pausa + viaggio + pausa + viaggio), new SineEase { EasingMode = EasingMode.EaseInOut }));
            a.Completed += (_, _) => { sposta.BeginAnimation(TranslateTransform.XProperty, null); sposta.X = 0; };
            sposta.BeginAnimation(TranslateTransform.XProperty, a);
        }
    }
}

// Pulsante play: quadrato bianco a pixel con il simbolo nero, come il play di Spotify.
static class Comandi
{
    public static Tasto Play(double lato, double glifo, Func<string> simbolo, out TextBlock t)
    {
        t = Tema.Glifo(simbolo(), glifo, Tema.Pennello(0, 0, 0, 1));
        var c = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Bianco, Width = lato, Height = lato, Child = t };
        var b = new Tasto(c, lato + 6, lato + 6) { Sinistro = Musica.PlayPausa };
        b.Margin = new Thickness(2, 0, 2, 0);
        return b;
    }
}

class Isola : Window, IFinestraPixel, IOspite
{
    public Schermo Schermo { get; }
    public bool PannelliSotto => true;
    public bool Attaccato => true;
    bool coperta;

    // Mentre il pannello si espande dall'isola, l'isola sparisce: sembra lei che cambia forma.
    public void Coperto(bool si)
    {
        coperta = si;
        if (Hwnd == IntPtr.Zero) return;
        if (si) N.SetWindowRgn(Hwnd, N.CreateRectRgn(0, 0, 0, 0), true);
        else Posiziona();
    }

    public N.RECT RettangoloDi(FrameworkElement el)
    {
        try
        {
            var a = el.TransformToAncestor(this).Transform(new Point(0, 0));
            int x = rett.L + (int)Math.Round(a.X * scala), y = rett.T + (int)Math.Round(a.Y * scala);
            return new N.RECT { L = x, T = y, R = x + (int)Math.Round(el.ActualWidth * scala), B = y + (int)Math.Round(el.ActualHeight * scala) };
        }
        catch { return rett; }
    }
    public IntPtr Hwnd;
    double scala = 1;
    int unitaPx = 2;
    N.RECT rett;
    readonly StackPanel musica;
    readonly ImmaginePixel copertina = new() { Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) };
    readonly Scorrevole titolo;
    readonly Avanzamento barretta;
    readonly Stile st = Stile.Attuale;
    double Fattore => Math.Clamp(st.IsolaScala, 60, 200) / 100.0;
    readonly TextBlock playGlifo, cinque, sette, avviso;
    readonly TextBlock cpu, ram;
    readonly Scorrevole titoloIde = new(150, 12, FontWeights.SemiBold, Tema.Pennello(244, 244, 239, 1));
    readonly Border statoIde = new() { Width = 8, Height = 8 };
    readonly TextBlock contoIde = Tema.Scritta("", 10, Tema.Pennello(244, 248, 250, .65), FontWeights.Medium);
    readonly FrameworkElement moduloIde;
    readonly Tasto indietro, avanti, playTasto;
    readonly List<(FrameworkElement modulo, Border divisore)> moduli = new();
    readonly bool conSistema;
    int giri;
    public bool Vuota => moduli.TrueForAll(x => x.modulo.Visibility != Visibility.Visible);
    readonly DispatcherTimer tic;

    public double UnitaDip => unitaPx / scala;
    public N.RECT RettangoloFinale => rett;

    public Isola(Schermo s)
    {
        Schermo = s;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        SizeToContent = SizeToContent.Width;
        Height = 38 * Fattore;
        Title = "BarraVascend isola";
        int largo = Math.Clamp(st.IsolaTitolo, 60, 400);
        titolo = new(largo, 12, FontWeights.SemiBold, Tema.Pennello(244, 244, 239, 1));
        barretta = new() { Height = 2, Width = largo, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 3, 0, 0) };
        copertina.Visibility = st.IsolaCopertina ? Visibility.Visible : Visibility.Collapsed;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var riga = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        var dentro = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 6, 0) };
        dentro.Children.Add(copertina);
        // la barra di avanzamento sta solo sotto il titolo, non sotto i pulsanti
        var colonna = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        colonna.Children.Add(titolo);
        colonna.Children.Add(barretta);
        dentro.Children.Add(colonna);
        var corpo = new Tasto(dentro, double.NaN, 32) { Margin = new Thickness(3, 0, 4, 0) };
        corpo.Sinistro = () => Apri(corpo, PannelloMusica());
        // sopra il titolo compare l'artista, la rotella regola il volume
        corpo.MouseEnter += (_, _) => Scambia(Musica.Artista.Length > 0 ? Musica.Artista : Musica.Titolo);
        corpo.MouseLeave += (_, _) => Scambia(Musica.Titolo);
        corpo.Rotella = d => Audio.Livello += d > 0 ? .02f : -.02f;
        musica = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        musica.Children.Add(corpo);
        indietro = new Tasto(Tema.Glifo("", 11, Tema.Pennello(255, 255, 255, .85)), 26, 28) { Sinistro = Musica.Indietro };
        musica.Children.Add(indietro);
        musica.Children.Add(playTasto = Comandi.Play(20, 9, () => Musica.Suona ? "" : "", out playGlifo));
        avanti = new Tasto(Tema.Glifo("", 11, Tema.Pennello(255, 255, 255, .85)), 26, 28) { Sinistro = Musica.Avanti, Margin = new Thickness(1, 0, 8, 0) };
        musica.Children.Add(avanti);

        var claude = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 12, 0) };
        var logo = new Image { Width = 16, Height = 16, Source = new BitmapImage(new Uri(System.IO.Path.Combine(App.Cartella, "claude.png"))), Margin = new Thickness(0, 0, 8, 0) };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        claude.Children.Add(logo);
        cinque = Tema.Scritta("–", 12, Tema.Pennello(244, 244, 239, 1), FontWeights.Bold);
        sette = Tema.Scritta("–", 12, Tema.Pennello(244, 244, 239, 1), FontWeights.Bold);
        if (st.IsolaCinqueOre) { claude.Children.Add(cinque); claude.Children.Add(Unita("5h")); }
        if (st.IsolaSetteGiorni) { claude.Children.Add(sette); claude.Children.Add(Unita("7g")); }
        avviso = Tema.Glifo("", 11, Tema.Pennello(255, 107, 94, 1));
        avviso.Visibility = Visibility.Collapsed;
        avviso.Margin = new Thickness(2, 0, 0, 0);
        claude.Children.Add(avviso);
        Tasto tc = null;
        tc = new Tasto(claude, double.NaN, 32) { Sinistro = () => Apri(tc, PannelloClaude()) };
        tc.Margin = new Thickness(3, 0, 3, 0);

        var sistema = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 2, 0) };
        cpu = Tema.Scritta("–", 12, Tema.Pennello(244, 244, 239, 1), FontWeights.Bold);
        ram = Tema.Scritta("–", 12, Tema.Pennello(244, 244, 239, 1), FontWeights.Bold);
        if (st.IsolaCpu || !st.IsolaRam) { sistema.Children.Add(cpu); sistema.Children.Add(Unita("cpu")); }
        if (st.IsolaRam) { sistema.Children.Add(ram); sistema.Children.Add(Unita("ram")); }
        var ts = new Tasto(sistema, double.NaN, 32) { Margin = new Thickness(3, 0, 3, 0) };
        ts.Sinistro = () => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); } catch { } };

        // gli agenti del Vascend IDE: icona, sessione in primo piano, pallino dello stato e quante lavorano
        var ide = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(9, 0, 8, 0) };
        var icoIde = new Image { Width = 16, Height = 16, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        try { icoIde.Source = new BitmapImage(new Uri(System.IO.Path.Combine(App.Cartella, "ide.png"))); } catch { }
        RenderOptions.SetBitmapScalingMode(icoIde, BitmapScalingMode.HighQuality);
        ide.Children.Add(icoIde);
        ide.Children.Add(titoloIde);
        statoIde.VerticalAlignment = VerticalAlignment.Center;
        statoIde.Margin = new Thickness(8, 0, 0, 0);
        ide.Children.Add(statoIde);
        contoIde.Margin = new Thickness(5, 0, 0, 0);
        contoIde.VerticalAlignment = VerticalAlignment.Center;
        ide.Children.Add(contoIde);
        Tasto ti = null;
        ti = new Tasto(ide, double.NaN, 32) { Margin = new Thickness(3, 0, 3, 0), Sinistro = () => Apri(ti, PannelloIde()) };
        moduloIde = ti;

        // i moduli nell'ordine scelto nell'Engine, con un divisore fra uno e l'altro
        foreach (var id in Stile.Attuale.IsolaModuli ?? new())
        {
            FrameworkElement m = id switch { "musica" => musica, "claude" => tc, "sistema" => ts, "ide" => ti, _ => null };
            if (m == null || riga.Children.Contains(m)) continue;
            var d = new Border { Width = 1, Height = 16, Background = Tema.Pennello(235, 246, 255, .24), VerticalAlignment = VerticalAlignment.Center };
            riga.Children.Add(d);
            riga.Children.Add(m);
            moduli.Add((m, d));
        }
        conSistema = moduli.Exists(x => x.modulo == ts);

        var vetro = Tema.Ghiaccio.Clone();
        vetro.Opacity = Math.Clamp(st.IsolaOpacita, 10, 100) / 100.0;
        vetro.Freeze();
        Content = new Cornice { Profilo = Gradini.Barra, Fondo = vetro, Bordo = Tema.Bordo, Child = riga, LayoutTransform = Fattore == 1 ? null : new ScaleTransform(Fattore, Fattore) };

        SourceInitialized += (_, _) =>
        {
            Hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(Hwnd);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            src.AddHook(Fondo.Gancio);
            src.AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool g) => { if (m == N.WM_DPICHANGED) Dispatcher.BeginInvoke(Posiziona); return IntPtr.Zero; });
            N.StileStrumento(Hwnd, true);
            N.Vetro(Hwnd);
            N.SetWindowRgn(Hwnd, N.CreateRectRgn(0, 0, 0, 0), false);
        };
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(Posiziona);
        Musica.Cambiata += Aggiorna;
        Claude.Cambiato += Aggiorna;
        Ide.Cambiato += Aggiorna;
        tic = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        tic.Tick += (_, _) =>
        {
            barretta.Valore = Musica.Avanzamento;
            if (conSistema && giri++ % 4 == 0) { cpu.Text = $"{Math.Round(Prestazioni.Cpu)}%"; ram.Text = $"{Math.Round(Prestazioni.Ram)}%"; }
        };
        tic.Start();
        Aggiorna();
    }

    void Scambia(string t)
    {
        if (titolo.Testo == t) return;
        titolo.Testo = t;
        titolo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)));
    }

    // Porta davanti la finestra dell'app che sta suonando (Spotify, browser...).
    static void ApriSorgente()
    {
        var id = Musica.App;
        if (string.IsNullOrEmpty(id)) return;
        foreach (var g in App.Io.Finestre.Gruppi)
        {
            if (g.Finestre.Count == 0) continue;
            bool ok = string.Equals(g.Aumid, id, StringComparison.OrdinalIgnoreCase)
                || (g.Exe != null && string.Equals(System.IO.Path.GetFileName(g.Exe), id, StringComparison.OrdinalIgnoreCase))
                || (g.Aumid != null && id.StartsWith(g.Aumid + ".", StringComparison.OrdinalIgnoreCase));
            if (ok) { App.Io.Pannello?.Chiudi(); N.Attiva(g.Finestre[0].H); return; }
        }
    }

    static TextBlock Unita(string t)
    {
        var s = Tema.Scritta(t, 10, Tema.Pennello(244, 248, 250, .65), FontWeights.Medium);
        s.Margin = new Thickness(3, 1, 10, 0);
        return s;
    }

    void Aggiorna()
    {
        bool c = Musica.Presente;
        musica.Visibility = c && (!st.IsolaSoloSuona || Musica.Suona) ? Visibility.Visible : Visibility.Collapsed;
        barretta.Visibility = c && st.IsolaAvanzamento && Musica.Durata > TimeSpan.Zero ? Visibility.Visible : st.IsolaAvanzamento ? Visibility.Hidden : Visibility.Collapsed;
        var p = Ide.Prima;
        moduloIde.Visibility = Ide.Aperto && p != null ? Visibility.Visible : Visibility.Collapsed;
        if (p != null)
        {
            titoloIde.Testo = p.Titolo;
            statoIde.Background = ColoreStato(p);
            int n = Ide.AlLavoro;
            contoIde.Text = n > 1 ? $"+{n - 1}" : "";
        }
        bool prima = true;
        foreach (var (m, d) in moduli)
        {
            bool v = m.Visibility == Visibility.Visible;
            d.Visibility = v && !prima ? Visibility.Visible : Visibility.Collapsed;
            if (v) prima = false;
        }
        Posiziona();
        if (c)
        {
            if (!musica.IsMouseOver) titolo.Testo = Musica.Titolo;
            copertina.Sorgente = Musica.Copertina;
            indietro.Visibility = avanti.Visibility = Musica.DaAudio || !st.IsolaComandi ? Visibility.Collapsed : Visibility.Visible;
            playTasto.Visibility = st.IsolaComandi ? Visibility.Visible : Visibility.Collapsed;
            playGlifo.Text = Musica.Suona ? "" : "";
            barretta.Valore = Musica.Avanzamento;
        }
        cinque.Text = Claude.Cinque is double a ? $"{Math.Round(a)}%" : "–";
        sette.Text = Claude.Sette is double b ? $"{Math.Round(b)}%" : "–";
        avviso.Visibility = Claude.Scaduto ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Avvia()
    {
        Show();
        Posiziona();
    }

    // In alto al centro, 10 DIP sotto il bordo dello schermo.
    public void Posiziona()
    {
        if (Hwnd == IntPtr.Zero) return;
        N.GetDpiForMonitor(Schermo.H, 0, out uint dx, out _);
        double s = dx / 96.0;
        bool cambiata = Math.Abs(s - scala) > .001;
        scala = s;
        unitaPx = Math.Max(2, (int)Math.Round(2 * scala, MidpointRounding.AwayFromZero));
        N.GetWindowRect(Hwnd, out var r);
        var mi = new N.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.MONITORINFOEX>() };
        var a = N.GetMonitorInfo(Schermo.H, ref mi) ? mi.rcWork : Schermo.Area;
        int w = (int)Math.Round(ActualWidth * scala), h = (int)Math.Round(38 * Fattore * scala), m = (int)Math.Round(Math.Clamp(st.IsolaDistanza, 0, 200) * scala);
        if (w <= 0) w = r.W;
        int x = Stile.Attuale.IsolaPosizione switch { "sinistra" => a.L + m, "destra" => a.R - m - w, _ => a.L + (a.W - w) / 2 };
        int y = a.T + m;
        rett = new N.RECT { L = x, T = y, R = x + w, B = y + h };
        N.SetWindowPos(Hwnd, (IntPtr)1, x, y, w, h, N.SWP_NOACTIVATE);
        if (Vuota) N.SetWindowRgn(Hwnd, N.CreateRectRgn(0, 0, 0, 0), true);
        else if (!coperta) N.SetWindowRgn(Hwnd, Gradini.Regione(w, h, unitaPx, Gradini.Barra), true);
        if (cambiata) Pannello.Ridisegna(this);
    }

    public int CentroSchermo(FrameworkElement el)
    {
        try
        {
            var c = el.TransformToAncestor(this).Transform(new Point(el.ActualWidth / 2, 0));
            return rett.L + (int)Math.Round(c.X * scala);
        }
        catch { return rett.L + rett.W / 2; }
    }

    void Apri(FrameworkElement ancora, UIElement contenuto)
    {
        var v = App.Io.Pannello;
        if (v != null && v.Aperto && v.Ancora == ancora) { v.Chiudi(); return; }
        App.Io.Pannello?.Chiudi();
        var p = new Pannello(this, ancora, contenuto) { MinWidth = ActualWidth };
        App.Io.Pannello = p;
        p.Chiuso += () => { if (App.Io.Pannello == p) App.Io.Pannello = null; };
        p.Apri();
    }

    public void Scollega()
    {
        Musica.Cambiata -= Aggiorna;
        Claude.Cambiato -= Aggiorna;
        Ide.Cambiato -= Aggiorna;
        tic.Stop();
    }

    static Brush ColoreStato(Ide.Sessione s) => s.IdConferma != null || s.Stato == "waiting" ? Tema.Pennello(255, 170, 90, 1)
        : s.Stato == "working" ? Tema.LimePennello : s.Stato == "error" ? Tema.Pennello(255, 107, 94, 1) : Tema.Pennello(255, 255, 255, .35);

    static string Descrivi(Ide.Sessione s)
    {
        if (s.IdConferma != null) return "Chiede il permesso";
        if (s.Stato == "working")
        {
            if (!string.IsNullOrEmpty(s.Strumento)) return s.Strumento;
            if (s.Totali > 0) return $"{s.Fatti}/{s.Totali} · {s.Passo}";
            return s.Dettaglio ?? "Al lavoro";
        }
        if (s.Stato == "waiting") return s.Dettaglio ?? "Aspetta una risposta";
        if (s.Stato == "error") return s.Dettaglio ?? "Errore";
        return s.Dettaglio ?? "Finito";
    }

    // Le sessioni degli agenti dell'IDE: un clic apre la sessione, i permessi si danno da qui.
    UIElement PannelloIde()
    {
        var c = new StackPanel { Width = 340, Margin = new Thickness(14) };
        var lista = new StackPanel();
        c.Children.Add(lista);
        void Disegna()
        {
            lista.Children.Clear();
            if (!Ide.Aperto || Ide.Sessioni.Count == 0) { var v = Tema.Scritta("Nessun agente", 12.5, Tema.BiancoTenue); v.Margin = new Thickness(6, 4, 0, 8); lista.Children.Add(v); return; }
            foreach (var s in Ide.Sessioni.OrderByDescending(x => x.IdConferma != null ? 2 : x.Attiva ? 1 : 0).ThenByDescending(x => x.Aggiornata).Take(8))
            {
                var g = new Grid { Margin = new Thickness(8, 6, 8, 6) };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                g.ColumnDefinitions.Add(new ColumnDefinition());
                g.Children.Add(new Border { Width = 8, Height = 8, Background = ColoreStato(s), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) });
                var testi = new StackPanel();
                var t = Tema.Scritta(s.Titolo, 12.5, null, FontWeights.SemiBold);
                t.TextTrimming = TextTrimming.CharacterEllipsis;
                testi.Children.Add(t);
                var d = Tema.Scritta(Descrivi(s), 11.5, Tema.BiancoTenue);
                d.TextTrimming = TextTrimming.CharacterEllipsis;
                d.Margin = new Thickness(0, 2, 0, 0);
                testi.Children.Add(d);
                Grid.SetColumn(testi, 1);
                g.Children.Add(testi);
                var riga = new StackPanel();
                riga.Children.Add(new Tasto(g, double.NaN, double.NaN) { HorizontalAlignment = HorizontalAlignment.Stretch, Sinistro = () => { Ide.Apri(s); App.Io.Pannello?.Chiudi(); } });
                if (s.IdConferma != null)
                {
                    var conf = new StackPanel { Margin = new Thickness(28, 0, 8, 8) };
                    var q = Tema.Scritta(s.Conferma ?? "", 11.5, Tema.Bianco);
                    q.TextWrapping = TextWrapping.Wrap;
                    conf.Children.Add(q);
                    if (!string.IsNullOrEmpty(s.Comando))
                    {
                        var cmd = Tema.Scritta(s.Comando, 11, Tema.BiancoTenue);
                        cmd.FontFamily = new FontFamily("JetBrainsMono Nerd Font, Cascadia Mono, Consolas");
                        cmd.TextTrimming = TextTrimming.CharacterEllipsis;
                        cmd.Margin = new Thickness(0, 4, 0, 0);
                        conf.Children.Add(cmd);
                    }
                    var tasti = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                    var si = Tema.Scritta("Consenti", 12, Tema.Pennello(20, 28, 36, 1), FontWeights.SemiBold);
                    si.Margin = new Thickness(14, 0, 14, 1);
                    tasti.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.LimePennello, Rilievo = false, Child = si }, double.NaN, 30) { Sinistro = () => Ide.Conferma(s, true) });
                    var no = Tema.Scritta("Nega", 12, Tema.Bianco);
                    no.Margin = new Thickness(14, 0, 14, 1);
                    tasti.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .25), Child = no }, double.NaN, 30) { Margin = new Thickness(6, 0, 0, 0), Sinistro = () => Ide.Conferma(s, false) });
                    conf.Children.Add(tasti);
                    riga.Children.Add(conf);
                }
                lista.Children.Add(riga);
            }
        }
        Disegna();
        var piede = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 8, 0, 0) };
        var nuova = Tema.Scritta("Nuova chat", 12.5);
        nuova.Margin = new Thickness(12, 0, 12, 1);
        piede.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .22), Child = nuova }, double.NaN, 32) { Sinistro = () => { Ide.NuovaChat(); App.Io.Pannello?.Chiudi(); } });
        var apri = Tema.Scritta("Apri l'IDE", 12.5);
        apri.Margin = new Thickness(12, 0, 12, 1);
        piede.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .22), Child = apri }, double.NaN, 32) { Margin = new Thickness(6, 0, 0, 0), Sinistro = () => { Ide.Mostra(); App.Io.Pannello?.Chiudi(); } });
        c.Children.Add(piede);
        Ide.Cambiato += Disegna;
        c.Unloaded += (_, _) => Ide.Cambiato -= Disegna;
        return c;
    }

    static string Tempo(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    // Copertina grande, titolo, artista, avanzamento trascinabile e comandi.
    static UIElement PannelloMusica()
    {
        var c = new StackPanel { Width = 300, Margin = new Thickness(16) };
        var testa = new Grid();
        testa.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        testa.ColumnDefinitions.Add(new ColumnDefinition());
        var cop = new ImmaginePixel { Width = 112, Height = 112, Profilo = Gradini.Barra, Sorgente = Musica.Copertina };
        // la copertina si ingrandisce un poco al passaggio e porta all'app che suona
        var zoom = new ScaleTransform(1, 1);
        cop.RenderTransform = zoom;
        cop.RenderTransformOrigin = new Point(.5, .5);
        cop.Cursor = System.Windows.Input.Cursors.Hand;
        cop.ToolTip = Barra.Suggerimento("Apri l'app");
        void Zoom(double v)
        {
            var an = new DoubleAnimation(v, TimeSpan.FromMilliseconds(220)) { EasingFunction = new BackEase { Amplitude = .5, EasingMode = EasingMode.EaseOut } };
            zoom.BeginAnimation(ScaleTransform.ScaleXProperty, an);
            zoom.BeginAnimation(ScaleTransform.ScaleYProperty, an);
        }
        cop.MouseEnter += (_, _) => Zoom(1.04);
        cop.MouseLeave += (_, _) => Zoom(1);
        cop.MouseLeftButtonUp += (_, _) => ApriSorgente();
        testa.Children.Add(cop);
        var testi = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        var tit = Tema.Scritta(Musica.Titolo, 16, null, FontWeights.SemiBold);
        tit.TextWrapping = TextWrapping.Wrap;
        tit.MaxHeight = 64;
        tit.TextTrimming = TextTrimming.CharacterEllipsis;
        var art = Tema.Scritta(Musica.Artista, 12.5, Tema.BiancoTenue);
        art.TextTrimming = TextTrimming.CharacterEllipsis;
        art.Margin = new Thickness(0, 4, 0, 0);
        testi.Children.Add(tit);
        testi.Children.Add(art);
        // con più fonti audio insieme: frecce per passare dall'una all'altra, con l'icona del processo in mezzo
        var fonti = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(-6, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        var icoFonte = new Image { Width = 18, Height = 18, Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(icoFonte, BitmapScalingMode.HighQuality);
        var numFonte = Tema.Scritta("", 10.5, Tema.BiancoTenue);
        numFonte.VerticalAlignment = VerticalAlignment.Center;
        numFonte.Margin = new Thickness(2, 0, 0, 1);
        fonti.Children.Add(new Tasto(Tema.Glifo("\uE76B", 10, Tema.Pennello(255, 255, 255, .85)), 26, 26) { Sinistro = () => Musica.Cambia(-1) });
        fonti.Children.Add(icoFonte);
        fonti.Children.Add(new Tasto(Tema.Glifo("\uE76C", 10, Tema.Pennello(255, 255, 255, .85)), 26, 26) { Sinistro = () => Musica.Cambia(1) });
        fonti.Children.Add(numFonte);
        testi.Children.Add(fonti);
        string chiaveIcona = null;
        Grid.SetColumn(testi, 1);
        testa.Children.Add(testi);
        c.Children.Add(testa);

        var cur = new Cursore { Margin = new Thickness(0, 14, 0, 0), Valore = Musica.Avanzamento };
        cur.Cambiato += v => Musica.Vai(v);
        c.Children.Add(cur);
        var tempi = new Grid { Margin = new Thickness(0, 2, 0, 6) };
        var da = Tema.Scritta("", 11, Tema.BiancoTenue);
        var a = Tema.Scritta("", 11, Tema.BiancoTenue);
        a.HorizontalAlignment = HorizontalAlignment.Right;
        tempi.Children.Add(da);
        tempi.Children.Add(a);
        c.Children.Add(tempi);

        var comandi = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        var pIndietro = new Tasto(Tema.Glifo("", 15), 44, 40) { Sinistro = Musica.Indietro };
        comandi.Children.Add(pIndietro);
        var play = Comandi.Play(38, 14, () => Musica.Suona ? "" : "", out var pg);
        play.Margin = new Thickness(10, 0, 10, 0);
        comandi.Children.Add(play);
        var pAvanti = new Tasto(Tema.Glifo("", 15), 44, 40) { Sinistro = Musica.Avanti };
        comandi.Children.Add(pAvanti);
        c.Children.Add(comandi);

        // volume di Windows e volume della sola app che suona
        Grid RigaVolume(UIElement icona, Cursore cur, TextBlock perc)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            g.Children.Add(icona);
            Grid.SetColumn(cur, 1);
            g.Children.Add(cur);
            perc.HorizontalAlignment = HorizontalAlignment.Right;
            perc.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(perc, 2);
            g.Children.Add(perc);
            return g;
        }
        var glifoSis = Tema.Glifo(Audio.Glifo(), 14, Tema.BiancoTenue);
        glifoSis.VerticalAlignment = VerticalAlignment.Center;
        var curSis = new Cursore { Valore = Audio.Livello };
        var percSis = Tema.Scritta("", 11, Tema.BiancoTenue);
        curSis.Cambiato += v => { Audio.Livello = (float)v; percSis.Text = $"{Math.Round(v * 100)}%"; glifoSis.Text = Audio.Glifo(); };
        var rigaSis = RigaVolume(glifoSis, curSis, percSis);
        rigaSis.Margin = new Thickness(0, 12, 0, 0);
        rigaSis.ToolTip = Barra.Suggerimento("Windows");
        c.Children.Add(rigaSis);

        var icoApp = new Image { Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(icoApp, BitmapScalingMode.HighQuality);
        var curApp = new Cursore();
        var percApp = Tema.Scritta("", 11, Tema.BiancoTenue);
        curApp.Cambiato += v => { Audio.ImpostaApp(Musica.App, (float)v); percApp.Text = $"{Math.Round(v * 100)}%"; };
        var rigaApp = RigaVolume(icoApp, curApp, percApp);
        c.Children.Add(rigaApp);
        string exeIcona = null;

        void Volumi()
        {
            if (!curSis.IsMouseCaptured)
            {
                curSis.Valore = Audio.Livello;
                percSis.Text = Audio.Muto ? "muto" : $"{Math.Round(Audio.Livello * 100)}%";
                glifoSis.Text = Audio.Glifo();
            }
            var la = Audio.LivelloApp(Musica.App, out var exe);
            rigaApp.Visibility = la == null ? Visibility.Collapsed : Visibility.Visible;
            if (la is float x && !curApp.IsMouseCaptured) { curApp.Valore = x; percApp.Text = $"{Math.Round(x * 100)}%"; }
            if (exe != null && exe != exeIcona) { exeIcona = exe; icoApp.Source = Shell.Icona(exe, 32); rigaApp.ToolTip = Barra.Suggerimento(System.IO.Path.GetFileNameWithoutExtension(exe)); }
        }

        void Agg()
        {
            var senza = Musica.DaAudio ? Visibility.Collapsed : Visibility.Visible;
            pIndietro.Visibility = pAvanti.Visibility = cur.Visibility = tempi.Visibility = senza;
            cop.Sorgente = Musica.Copertina;
            fonti.Visibility = Musica.Fonti.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
            if (Musica.Fonti.Count >= 2)
            {
                int i = Musica.Fonti.FindIndex(v => v.Chiave == Musica.Chiave);
                numFonte.Text = $"{(i < 0 ? 1 : i + 1)}/{Musica.Fonti.Count}";
                if (Musica.Chiave != chiaveIcona) { chiaveIcona = Musica.Chiave; icoFonte.Source = Musica.Icona(Musica.Attuale); }
            }
            tit.Text = Musica.Titolo;
            art.Text = Musica.Artista;
            pg.Text = Musica.Suona ? "" : "";
            if (!cur.IsMouseCaptured) cur.Valore = Musica.Avanzamento;
            da.Text = Tempo(Musica.Ora);
            a.Text = Musica.Durata > TimeSpan.Zero ? Tempo(Musica.Durata) : "";
            Volumi();
        }
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        t.Tick += (_, _) => Agg();
        t.Start();
        Musica.Cambiata += Agg;
        c.Unloaded += (_, _) => { t.Stop(); Musica.Cambiata -= Agg; };
        Agg();
        return c;
    }

    static UIElement Limite(string nome, double? perc, DateTime? reset)
    {
        var g = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        g.RowDefinitions.Add(new RowDefinition());
        g.RowDefinitions.Add(new RowDefinition());
        var n = Tema.Scritta(nome, 12.5, null, FontWeights.SemiBold);
        g.Children.Add(n);
        var d = Tema.Scritta((perc is double p ? $"{Math.Round(p)}%" : "–") + (reset != null ? "  ·  azzera " + Claude.Mancano(reset) : ""), 11.5, Tema.BiancoTenue);
        d.HorizontalAlignment = HorizontalAlignment.Right;
        g.Children.Add(d);
        var b = new Avanzamento { Height = 4, Valore = (perc ?? 0) / 100, Margin = new Thickness(0, 6, 0, 0) };
        b.Height = 2;
        Grid.SetRow(b, 1);
        g.Children.Add(b);
        return g;
    }

    // Limiti, grafico a linee dei giorni (settimana o mese), totale, costo e modelli.
    static int periodo = 7;
    static UIElement PannelloClaude()
    {
        Claude.Aggiorna();
        var c = new StackPanel { Width = 380, Margin = new Thickness(16, 14, 16, 14) };
        var limiti = new StackPanel();
        c.Children.Add(limiti);

        var testa = new Grid { Margin = new Thickness(0, 6, 0, 8) };
        testa.Children.Add(Tema.Scritta("Utilizzo", 13.5, null, FontWeights.SemiBold));
        var interruttore = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        testa.Children.Add(interruttore);
        c.Children.Add(testa);

        var grafico = new Grafico { Height = 150 };
        var cornice = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Pennello(10, 20, 30, .22), Bordo = Tema.Pennello(235, 246, 255, .10), Child = grafico };
        grafico.Margin = new Thickness(10, 10, 10, 6);
        c.Children.Add(cornice);

        var totali = new Grid { Margin = new Thickness(0, 10, 0, 6) };
        var tot = Tema.Scritta("", 13, null, FontWeights.SemiBold);
        var costo = Tema.Scritta("", 20, Tema.LimePennello, FontWeights.Bold);
        costo.HorizontalAlignment = HorizontalAlignment.Right;
        totali.Children.Add(tot);
        totali.Children.Add(costo);
        c.Children.Add(totali);

        var modTit = Tema.Scritta("Modelli", 12, Tema.BiancoTenue, FontWeights.SemiBold);
        modTit.Margin = new Thickness(0, 6, 0, 8);
        c.Children.Add(modTit);
        var modelli = new StackPanel();
        c.Children.Add(modelli);

        var bottoni = new List<(int giorni, Cornice c, TextBlock t)>();
        void Disegna()
        {
            limiti.Children.Clear();
            limiti.Children.Add(Limite("Sessione (5 ore)", Claude.Cinque, Claude.CinqueReset));
            limiti.Children.Add(Limite("Settimana (7 giorni)", Claude.Sette, Claude.SetteReset));
            foreach (var (g, bc, bt) in bottoni)
            {
                bool on = g == periodo;
                bc.Fondo = on ? Tema.LimePennello : Tema.Pennello(255, 255, 255, .10);
                bt.Foreground = on ? Tema.Pennello(20, 28, 36, 1) : Tema.Bianco;
            }
            var r = Claude.Periodo(periodo);
            grafico.Imposta(r.Token, r.Etichette, r.Costi, r.Date);
            tot.Text = Claude.Token(r.Totale) + " token";
            costo.Text = Claude.Soldi(r.Spesa);
            modelli.Children.Clear();
            double maxc = r.Modelli.Count > 0 ? r.Modelli.Max(m => m.costo) : 1;
            int i = 0;
            Color[] colori = { Tema.Lime, Color.FromRgb(120, 200, 255), Color.FromRgb(255, 170, 90), Color.FromRgb(200, 150, 255), Color.FromRgb(255, 120, 150) };
            foreach (var (nome, tok, k) in r.Modelli.Take(5))
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                g.RowDefinitions.Add(new RowDefinition());
                g.RowDefinitions.Add(new RowDefinition());
                g.Children.Add(Tema.Scritta(nome, 12.5));
                var v = Tema.Scritta($"{Claude.Token(tok)} · {Claude.Soldi(k)}", 11.5, Tema.BiancoTenue);
                v.HorizontalAlignment = HorizontalAlignment.Right;
                g.Children.Add(v);
                var b = new Barra2 { Height = 2, Valore = maxc > 0 ? k / maxc : 0, Colore = new SolidColorBrush(colori[i++ % colori.Length]), Margin = new Thickness(0, 6, 0, 0) };
                Grid.SetRow(b, 1);
                g.Children.Add(b);
                modelli.Children.Add(g);
            }
        }
        foreach (var (g, nome) in new[] { (7, "Settimana"), (30, "Mese") })
        {
            var t = Tema.Scritta(nome, 11.5, null, FontWeights.SemiBold);
            t.Margin = new Thickness(10, 3, 10, 4);
            var bc = new Cornice { Profilo = Gradini.Minimo, Child = t };
            var tasto = new Tasto(bc, double.NaN, 26) { Margin = new Thickness(2, 0, 0, 0), Sinistro = () => { periodo = g; Disegna(); } };
            bottoni.Add((g, bc, t));
            interruttore.Children.Add(tasto);
        }
        Claude.Cambiato += Disegna;
        c.Unloaded += (_, _) => Claude.Cambiato -= Disegna;
        Disegna();
        return c;
    }
}

class Barra2 : FrameworkElement
{
    public double Valore;
    public Brush Colore = Tema.LimePennello;
    protected override void OnRender(DrawingContext dc)
    {
        double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
        dc.DrawRectangle(Tema.Pennello(235, 246, 255, .14), null, new Rect(0, 0, ActualWidth, u));
        dc.DrawRectangle(Colore, null, new Rect(0, 0, Math.Max(u, Math.Round(ActualWidth * Valore / u) * u), u));
    }
}

// Grafico a linee morbide con area sfumata, griglia, punti e giorni sotto.
// Segue il mouse (linea, punto e fumetto col giorno) e cambia periodo con una transizione.
class Grafico : FrameworkElement
{
    List<double> valori = new(), costi = new(), mostrati = new(), partenza = new();
    List<string> etichette = new();
    List<DateTime> date = new();
    int sopra = -1;
    DateTime inizio;
    bool anima;

    public Grafico() { Cursor = System.Windows.Input.Cursors.Cross; }

    public void Imposta(List<double> v, List<string> e, List<double> c, List<DateTime> d)
    {
        bool stessi = v.Count == valori.Count && v.SequenceEqual(valori);
        partenza = Ricampiona(mostrati.Count > 0 ? mostrati : v.Select(_ => 0.0).ToList(), v.Count);
        valori = v;
        etichette = e;
        costi = c;
        date = d;
        if (stessi && mostrati.Count == v.Count) { InvalidateVisual(); return; }
        inizio = DateTime.Now;
        if (!anima) { anima = true; CompositionTarget.Rendering += Passo; }
    }

    static List<double> Ricampiona(List<double> s, int n)
    {
        var r = new List<double>();
        if (s.Count == 0) { for (int i = 0; i < n; i++) r.Add(0); return r; }
        for (int i = 0; i < n; i++)
        {
            double x = n == 1 ? 0 : (double)i * (s.Count - 1) / (n - 1);
            int a = (int)Math.Floor(x), b = Math.Min(s.Count - 1, a + 1);
            r.Add(s[a] + (s[b] - s[a]) * (x - a));
        }
        return r;
    }

    void Passo(object o, EventArgs e)
    {
        double t = Math.Clamp((DateTime.Now - inizio).TotalMilliseconds / 420, 0, 1);
        double k = 1 - Math.Pow(1 - t, 3);
        mostrati = valori.Select((v, i) => partenza[i] + (v - partenza[i]) * k).ToList();
        InvalidateVisual();
        if (t >= 1) { anima = false; CompositionTarget.Rendering -= Passo; }
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        int n = valori.Count;
        if (n == 0) return;
        double x = e.GetPosition(this).X;
        int i = n == 1 ? 0 : (int)Math.Round((x - 4) / Math.Max(1, ActualWidth - 8) * (n - 1));
        i = Math.Clamp(i, 0, n - 1);
        if (i != sopra) { sopra = i; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e) { sopra = -1; InvalidateVisual(); }

    protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        var serie = mostrati.Count == valori.Count ? mostrati : valori;
        if (w <= 0 || serie.Count == 0) return;
        double basso = 18, alto = 20, area = h - basso - alto;
        double max = Math.Max(1, valori.Max());
        var griglia = new Pen(Tema.Pennello(235, 246, 255, .08), 1);
        for (int i = 0; i <= 3; i++)
        {
            double y = Math.Round(alto + area * i / 3) + .5;
            dc.DrawLine(griglia, new Point(0, y), new Point(w, y));
        }
        int n = serie.Count;
        var pts = new List<Point>();
        for (int i = 0; i < n; i++) pts.Add(new Point(n == 1 ? w / 2 : 4 + (w - 8) * i / (n - 1), alto + area * (1 - Math.Max(0, serie[i]) / max)));

        var linea = new StreamGeometry();
        using (var g = linea.Open())
        {
            g.BeginFigure(pts[0], false, false);
            Curva(g, pts);
        }
        var riempi = new StreamGeometry();
        using (var g = riempi.Open())
        {
            g.BeginFigure(new Point(pts[0].X, alto + area), true, true);
            g.LineTo(pts[0], false, false);
            Curva(g, pts);
            g.LineTo(new Point(pts[^1].X, alto + area), false, false);
        }
        var sfuma = new LinearGradientBrush(Color.FromArgb(110, Tema.Lime.R, Tema.Lime.G, Tema.Lime.B), Color.FromArgb(0, Tema.Lime.R, Tema.Lime.G, Tema.Lime.B), 90);
        dc.DrawGeometry(sfuma, null, riempi);
        dc.DrawGeometry(null, new Pen(Tema.LimePennello, 2) { LineJoin = PenLineJoin.Round }, linea);
        for (int i = 0; i < n; i++)
        {
            bool ultimo = i == n - 1;
            if ((n > 10 && !ultimo) || i == sopra) continue;
            dc.DrawEllipse(ultimo ? Tema.Bianco : Tema.LimePennello, ultimo ? new Pen(Tema.LimePennello, 2) : null, pts[i], ultimo ? 4 : 2.5, ultimo ? 4 : 2.5);
        }
        var tf = new Typeface(Tema.Testo, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var tfb = new Typeface(Tema.Testo, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        int passo = n > 10 ? 5 : 1;
        for (int i = 0; i < n; i++)
        {
            if ((n - 1 - i) % passo != 0 && i != sopra) continue;
            if (sopra >= 0 && i != sopra && Math.Abs(i - sopra) < (n > 10 ? 3 : 1)) continue;
            var t = new FormattedText(etichette[i], System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, i == sopra ? tfb : tf, 10, i == n - 1 || i == sopra ? Tema.Bianco : Tema.BiancoTenue, dpi);
            dc.DrawText(t, new Point(Math.Clamp(pts[i].X - t.Width / 2, 0, w - t.Width), h - basso + 4));
        }

        if (sopra >= 0 && sopra < n)
        {
            // linea verticale, punto pieno e fumetto a pixel col dettaglio del giorno
            var p = pts[sopra];
            dc.DrawRectangle(Tema.Pennello(255, 255, 255, .25), null, new Rect(Math.Round(p.X), alto, 1, area));
            dc.DrawEllipse(Tema.Bianco, new Pen(Tema.LimePennello, 2), p, 4.5, 4.5);
            var it = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
            string giorno = sopra < date.Count ? date[sopra].ToString("ddd d MMM", it).Replace(".", "") : etichette[sopra];
            var r1 = new FormattedText(giorno, it, FlowDirection.LeftToRight, tf, 10.5, Tema.BiancoTenue, dpi);
            var r2 = new FormattedText($"{Claude.Token(valori[sopra])} · {Claude.Soldi(costi[sopra])}", it, FlowDirection.LeftToRight, tfb, 12, Tema.Bianco, dpi);
            double bw = Math.Max(r1.Width, r2.Width) + 18, bh = r1.Height + r2.Height + 12;
            double bx = Math.Clamp(p.X - bw / 2, 0, w - bw), by = Math.Max(0, p.Y - bh - 10);
            if (by < 0 || p.Y - bh - 10 < 0) by = Math.Min(h - bh, p.Y + 10);
            double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
            dc.PushTransform(new TranslateTransform(Math.Round(bx), Math.Round(by)));
            dc.DrawGeometry(Tema.Pennello(18, 28, 38, .95), null, Gradini.Riempimento(bw, bh, u, Gradini.Piccolo));
            dc.DrawGeometry(Tema.Pennello(225, 242, 255, .30), null, Gradini.Contorno(bw, bh, u, Gradini.Piccolo));
            dc.DrawText(r1, new Point(9, 5));
            dc.DrawText(r2, new Point(9, 5 + r1.Height + 1));
            dc.Pop();
        }
        else
        {
            int imax = valori.IndexOf(valori.Max());
            var testa = new FormattedText($"max {Claude.Token(valori[imax])} · {Claude.Soldi(costi[imax])}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, tf, 10.5, Tema.BiancoTenue, dpi);
            dc.DrawText(testa, new Point(0, 0));
        }
    }

    // Catmull-Rom convertita in curve di Bézier, senza scendere sotto lo zero.
    static void Curva(StreamGeometryContext g, List<Point> p)
    {
        double fondo = p.Max(x => x.Y);
        for (int i = 0; i < p.Count - 1; i++)
        {
            var p0 = p[Math.Max(0, i - 1)];
            var p1 = p[i];
            var p2 = p[i + 1];
            var p3 = p[Math.Min(p.Count - 1, i + 2)];
            var c1 = new Point(p1.X + (p2.X - p0.X) / 6, Math.Min(fondo, p1.Y + (p2.Y - p0.Y) / 6));
            var c2 = new Point(p2.X - (p3.X - p1.X) / 6, Math.Min(fondo, p2.Y - (p3.Y - p1.Y) / 6));
            g.BezierTo(c1, c2, p2, true, false);
        }
    }
}
