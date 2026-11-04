using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace BarraVascend;

// Una voce della cartella: i campi testuali sono già pronti, l'icona arriva quando la riga diventa visibile.
class Elemento : INotifyPropertyChanged
{
    public string Nome { get; set; }
    public string Percorso { get; set; }
    public bool Cartella { get; set; }
    public DateTime Data { get; set; }
    public long Dim { get; set; }
    public string Tipo { get; set; }
    public string DataTesto { get; set; }
    public string DimTesto { get; set; }
    public double Opacita { get; set; } = 1;
    ImageSource icona;
    bool chiesta;
    public ImageSource Icona
    {
        get { if (!chiesta) { chiesta = true; Esplora.ChiediIcona(this); } return icona; }
        set { icona = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icona))); }
    }
    public event PropertyChangedEventHandler PropertyChanged;
}

// Esplora file nello stile della barra: vetro ghiaccio, angoli a pixel, cartelle a sinistra, elenco veloce al centro.
// Si apre con Win+E, dal tasto fissato di Esplora file e dal menu Start.
class Esplora : Window, IFinestraPixel
{
    IntPtr hwnd;
    double scala = 1;
    int unitaPx = 2;
    public double UnitaDip => unitaPx / scala;

    string cartella;
    readonly List<string> indietro = new(), avanti = new();
    List<Elemento> voci = new();
    ListCollectionView vista;
    readonly ListBox lista;
    readonly StackPanel briciole = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBox campoPercorso, filtro;
    readonly TextBlock stato, vuoto;
    readonly StackPanel laterale = new();
    readonly Tasto tIndietro, tAvanti, tSu, tMassimo;
    readonly Grid intestazioni;
    FileSystemWatcher guardia;
    DispatcherTimer rinfresco;
    string ordine = "Nome";
    bool crescente = true;
    int caricamento;
    N.RECT? normale;

    static readonly string Casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static string Wsl => PercorsiUtente.WslRoot;

    public static void Apri(string percorso = null)
    {
        var e = new Esplora(percorso ?? Casa);
        e.Show();
        e.Activate();
    }

    public Esplora(string inizio)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        Title = "Esplora";
        FontFamily = Tema.Testo;
        Foreground = Tema.Bianco;
        Icon = Contenuti.Ico.Esplora;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, GlassFrameThickness = new Thickness(0), ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        // a metà dello schermo dove c'è il mouse
        var cur = N.Cursore();
        var mon = N.MonitorFromPoint(cur, 2);
        N.GetDpiForMonitor(mon, 0, out uint dx, out _);
        var mi = new N.MONITORINFOEX { cbSize = Marshal.SizeOf<N.MONITORINFOEX>() };
        N.GetMonitorInfo(mon, ref mi);
        double s = dx / 96.0, w = Math.Min(1120, mi.rcWork.W / s - 80), h = Math.Min(700, mi.rcWork.H / s - 80);
        Width = w; Height = h;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = mi.rcWork.L / s + (mi.rcWork.W / s - w) / 2 + 24 * (Application.Current.Windows.OfType<Esplora>().Count());
        Top = mi.rcWork.T / s + (mi.rcWork.H / s - h) / 2 + 24 * (Application.Current.Windows.OfType<Esplora>().Count());

        var radice = new Grid();
        radice.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(212) });
        radice.ColumnDefinitions.Add(new ColumnDefinition());

        // colonna sinistra: posti e dischi
        var sinistra = new Border { Background = Tema.Pennello(0, 0, 0, .14) };
        var scorriLato = new ScrollViewer { Content = laterale, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false, Padding = new Thickness(10, 14, 10, 10) };
        sinistra.Child = scorriLato;
        radice.Children.Add(sinistra);

        var destra = new Grid();
        destra.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        destra.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        destra.RowDefinitions.Add(new RowDefinition());
        destra.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(destra, 1);
        radice.Children.Add(destra);

        // barra in alto: navigazione, percorso, filtro, tasti della finestra
        var alto = new Grid { Height = 52, Background = Brushes.Transparent };
        alto.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        alto.ColumnDefinitions.Add(new ColumnDefinition());
        alto.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        alto.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        alto.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource != alto) return;
            if (e.ClickCount == 2) Massimizza(); else { if (normale != null) Ripristina(e); try { DragMove(); } catch { } }
        };
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        tIndietro = new Tasto(Tema.Glifo("\uE72B", 13), 34, 32) { Sinistro = Indietro, ToolTip = Barra.Suggerimento("Indietro  (Alt+←)") };
        tAvanti = new Tasto(Tema.Glifo("\uE72A", 13), 34, 32) { Sinistro = Avanti, ToolTip = Barra.Suggerimento("Avanti  (Alt+→)") };
        tSu = new Tasto(Tema.Glifo("\uE74A", 13), 34, 32) { Sinistro = Su, ToolTip = Barra.Suggerimento("Cartella superiore  (Alt+↑)") };
        nav.Children.Add(tIndietro);
        nav.Children.Add(tAvanti);
        nav.Children.Add(tSu);
        nav.Children.Add(new Tasto(Tema.Glifo("\uE72C", 13), 34, 32) { Sinistro = () => Carica(), ToolTip = Barra.Suggerimento("Aggiorna  (F5)") });
        alto.Children.Add(nav);

        campoPercorso = Campo(13);
        campoPercorso.Visibility = Visibility.Collapsed;
        campoPercorso.Margin = new Thickness(12, 0, 8, 0);
        campoPercorso.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { var p = Environment.ExpandEnvironmentVariables(campoPercorso.Text.Trim().Trim('"')); FinePercorso(); Vai(p); e.Handled = true; }
            if (e.Key == Key.Escape) { FinePercorso(); e.Handled = true; }
        };
        campoPercorso.LostKeyboardFocus += (_, _) => FinePercorso();
        var percorso = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Pennello(255, 255, 255, .07), Bordo = Tema.Pennello(255, 255, 255, .16), Height = 34, Margin = new Thickness(4, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        var dentroPercorso = new Grid { ClipToBounds = true };
        var scorriBriciole = new ScrollViewer { Content = briciole, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Focusable = false, Margin = new Thickness(4, 0, 4, 0) };
        dentroPercorso.Children.Add(scorriBriciole);
        dentroPercorso.Children.Add(campoPercorso);
        percorso.Child = dentroPercorso;
        // clic nello spazio vuoto del percorso: si scrive il percorso a mano
        percorso.MouseLeftButtonUp += (_, e) => { if (e.OriginalSource is not TextBlock && campoPercorso.Visibility != Visibility.Visible) ScriviPercorso(); };
        Grid.SetColumn(percorso, 1);
        alto.Children.Add(percorso);

        filtro = Campo(13);
        var segnaposto = Tema.Scritta("Filtra", 13, Tema.Pennello(255, 255, 255, .38));
        segnaposto.IsHitTestVisible = false;
        segnaposto.VerticalAlignment = VerticalAlignment.Center;
        var riquadroFiltro = new Grid { Margin = new Thickness(10, 0, 10, 0) };
        riquadroFiltro.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        riquadroFiltro.ColumnDefinitions.Add(new ColumnDefinition());
        var lente = Tema.Glifo("\uE721", 12, Tema.BiancoTenue);
        lente.HorizontalAlignment = HorizontalAlignment.Left;
        riquadroFiltro.Children.Add(lente);
        Grid.SetColumn(segnaposto, 1);
        Grid.SetColumn(filtro, 1);
        riquadroFiltro.Children.Add(segnaposto);
        riquadroFiltro.Children.Add(filtro);
        filtro.TextChanged += (_, _) => { segnaposto.Visibility = filtro.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; vista?.Refresh(); Stato(); };
        filtro.KeyDown += (_, e) => { if (e.Key == Key.Escape) { filtro.Text = ""; lista.Focus(); e.Handled = true; } if (e.Key == Key.Down) { lista.Focus(); e.Handled = true; } };
        var boxFiltro = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Pennello(255, 255, 255, .07), Bordo = Tema.Pennello(255, 255, 255, .16), Child = riquadroFiltro, Width = 220, Height = 34, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(boxFiltro, 2);
        alto.Children.Add(boxFiltro);

        var finestra = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        finestra.Children.Add(new Tasto(Tema.Glifo("\uE921", 11), 40, 32) { Sinistro = () => WindowState = WindowState.Minimized });
        tMassimo = new Tasto(Tema.Glifo("\uE922", 11), 40, 32) { Sinistro = Massimizza };
        finestra.Children.Add(tMassimo);
        finestra.Children.Add(new Tasto(Tema.Glifo("\uE8BB", 11), 40, 32) { Sinistro = Close });
        Grid.SetColumn(finestra, 3);
        alto.Children.Add(finestra);
        destra.Children.Add(alto);

        // intestazioni delle colonne: un clic ordina, un altro inverte
        intestazioni = Colonne(new Thickness(22, 2, 26, 4));
        Grid.SetRow(intestazioni, 1);
        destra.Children.Add(intestazioni);

        lista = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Tema.Bianco,
            SelectionMode = SelectionMode.Extended,
            Margin = new Thickness(10, 0, 10, 0),
            ItemContainerStyle = (Style)XamlReader.Parse(StileRiga),
            ItemTemplate = (DataTemplate)XamlReader.Parse(ModelloRiga),
            AllowDrop = true,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(lista, ScrollBarVisibility.Hidden);
        ScrollViewer.SetHorizontalScrollBarVisibility(lista, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(lista, true);
        VirtualizingPanel.SetVirtualizationMode(lista, VirtualizationMode.Recycling);
        VirtualizingPanel.SetScrollUnit(lista, ScrollUnit.Pixel);
        TextSearch.SetTextPath(lista, "Nome");
        lista.MouseDoubleClick += (_, e) => { if (Sotto(e) is Elemento x) ApriVoce(x); };
        lista.SelectionChanged += (_, _) => Stato();
        lista.PreviewMouseRightButtonUp += (_, e) =>
        {
            var x = Sotto(e);
            if (x != null && !lista.SelectedItems.Contains(x)) lista.SelectedItem = x;
            if (x == null) lista.SelectedItems.Clear();
            Menu(x != null);
            e.Handled = true;
        };
        lista.PreviewMouseLeftButtonDown += (_, e) => { partenza = e.GetPosition(lista); sotto = Sotto(e); };
        lista.PreviewMouseMove += Trascina;
        lista.DragOver += (_, e) => { e.Effects = Effetto(e, Destinazione(e)); e.Handled = true; };
        lista.Drop += Rilascia;
        var corpo = new Grid();
        corpo.Children.Add(lista);
        vuoto = Tema.Scritta("", 13, Tema.BiancoTenue);
        vuoto.HorizontalAlignment = HorizontalAlignment.Center;
        vuoto.VerticalAlignment = VerticalAlignment.Center;
        vuoto.IsHitTestVisible = false;
        corpo.Children.Add(vuoto);
        Grid.SetRow(corpo, 2);
        destra.Children.Add(corpo);

        stato = Tema.Scritta("", 11.5, Tema.BiancoTenue);
        stato.Margin = new Thickness(22, 6, 0, 10);
        Grid.SetRow(stato, 3);
        destra.Children.Add(stato);

        Content = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.GhiaccioPannello, Bordo = Tema.Bordo, Child = radice };

        PreviewKeyDown += Tasti;
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.XButton1) { Indietro(); e.Handled = true; }
            if (e.ChangedButton == MouseButton.XButton2) { Avanti(); e.Handled = true; }
        };
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(hwnd);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            N.Vetro(hwnd);
            // la barra riconosce queste finestre come Esplora file
            N.SetProp(hwnd, "BarraVascendEsplora", (IntPtr)1);
            Forma();
        };
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(Forma);
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(Forma);
        Closed += (_, _) => { guardia?.Dispose(); rinfresco?.Stop(); };
        Lato();
        Loaded += (_, _) => { Vai(inizio, false); lista.Focus(); };
    }

    static TextBox Campo(double dim) => new()
    {
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Foreground = Tema.Bianco,
        CaretBrush = Tema.Bianco,
        SelectionBrush = Tema.Pennello(170, 205, 230, 1),
        FontFamily = Tema.Testo,
        FontSize = dim,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    void Forma()
    {
        if (hwnd == IntPtr.Zero) return;
        var mon = N.MonitorFromWindow(hwnd, 2);
        N.GetDpiForMonitor(mon, 0, out uint dx, out _);
        double s = dx / 96.0;
        bool cambiata = Math.Abs(s - scala) > .001;
        scala = s;
        unitaPx = Math.Max(2, (int)Math.Round(2 * scala, MidpointRounding.AwayFromZero));
        N.GetWindowRect(hwnd, out var r);
        N.SetWindowRgn(hwnd, Gradini.Regione(r.W, r.H, unitaPx, Gradini.Barra), true);
        if (cambiata) Pannello.Ridisegna(this);
    }

    // Massimizzata = l'area di lavoro dello schermo (accanto alla barra), senza bordi di Windows.
    void Massimizza()
    {
        if (normale is N.RECT n)
        {
            normale = null;
            N.SetWindowPos(hwnd, IntPtr.Zero, n.L, n.T, n.W, n.H, N.SWP_NOACTIVATE | 0x4);
            tMassimo.Child = Tema.Glifo("\uE922", 11);
            return;
        }
        N.GetWindowRect(hwnd, out var r);
        normale = r;
        var mi = new N.MONITORINFOEX { cbSize = Marshal.SizeOf<N.MONITORINFOEX>() };
        N.GetMonitorInfo(N.MonitorFromWindow(hwnd, 2), ref mi);
        var a = mi.rcWork;
        int m = (int)Math.Round(6 * scala);
        N.SetWindowPos(hwnd, IntPtr.Zero, a.L + m, a.T + m, a.W - 2 * m, a.H - 2 * m, N.SWP_NOACTIVATE | 0x4);
        tMassimo.Child = Tema.Glifo("\uE923", 11);
    }

    // Trascinando la finestra massimizzata torna alla sua misura sotto il mouse.
    void Ripristina(MouseButtonEventArgs e)
    {
        if (normale is not N.RECT n) return;
        var p = N.Cursore();
        N.GetWindowRect(hwnd, out var r);
        double fx = (p.X - r.L) / (double)Math.Max(1, r.W);
        normale = null;
        int x = p.X - (int)(n.W * fx), y = p.Y - (int)(26 * scala);
        N.SetWindowPos(hwnd, IntPtr.Zero, x, y, n.W, n.H, 0x4);
        tMassimo.Child = Tema.Glifo("\uE922", 11);
    }

    // ---------- colonna sinistra ----------

    void Lato()
    {
        laterale.Children.Clear();
        void Posto(string nome, string p, ImageSource ic = null)
        {
            if (!p.StartsWith(@"\\") && !Directory.Exists(p)) return;
            laterale.Children.Add(VoceLato(nome, p, ic ?? Shell.Icona(p, 32)));
        }
        Posto("Home", Casa);
        Posto("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        Posto("Download", Path.Combine(Casa, "Downloads"));
        Posto("Documenti", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        Posto("Immagini", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        Posto("Video", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        Posto("Musica", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        laterale.Children.Add(TitoloLato("Linux"));
        Posto("Progetti", System.IO.Path.Combine(PercorsiUtente.WslHome,"progetti"), Contenuti.Ico.Linux);
        Posto("Home Linux", PercorsiUtente.WslHome, Contenuti.Ico.Linux);
        laterale.Children.Add(TitoloLato("Dischi"));
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                var nome = (string.IsNullOrWhiteSpace(d.VolumeLabel) ? d.DriveType == DriveType.Network ? "Rete" : "Disco" : d.VolumeLabel) + $" ({d.Name.TrimEnd('\\')})";
                double pieno = d.TotalSize > 0 ? 1 - d.AvailableFreeSpace / (double)d.TotalSize : 0;
                laterale.Children.Add(VoceLato(nome, d.RootDirectory.FullName, Shell.Icona(d.RootDirectory.FullName, 32), pieno, $"{Dimensione(d.AvailableFreeSpace)} liberi su {Dimensione(d.TotalSize)}"));
            }
            catch { }
        }
    }

    static TextBlock TitoloLato(string t)
    {
        var s = Tema.Scritta(t, 11.5, Tema.BiancoTenue, FontWeights.SemiBold);
        s.Margin = new Thickness(10, 16, 0, 6);
        return s;
    }

    FrameworkElement VoceLato(string nome, string p, ImageSource ic, double pieno = -1, string sugg = null)
    {
        var g = new Grid { Margin = new Thickness(10, 0, 8, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var img = new Image { Source = ic, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        g.Children.Add(img);
        var testo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var n = Tema.Scritta(nome, 12.5);
        n.TextTrimming = TextTrimming.CharacterEllipsis;
        testo.Children.Add(n);
        if (pieno >= 0)
        {
            // occupazione del disco: binario sottile color ghiaccio, arancione se quasi pieno
            var bin = new Grid { Height = 3, Margin = new Thickness(0, 5, 0, 0) };
            bin.Children.Add(new Border { Background = Tema.Pennello(255, 255, 255, .16) });
            bin.Children.Add(new Border { Background = pieno > .9 ? Tema.Pennello(255, 170, 90, 1) : Tema.Pennello(190, 218, 236, 1), HorizontalAlignment = HorizontalAlignment.Left, Width = Math.Max(2, 150 * pieno) });
            testo.Children.Add(bin);
        }
        Grid.SetColumn(testo, 1);
        g.Children.Add(testo);
        var t = new Tasto(g, double.NaN, pieno >= 0 ? 42 : 34) { Margin = new Thickness(0, 0, 0, 1), Sinistro = () => Vai(p), Tag = p };
        t.Centrale = () => Apri(p);
        if (sugg != null) t.ToolTip = Barra.Suggerimento(sugg);
        return t;
    }

    void SegnaLato()
    {
        foreach (var t in laterale.Children.OfType<Tasto>())
            t.Acceso = t.Tag is string p && string.Equals(p.TrimEnd('\\'), cartella?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }

    // ---------- navigazione ----------

    void Vai(string p, bool storia = true)
    {
        if (string.IsNullOrWhiteSpace(p)) return;
        if (p.Length == 2 && p[1] == ':') p += "\\";
        if (File.Exists(p) && !Directory.Exists(p)) { ApriFile(p); return; }
        if (storia && cartella != null && !string.Equals(cartella, p, StringComparison.OrdinalIgnoreCase)) { indietro.Add(cartella); avanti.Clear(); }
        cartella = p;
        Title = NomeDi(p);
        filtro.Text = "";
        Briciole();
        SegnaLato();
        Guarda();
        Carica(true);
    }

    void Indietro()
    {
        if (indietro.Count == 0) return;
        avanti.Add(cartella);
        var p = indietro[^1];
        indietro.RemoveAt(indietro.Count - 1);
        Vai(p, false);
    }

    void Avanti()
    {
        if (avanti.Count == 0) return;
        indietro.Add(cartella);
        var p = avanti[^1];
        avanti.RemoveAt(avanti.Count - 1);
        Vai(p, false);
    }

    void Su()
    {
        var padre = Padre(cartella);
        if (padre == null) return;
        var da = cartella;
        Vai(padre);
        selezionaDopo = da;
    }

    static string Padre(string p)
    {
        try { return Directory.GetParent(p.TrimEnd('\\') + (p.EndsWith(":\\") ? "\\" : ""))?.FullName; } catch { return null; }
    }

    static string NomeDi(string p)
    {
        var t = p.TrimEnd('\\');
        if (string.Equals(t, Casa, StringComparison.OrdinalIgnoreCase)) return "Home";
        if (t.Length == 2 && t[1] == ':') return "Disco (" + t + ")";
        var n = Path.GetFileName(t);
        return string.IsNullOrEmpty(n) ? t : n;
    }

    // Percorso a pezzi: ogni pezzo porta lì; la home e la home di Linux si accorciano.
    void Briciole()
    {
        briciole.Children.Clear();
        var pezzi = new List<(string nome, string p)>();
        string q = cartella.TrimEnd('\\');
        string tetto = null, nomeTetto = null;
        if (q.StartsWith(Casa, StringComparison.OrdinalIgnoreCase)) { tetto = Casa; nomeTetto = "Home"; }
        else if (q.StartsWith(PercorsiUtente.WslHome, StringComparison.OrdinalIgnoreCase)) { tetto = PercorsiUtente.WslHome; nomeTetto = "Linux ~"; }
        else if (q.StartsWith(Wsl, StringComparison.OrdinalIgnoreCase)) { tetto = Wsl; nomeTetto = "Linux"; }
        while (q != null)
        {
            if (tetto != null && string.Equals(q, tetto, StringComparison.OrdinalIgnoreCase)) { pezzi.Add((nomeTetto, tetto)); break; }
            var n = Path.GetFileName(q);
            if (string.IsNullOrEmpty(n)) { pezzi.Add((q.Length == 2 && q[1] == ':' ? q : q.TrimStart('\\'), q.Length == 2 ? q + "\\" : q)); break; }
            pezzi.Add((n, q));
            var padre = Padre(q);
            q = padre?.TrimEnd('\\');
        }
        pezzi.Reverse();
        for (int i = 0; i < pezzi.Count; i++)
        {
            var (nome, p) = pezzi[i];
            if (i > 0)
            {
                var c = Tema.Glifo("\uE76C", 8, Tema.BiancoTenue);
                c.Margin = new Thickness(2, 1, 2, 0);
                briciole.Children.Add(c);
            }
            var s = Tema.Scritta(nome, 13, i == pezzi.Count - 1 ? Tema.Bianco : Tema.BiancoTenue, i == pezzi.Count - 1 ? FontWeights.SemiBold : FontWeights.Normal);
            s.Margin = new Thickness(8, 0, 8, 1);
            var pp = p;
            briciole.Children.Add(new Tasto(s, double.NaN, 26) { Margin = new Thickness(0), Sinistro = () => Vai(pp) });
        }
        Dispatcher.BeginInvoke(() => (briciole.Parent as ScrollViewer)?.ScrollToRightEnd(), DispatcherPriority.Loaded);
        tIndietro.Opacity = indietro.Count > 0 ? 1 : .4;
        tAvanti.Opacity = avanti.Count > 0 ? 1 : .4;
        tSu.Opacity = Padre(cartella) != null ? 1 : .4;
    }

    void ScriviPercorso()
    {
        campoPercorso.Text = cartella;
        campoPercorso.Visibility = Visibility.Visible;
        briciole.Visibility = Visibility.Hidden;
        campoPercorso.Focus();
        campoPercorso.SelectAll();
    }

    void FinePercorso()
    {
        campoPercorso.Visibility = Visibility.Collapsed;
        briciole.Visibility = Visibility.Visible;
    }

    // Ricarica quando nella cartella cambia qualcosa (anche da fuori), con un attimo di attesa per i cambi a raffica.
    void Guarda()
    {
        guardia?.Dispose();
        guardia = null;
        try
        {
            guardia = new FileSystemWatcher(cartella) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size };
            FileSystemEventHandler h = (_, _) => Dispatcher.BeginInvoke(Rinfresca);
            guardia.Created += h; guardia.Deleted += h; guardia.Changed += h;
            guardia.Renamed += (_, _) => Dispatcher.BeginInvoke(Rinfresca);
            guardia.EnableRaisingEvents = true;
        }
        catch { }
    }

    void Rinfresca()
    {
        rinfresco ??= new DispatcherTimer(TimeSpan.FromMilliseconds(350), DispatcherPriority.Background, (_, _) => { rinfresco.Stop(); Carica(); }, Dispatcher);
        rinfresco.Stop();
        rinfresco.Start();
    }

    string selezionaDopo;

    // La lettura della cartella va in sottofondo: le cartelle di Linux e di rete possono essere lente.
    async void Carica(bool nuova = false)
    {
        int mio = ++caricamento;
        var p = cartella;
        var scelti = lista.SelectedItems.OfType<Elemento>().Select(x => x.Percorso).ToHashSet(StringComparer.OrdinalIgnoreCase);
        double scroll = nuova ? 0 : Scorrimento()?.VerticalOffset ?? 0;
        if (nuova) { vuoto.Text = ""; lista.Opacity = .5; }
        List<Elemento> l;
        string errore = null;
        try { l = await Task.Run(() => Leggi(p)); }
        catch (Exception e) { l = new List<Elemento>(); errore = e is UnauthorizedAccessException ? "Accesso negato" : e is DirectoryNotFoundException ? "Cartella non trovata" : e.Message; }
        if (mio != caricamento) return;
        lista.Opacity = 1;
        voci = l;
        foreach (var x in voci) Completa(x);
        vista = new ListCollectionView(voci) { CustomSort = new Ordine(ordine, crescente), Filter = o => filtro.Text.Length == 0 || ((Elemento)o).Nome.Contains(filtro.Text, StringComparison.OrdinalIgnoreCase) };
        lista.ItemsSource = vista;
        vuoto.Text = errore ?? (voci.Count == 0 ? "Cartella vuota" : "");
        if (selezionaDopo != null) { scelti = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { selezionaDopo }; selezionaDopo = null; }
        foreach (var x in voci.Where(x => scelti.Contains(x.Percorso))) lista.SelectedItems.Add(x);
        if (lista.SelectedItem != null) lista.ScrollIntoView(lista.SelectedItem);
        else if (!nuova) Dispatcher.BeginInvoke(() => Scorrimento()?.ScrollToVerticalOffset(scroll), DispatcherPriority.Loaded);
        if (nuova) Entra();
        Stato();
        if (rinomina != null) { var r = voci.FirstOrDefault(x => string.Equals(x.Percorso, rinomina, StringComparison.OrdinalIgnoreCase)); rinomina = null; if (r != null) Rinomina(r); }
    }

    // Entrata dell'elenco nuovo: sale appena e sfuma (solo trasformazione e opacità).
    void Entra()
    {
        var tr = new TranslateTransform(0, 10);
        lista.RenderTransform = tr;
        var e = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        tr.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = e });
        lista.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(.2, 1, TimeSpan.FromMilliseconds(200)));
    }

    ScrollViewer Scorrimento() => VisualTreeHelper.GetChildrenCount(lista) > 0 && VisualTreeHelper.GetChild(lista, 0) is Border b ? b.Child as ScrollViewer : null;

    static List<Elemento> Leggi(string p)
    {
        var l = new List<Elemento>();
        foreach (var f in new DirectoryInfo(p).EnumerateFileSystemInfos())
        {
            try
            {
                var att = f.Attributes;
                bool nascosto = (att & FileAttributes.Hidden) != 0 || (att & FileAttributes.System) != 0;
                if ((att & FileAttributes.System) != 0 && (att & FileAttributes.Hidden) != 0) continue;
                bool dir = (att & FileAttributes.Directory) != 0;
                l.Add(new Elemento
                {
                    Nome = f.Name,
                    Percorso = f.FullName,
                    Cartella = dir,
                    Data = f.LastWriteTime,
                    Dim = dir ? -1 : ((FileInfo)f).Length,
                    Opacita = nascosto || f.Name.StartsWith(".") ? .55 : 1,
                });
            }
            catch { }
        }
        return l;
    }

    static readonly Dictionary<string, string> tipi = new(StringComparer.OrdinalIgnoreCase);
    static readonly System.Globalization.CultureInfo It = System.Globalization.CultureInfo.GetCultureInfo("it-IT");

    static void Completa(Elemento x)
    {
        x.Tipo = x.Cartella ? "Cartella" : TipoDi(Path.GetExtension(x.Nome));
        x.DimTesto = x.Cartella ? "" : Dimensione(x.Dim);
        var d = x.Data;
        x.DataTesto = d.Date == DateTime.Today ? "oggi " + d.ToString("HH:mm") : d.Date == DateTime.Today.AddDays(-1) ? "ieri " + d.ToString("HH:mm") : d.ToString(d.Year == DateTime.Today.Year ? "d MMM HH:mm" : "d MMM yyyy", It).Replace(".", "");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO { public IntPtr hIcon; public int iIcon; public uint attr; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string nome; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string tipo; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string p, uint att, ref SHFILEINFO i, uint dim, uint flag);

    // Il nome del tipo come lo scrive Windows ("Documento di testo"), uno per estensione.
    static string TipoDi(string est)
    {
        if (string.IsNullOrEmpty(est)) return "File";
        if (tipi.TryGetValue(est, out var t)) return t;
        var i = new SHFILEINFO();
        SHGetFileInfo("x" + est, 0x80, ref i, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x400 | 0x10);
        t = string.IsNullOrWhiteSpace(i.tipo) ? est.TrimStart('.').ToUpperInvariant() + " file" : i.tipo;
        tipi[est] = t;
        return t;
    }

    public static string Dimensione(long b)
    {
        if (b < 0) return "";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b;
        int k = 0;
        while (v >= 1024 && k < u.Length - 1) { v /= 1024; k++; }
        return (k == 0 ? v.ToString("0") : v.ToString(v < 10 ? "0.#" : "0", It)) + " " + u[k];
    }

    // Icone: una per estensione per i file comuni, una per file per eseguibili, collegamenti e immagini (anteprima vera).
    static readonly Dictionary<string, ImageSource> perEstensione = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> proprie = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".lnk", ".ico", ".url", ".appref-ms", ".msc", ".cpl", ".scr", ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg" };

    public static void ChiediIcona(Elemento x)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var est = Path.GetExtension(x.Nome);
            if (!x.Cartella && !proprie.Contains(est) && perEstensione.TryGetValue(est, out var c)) { x.Icona = c; return; }
            // le icone delle cartelle sfogliate non restano in memoria: per i file comuni basta una per estensione
            var i = Shell.Icona(x.Percorso, 32, false);
            if (!x.Cartella && !proprie.Contains(est) && i != null) perEstensione[est] = i;
            x.Icona = i;
        }, DispatcherPriority.Background);
    }

    class Ordine : IComparer
    {
        readonly string campo;
        readonly int verso;
        public Ordine(string c, bool cresc) { campo = c; verso = cresc ? 1 : -1; }
        public int Compare(object a, object b)
        {
            var x = (Elemento)a; var y = (Elemento)b;
            if (x.Cartella != y.Cartella) return x.Cartella ? -1 : 1;
            int r = campo switch
            {
                "Data" => x.Data.CompareTo(y.Data),
                "Tipo" => string.Compare(x.Tipo, y.Tipo, StringComparison.CurrentCultureIgnoreCase),
                "Dim" => x.Dim.CompareTo(y.Dim),
                _ => 0,
            };
            if (r == 0) r = StrCmpLogicalW(x.Nome, y.Nome);
            return r * verso;
        }
    }

    // ordine "naturale" come Windows: file2 prima di file10
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] static extern int StrCmpLogicalW(string a, string b);

    Grid Colonne(Thickness m)
    {
        var g = new Grid { Margin = m };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
        (string t, string c)[] cc = { ("Nome", "Nome"), ("Modificato", "Data"), ("Tipo", "Tipo"), ("Dimensione", "Dim") };
        for (int i = 0; i < cc.Length; i++)
        {
            var (t, c) = cc[i];
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(i == 0 ? 30 : 0, 0, 0, 0), HorizontalAlignment = i == 3 ? HorizontalAlignment.Right : HorizontalAlignment.Left };
            sp.Children.Add(Tema.Scritta(t, 11.5, Tema.BiancoTenue, FontWeights.SemiBold));
            var freccia = Tema.Glifo(c == ordine ? (crescente ? "\uE70E" : "\uE70D") : "", 8, Tema.BiancoTenue);
            freccia.Margin = new Thickness(6, 1, 0, 0);
            sp.Children.Add(freccia);
            var tasto = new Tasto(sp, double.NaN, 26) { Margin = new Thickness(0), HorizontalAlignment = i == 3 ? HorizontalAlignment.Right : HorizontalAlignment.Stretch };
            var cc2 = c;
            tasto.Sinistro = () =>
            {
                crescente = ordine == cc2 ? !crescente : cc2 != "Data" && cc2 != "Dim";
                ordine = cc2;
                if (vista != null) vista.CustomSort = new Ordine(ordine, crescente);
                foreach (var tt in g.Children.OfType<Tasto>())
                    if (tt.Child is StackPanel s && s.Children[1] is TextBlock f) f.Text = "";
                freccia.Text = crescente ? "\uE70E" : "\uE70D";
            };
            Grid.SetColumn(tasto, i);
            g.Children.Add(tasto);
        }
        return g;
    }

    const string StileRiga = @"
<Style TargetType='ListBoxItem' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
  <Setter Property='Foreground' Value='White'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ListBoxItem'>
        <Grid Height='32' Margin='0,0,0,1' Background='Transparent'>
          <Border x:Name='fondo' Background='White' Opacity='0' Margin='1,0,1,0'/>
          <Border x:Name='fondo2' Background='White' Opacity='0' Margin='0,1,0,1'/>
          <Rectangle x:Name='segno' Fill='#D8EAF6' Width='2' HorizontalAlignment='Left' Margin='0,8,0,8' Visibility='Collapsed'/>
          <ContentPresenter/>
        </Grid>
        <ControlTemplate.Triggers>
          <Trigger Property='IsMouseOver' Value='True'>
            <Setter TargetName='fondo' Property='Opacity' Value='0.08'/>
            <Setter TargetName='fondo2' Property='Opacity' Value='0.08'/>
          </Trigger>
          <Trigger Property='IsSelected' Value='True'>
            <Setter TargetName='fondo' Property='Opacity' Value='0.10'/>
            <Setter TargetName='fondo2' Property='Opacity' Value='0.10'/>
            <Setter TargetName='segno' Property='Visibility' Value='Visible'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

    const string ModelloRiga = @"
<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
  <Grid Margin='12,0,16,0' Opacity='{Binding Opacita}'>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width='30'/>
      <ColumnDefinition Width='*'/>
      <ColumnDefinition Width='140'/>
      <ColumnDefinition Width='170'/>
      <ColumnDefinition Width='80'/>
    </Grid.ColumnDefinitions>
    <Image Source='{Binding Icona}' Width='18' Height='18' HorizontalAlignment='Left' RenderOptions.BitmapScalingMode='HighQuality'/>
    <TextBlock Grid.Column='1' Text='{Binding Nome}' FontSize='13' VerticalAlignment='Center' TextTrimming='CharacterEllipsis' Margin='0,0,12,1'/>
    <TextBlock Grid.Column='2' Text='{Binding DataTesto}' FontSize='12' Opacity='0.62' VerticalAlignment='Center' Margin='8,0,0,1'/>
    <TextBlock Grid.Column='3' Text='{Binding Tipo}' FontSize='12' Opacity='0.62' VerticalAlignment='Center' TextTrimming='CharacterEllipsis' Margin='8,0,8,1'/>
    <TextBlock Grid.Column='4' Text='{Binding DimTesto}' FontSize='12' Opacity='0.62' VerticalAlignment='Center' HorizontalAlignment='Right' Margin='0,0,0,1'/>
  </Grid>
</DataTemplate>";

    Elemento Sotto(MouseEventArgs e)
    {
        var d = e.OriginalSource as DependencyObject;
        while (d != null && d is not ListBoxItem) d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        return (d as ListBoxItem)?.DataContext as Elemento;
    }

    List<Elemento> Scelti() => lista.SelectedItems.OfType<Elemento>().ToList();

    void Stato()
    {
        int n = vista?.Count ?? 0, s = lista.SelectedItems.Count;
        var t = n == 1 ? "1 elemento" : $"{n} elementi";
        if (s > 0)
        {
            long dim = Scelti().Where(x => !x.Cartella).Sum(x => x.Dim);
            t += s == 1 ? " · 1 selezionato" : $" · {s} selezionati";
            if (dim > 0) t += " · " + Dimensione(dim);
        }
        stato.Text = t;
    }

    // ---------- aprire ----------

    void ApriVoce(Elemento x)
    {
        if (x.Cartella) { Vai(x.Percorso); return; }
        ApriFile(x.Percorso);
    }

    void ApriFile(string p)
    {
        // un collegamento a una cartella si apre qui dentro
        if (p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var d = Shell.DestinazioneLnk(p);
            if (d != null && Directory.Exists(d)) { Vai(d); return; }
        }
        try { Process.Start(new ProcessStartInfo(p) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(p) }); }
        catch (Exception e) { if (e is not Win32Exception { NativeErrorCode: 1223 }) ApriCon(p); }
    }

    static void ApriCon(string p)
    {
        try { Process.Start("rundll32.exe", "shell32.dll,OpenAs_RunDLL " + p); } catch { }
    }

    // Linux: \\wsl.localhost\Ubuntu\home\x → /home/x
    static string Linux(string p)
    {
        foreach (var r in new[] { Wsl, PercorsiUtente.WslLegacyRoot })
            if (p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return "/" + p[r.Length..].TrimStart('\\').Replace('\\', '/');
        return null;
    }

    static void Terminale(string p)
    {
        var l = Linux(p);
        try
        {
            if (l != null) Process.Start(new ProcessStartInfo("wt.exe", $"wsl.exe -d Ubuntu --cd \"{l}\"") { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo("wt.exe", $"-d \"{p}\"") { UseShellExecute = true });
        }
        catch (Exception e) { App.Log("terminale: " + e.Message); }
    }

    static void Code(string p)
    {
        var l = Linux(p);
        var arg = l != null ? $"--remote wsl+Ubuntu \"{l}\"" : $"\"{p}\"";
        try { Process.Start(new ProcessStartInfo("cmd.exe", "/c code " + arg) { CreateNoWindow = true, UseShellExecute = false }); }
        catch (Exception e) { App.Log("code: " + e.Message); }
    }

    // ---------- operazioni sui file (con le finestre di avanzamento e il cestino di Windows) ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
        [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref SHFILEOPSTRUCT op);

    void Operazione(uint tipo, IEnumerable<string> da, string a, ushort flag)
    {
        var h = hwnd;
        var from = string.Join("\0", da) + "\0";
        var to = a == null ? null : a + "\0";
        var t = new Thread(() =>
        {
            var op = new SHFILEOPSTRUCT { hwnd = h, wFunc = tipo, pFrom = from, pTo = to, fFlags = flag };
            int r = SHFileOperation(ref op);
            if (r != 0 && r != 0x4C7) App.Log($"operazione file {tipo}: 0x{r:X}");
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    const uint FO_MOVE = 1, FO_COPY = 2, FO_DELETE = 3;
    const ushort ANNULLABILE = 0x40, RINOMINA_SE_C_E = 0x8, SENZA_CONFERMA = 0x10, AVVISO_SE_DEFINITIVO = 0x4000;

    void Elimina(bool definitivo = false)
    {
        var s = Scelti();
        if (s.Count == 0) return;
        Operazione(FO_DELETE, s.Select(x => x.Percorso), null, (ushort)(definitivo ? 0 : ANNULLABILE | SENZA_CONFERMA | AVVISO_SE_DEFINITIVO));
    }

    void Copia(bool taglia)
    {
        var s = Scelti();
        if (s.Count == 0) return;
        var col = new StringCollection();
        foreach (var x in s) col.Add(x.Percorso);
        var d = new DataObject();
        d.SetFileDropList(col);
        d.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(taglia ? 2 : 5)));
        try { Clipboard.SetDataObject(d, true); } catch { }
    }

    void Incolla(string dove = null)
    {
        dove ??= cartella;
        try
        {
            var d = Clipboard.GetDataObject();
            if (d == null || !d.GetDataPresent(DataFormats.FileDrop)) return;
            var file = (string[])d.GetData(DataFormats.FileDrop);
            bool sposta = d.GetData("Preferred DropEffect") is MemoryStream m && m.Length >= 4 && (BitConverter.ToInt32(m.ToArray(), 0) & 2) != 0;
            Operazione(sposta ? FO_MOVE : FO_COPY, file, dove, (ushort)(ANNULLABILE | RINOMINA_SE_C_E | 0x200));
            if (sposta) Clipboard.Clear();
        }
        catch (Exception e) { App.Log("incolla: " + e.Message); }
    }

    bool PuoIncollare() { try { return Clipboard.ContainsFileDropList(); } catch { return false; } }

    string rinomina;

    void NuovaCartella()
    {
        var p = Libero(Path.Combine(cartella, "Nuova cartella"), "");
        try { Directory.CreateDirectory(p); rinomina = p; Carica(); } catch (Exception e) { App.Log("nuova cartella: " + e.Message); }
    }

    void NuovoFile()
    {
        var p = Libero(Path.Combine(cartella, "Nuovo file"), ".txt");
        try { File.WriteAllText(p, ""); rinomina = p; Carica(); } catch (Exception e) { App.Log("nuovo file: " + e.Message); }
    }

    static string Libero(string base0, string est)
    {
        var p = base0 + est;
        for (int i = 2; File.Exists(p) || Directory.Exists(p); i++) p = $"{base0} ({i}){est}";
        return p;
    }

    // Rinomina sul posto: un campo sopra il nome, col nome già scelto senza estensione.
    void Rinomina(Elemento x)
    {
        lista.SelectedItem = x;
        lista.ScrollIntoView(x);
        lista.UpdateLayout();
        if (lista.ItemContainerGenerator.ContainerFromItem(x) is not ListBoxItem riga) return;
        var campo = Campo(13);
        campo.Text = x.Nome;
        campo.Width = Math.Max(160, riga.ActualWidth - 30 - 12 - 140 - 170 - 80 - 40);
        var box = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Pennello(30, 44, 58, .98), Bordo = Tema.Pennello(225, 242, 255, .45), Child = new Border { Child = campo, Padding = new Thickness(6, 0, 6, 0) }, Height = 28 };
        var pop = new Popup { Child = box, PlacementTarget = riga, Placement = PlacementMode.Relative, HorizontalOffset = 12 + 30 - 7, VerticalOffset = 2, AllowsTransparency = true, StaysOpen = false };
        bool fatto = false;
        void Fine(bool applica)
        {
            if (fatto) return;
            fatto = true;
            pop.IsOpen = false;
            var nuovo = campo.Text.Trim();
            if (applica && nuovo.Length > 0 && nuovo != x.Nome)
            {
                var dest = Path.Combine(Path.GetDirectoryName(x.Percorso), nuovo);
                try
                {
                    if (x.Cartella) Directory.Move(x.Percorso, dest); else File.Move(x.Percorso, dest);
                    selezionaDopo = dest;
                    Carica();
                }
                catch (Exception e) { App.Log("rinomina: " + e.Message); }
            }
            lista.Focus();
        }
        campo.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Fine(true); e.Handled = true; }
            if (e.Key == Key.Escape) { Fine(false); e.Handled = true; }
        };
        pop.Closed += (_, _) => Fine(true);
        pop.IsOpen = true;
        campo.Focus();
        int punto = x.Cartella ? -1 : x.Nome.LastIndexOf('.');
        campo.Select(0, punto > 0 ? punto : x.Nome.Length);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHELLEXECUTEINFO
    {
        public int cbSize; public uint fMask; public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpDirectory;
        public int nShow; public IntPtr hInstApp, lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpClass;
        public IntPtr hkeyClass; public uint dwHotKey; public IntPtr hIcon, hProcess;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool ShellExecuteEx(ref SHELLEXECUTEINFO i);

    void Proprieta(string p)
    {
        var i = new SHELLEXECUTEINFO { cbSize = Marshal.SizeOf<SHELLEXECUTEINFO>(), fMask = 0xC, hwnd = hwnd, lpVerb = "properties", lpFile = p, nShow = 1 };
        ShellExecuteEx(ref i);
    }

    // ---------- trascinare ----------

    Point partenza;
    Elemento sotto;

    void Trascina(object s, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sotto == null) return;
        var d = e.GetPosition(lista) - partenza;
        if (Math.Abs(d.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var file = Scelti().Select(x => x.Percorso).ToArray();
        if (file.Length == 0) return;
        sotto = null;
        var dati = new DataObject(DataFormats.FileDrop, file);
        try { DragDrop.DoDragDrop(lista, dati, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link); } catch { }
    }

    string Destinazione(DragEventArgs e)
    {
        var d = e.OriginalSource as DependencyObject;
        while (d != null && d is not ListBoxItem) d = d is Visual ? VisualTreeHelper.GetParent(d) : null;
        return (d as ListBoxItem)?.DataContext is Elemento { Cartella: true } x ? x.Percorso : cartella;
    }

    // Come Windows: sullo stesso disco sposta, altrimenti copia; Ctrl copia sempre, Maiusc sposta sempre.
    static DragDropEffects Effetto(DragEventArgs e, string dest)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return DragDropEffects.None;
        var file = (string[])e.Data.GetData(DataFormats.FileDrop);
        if (file.Any(f => string.Equals(f, dest, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetDirectoryName(f), dest, StringComparison.OrdinalIgnoreCase) && (e.KeyStates & DragDropKeyStates.ControlKey) == 0)) return DragDropEffects.None;
        if ((e.KeyStates & DragDropKeyStates.ControlKey) != 0) return DragDropEffects.Copy;
        if ((e.KeyStates & DragDropKeyStates.ShiftKey) != 0) return DragDropEffects.Move;
        bool stesso = file.All(f => string.Equals(Path.GetPathRoot(f), Path.GetPathRoot(dest), StringComparison.OrdinalIgnoreCase));
        return stesso ? DragDropEffects.Move : DragDropEffects.Copy;
    }

    void Rilascia(object s, DragEventArgs e)
    {
        var dest = Destinazione(e);
        var eff = Effetto(e, dest);
        if (eff == DragDropEffects.None) return;
        var file = (string[])e.Data.GetData(DataFormats.FileDrop);
        Operazione(eff == DragDropEffects.Move ? FO_MOVE : FO_COPY, file, dest, (ushort)(ANNULLABILE | RINOMINA_SE_C_E | 0x200));
        e.Handled = true;
    }

    // ---------- tastiera ----------

    void Tasti(object s, KeyEventArgs e)
    {
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0, alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0, maiusc = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var k = e.Key == Key.System ? e.SystemKey : e.Key;
        bool inCampo = Keyboard.FocusedElement is TextBox;
        if (alt && k == Key.Left) { Indietro(); e.Handled = true; return; }
        if (alt && k == Key.Right) { Avanti(); e.Handled = true; return; }
        if (alt && k == Key.Up) { Su(); e.Handled = true; return; }
        if (ctrl && k == Key.L) { ScriviPercorso(); e.Handled = true; return; }
        if (ctrl && k == Key.F) { filtro.Focus(); filtro.SelectAll(); e.Handled = true; return; }
        if (ctrl && k == Key.W) { Close(); e.Handled = true; return; }
        if (ctrl && k == Key.N && !maiusc) { Apri(cartella); e.Handled = true; return; }
        if (ctrl && maiusc && k == Key.N) { NuovaCartella(); e.Handled = true; return; }
        if (k == Key.F5) { Carica(); e.Handled = true; return; }
        if (inCampo) return;
        if (k == Key.Back) { Su(); e.Handled = true; }
        else if (k == Key.Enter) { var x = Scelti(); if (x.Count == 1) ApriVoce(x[0]); else foreach (var f in x.Where(y => !y.Cartella)) ApriFile(f.Percorso); e.Handled = true; }
        else if (k == Key.F2) { if (lista.SelectedItem is Elemento x) Rinomina(x); e.Handled = true; }
        else if (k == Key.Delete) { Elimina(maiusc); e.Handled = true; }
        else if (ctrl && k == Key.C) { Copia(false); e.Handled = true; }
        else if (ctrl && k == Key.X) { Copia(true); e.Handled = true; }
        else if (ctrl && k == Key.V) { Incolla(); e.Handled = true; }
        else if (k == Key.Escape) { if (filtro.Text.Length > 0) filtro.Text = ""; else lista.SelectedItems.Clear(); e.Handled = true; }
    }

    // ---------- menu col tasto destro ----------

    void Menu(bool suVoci)
    {
        var col = new StackPanel { Width = 250, Margin = new Thickness(8) };
        var pop = new Popup { Placement = PlacementMode.MousePoint, AllowsTransparency = true, StaysOpen = false, PopupAnimation = PopupAnimation.None };
        void Voce(string glifo, string testo, string tasti, Action a, ImageSource ic = null)
        {
            var g = new Grid { Margin = new Thickness(8, 0, 10, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (ic != null) { var im = new Image { Source = ic, Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left }; RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality); g.Children.Add(im); }
            else { var gl = Tema.Glifo(glifo, 13); gl.HorizontalAlignment = HorizontalAlignment.Left; g.Children.Add(gl); }
            var t = Tema.Scritta(testo, 12.5);
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            if (tasti != null) { var k = Tema.Scritta(tasti, 11, Tema.BiancoTenue); Grid.SetColumn(k, 2); g.Children.Add(k); }
            col.Children.Add(new Tasto(g, double.NaN, 32) { Margin = new Thickness(0), Sinistro = () => { pop.IsOpen = false; a(); } });
        }
        void Linea() => col.Children.Add(new Border { Height = 1, Background = Tema.Separatore, Margin = new Thickness(8, 5, 8, 5) });

        var s = Scelti();
        if (suVoci && s.Count > 0)
        {
            var primo = s[0];
            Voce("\uE8E5", "Apri", "Invio", () => ApriVoce(primo));
            if (!primo.Cartella && s.Count == 1) Voce("\uE7AC", "Apri con…", null, () => ApriCon(primo.Percorso));
            if (primo.Cartella && s.Count == 1) Voce("\uE8A7", "Apri in una nuova finestra", null, () => Apri(primo.Percorso));
            Linea();
            var dove = primo.Cartella && s.Count == 1 ? primo.Percorso : cartella;
            Voce("\uE756", "Apri nel terminale", null, () => Terminale(dove), Contenuti.Ico.Terminale);
            Voce("\uE943", "Apri in VS Code", null, () => Code(s.Count == 1 ? primo.Percorso : cartella), IconaCode());
            Linea();
            Voce("\uE8C6", "Taglia", "Ctrl+X", () => Copia(true));
            Voce("\uE8C8", "Copia", "Ctrl+C", () => Copia(false));
            Voce("\uE71B", "Copia percorso", null, () => { try { Clipboard.SetText(string.Join(Environment.NewLine, s.Select(x => x.Percorso))); } catch { } });
            if (primo.Cartella && s.Count == 1 && PuoIncollare()) Voce("\uE77F", "Incolla dentro", null, () => Incolla(primo.Percorso));
            Linea();
            if (s.Count == 1) Voce("\uE8AC", "Rinomina", "F2", () => Rinomina(primo));
            Voce("\uE74D", "Elimina", "Canc", () => Elimina());
            Linea();
            Voce("\uEC50", "Mostra in Esplora di Windows", null, () => Process.Start("explorer.exe", $"/select,\"{primo.Percorso}\""), Contenuti.Ico.Esplora);
            Voce("\uE946", "Proprietà", null, () => Proprieta(primo.Percorso));
        }
        else
        {
            Voce("\uE8F4", "Nuova cartella", "Ctrl+Maiusc+N", NuovaCartella);
            Voce("\uE8A5", "Nuovo file di testo", null, NuovoFile);
            if (PuoIncollare()) Voce("\uE77F", "Incolla", "Ctrl+V", () => Incolla());
            Voce("\uE72C", "Aggiorna", "F5", () => Carica());
            Linea();
            Voce("\uE756", "Apri nel terminale", null, () => Terminale(cartella), Contenuti.Ico.Terminale);
            Voce("\uE943", "Apri in VS Code", null, () => Code(cartella), IconaCode());
            Voce("\uE71B", "Copia percorso", null, () => { try { Clipboard.SetText(cartella); } catch { } });
            Linea();
            Voce("\uEC50", "Apri in Esplora di Windows", null, () => Process.Start("explorer.exe", $"\"{cartella}\""), Contenuti.Ico.Esplora);
            Voce("\uE946", "Proprietà", null, () => Proprieta(cartella));
        }
        var cornice = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.Pennello(30, 42, 54, .97), Bordo = Tema.Bordo, Child = col };
        // il menu nasce piccolo dal punto del clic e si apre (solo scala e opacità)
        var sc = new ScaleTransform(.94, .94);
        cornice.RenderTransform = sc;
        cornice.Opacity = 0;
        pop.Child = cornice;
        pop.Opened += (_, _) =>
        {
            var e = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            sc.BeginAnimation(ScaleTransform.ScaleXProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = e });
            sc.BeginAnimation(ScaleTransform.ScaleYProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(160)) { EasingFunction = e });
            cornice.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        };
        pop.IsOpen = true;
    }

    static ImageSource IconaCode()
    {
        var p = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe");
        return File.Exists(p) ? Shell.Icona(p, 32) : null;
    }
}
