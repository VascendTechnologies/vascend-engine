using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace BarraVascend;

// Cursore a pixel per il volume: binario sottile, riempimento lime, maniglia quadrata.
class Cursore : FrameworkElement
{
    public double Valore { get => valore; set { valore = Math.Clamp(value, 0, 1); InvalidateVisual(); } }
    double valore;
    public event Action<double> Cambiato;

    public Cursore() { Height = 22; Cursor = System.Windows.Input.Cursors.Hand; }

    protected override void OnRender(DrawingContext dc)
    {
        double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
        double w = ActualWidth, cy = Math.Round(ActualHeight / 2 / u) * u;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, ActualHeight));
        double maniglia = u * 5, utile = w - maniglia, x = Math.Round(utile * valore / u) * u;
        dc.DrawRectangle(Tema.Pennello(255, 255, 255, .22), null, new Rect(0, cy - u, w, u * 2));
        dc.DrawRectangle(Tema.LimePennello, null, new Rect(0, cy - u, x + maniglia / 2, u * 2));
        dc.DrawRectangle(Tema.Bianco, null, new Rect(x + u, cy - maniglia / 2, maniglia - 2 * u, maniglia));
        dc.DrawRectangle(Tema.Bianco, null, new Rect(x, cy - maniglia / 2 + u, maniglia, maniglia - 2 * u));
    }

    void Da(System.Windows.Input.MouseEventArgs e)
    {
        double u = Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;
        Valore = (e.GetPosition(this).X - u * 2.5) / Math.Max(1, ActualWidth - u * 5);
        Cambiato?.Invoke(valore);
    }

    protected override void OnMouseLeftButtonDown(System.Windows.Input.MouseButtonEventArgs e) { CaptureMouse(); Da(e); e.Handled = true; }
    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e) { if (IsMouseCaptured) Da(e); }
    protected override void OnMouseLeftButtonUp(System.Windows.Input.MouseButtonEventArgs e) => ReleaseMouseCapture();
    protected override void OnMouseWheel(System.Windows.Input.MouseWheelEventArgs e) { Valore += e.Delta > 0 ? .02 : -.02; Cambiato?.Invoke(valore); e.Handled = true; }
}

static class Contenuti
{
    static void Chiudi() => App.Io.Pannello?.Chiudi();

    static void Apri(string cosa, string arg = null)
    {
        try { Process.Start(new ProcessStartInfo(cosa) { UseShellExecute = true, Arguments = arg ?? "" }); }
        catch (Exception e) { App.Log("apri " + cosa + ": " + e.Message); }
    }

    static StackPanel Colonna(double larghezza) => new() { Width = larghezza, Margin = new Thickness(12) };

    static TextBlock Titolo(string t)
    {
        var s = Tema.Scritta(t, 12, Tema.BiancoTenue, FontWeights.SemiBold);
        s.Margin = new Thickness(6, 2, 0, 8);
        return s;
    }

    // Icone vere e colorate delle cose che le righe aprono, come nel menu Start di Windows.
    public static class Ico
    {
        static string Sys(string f) => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), f);
        public static ImageSource Cartella(string p) => Shell.Icona(p, 48);
        public static ImageSource Esplora => Shell.Icona(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), 48);
        public static ImageSource Linux => Shell.Icona(Sys("wsl.exe"), 48);
        public static ImageSource Terminale => Shell.Icona(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", 48) ?? Shell.Icona(Sys("cmd.exe"), 48);
        public static ImageSource Attivita => Shell.Icona(Sys("Taskmgr.exe"), 48);
        public static ImageSource Impostazioni => Shell.Icona(@"shell:AppsFolder\windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel", 48);
        public static ImageSource Calcolatrice => Shell.Icona(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", 48);
        public static ImageSource Browser => Shell.Icona(BrowserExe() ?? "", 48);

        // Il browser predefinito, dall'associazione di http dell'utente.
        static string browser;
        static string BrowserExe()
        {
            if (browser != null) return browser;
            try
            {
                using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice");
                var prog = k?.GetValue("ProgId") as string;
                using var c = Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(prog + @"\shell\open\command");
                var cmd = c?.GetValue("") as string ?? "";
                var m = System.Text.RegularExpressions.Regex.Match(cmd, "^\"([^\"]+)\"|^(\\S+)");
                browser = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            }
            catch { browser = ""; }
            return browser;
        }
    }

    static FrameworkElement Riga(string glifo, string testo, Action azione, string destra = null, bool acceso = false, bool chiudi = true, ImageSource icona = null)
    {
        var g = new Grid { Margin = new Thickness(8, 0, 10, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (icona != null)
        {
            var im = new Image { Source = icona, Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Left };
            RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
            g.Children.Add(im);
        }
        else if (glifo != null)
        {
            var i = Tema.Glifo(glifo, 14, acceso ? Tema.LimePennello : null);
            i.HorizontalAlignment = HorizontalAlignment.Left;
            g.Children.Add(i);
        }
        var s = Tema.Scritta(testo, 13, acceso ? Tema.LimePennello : null);
        s.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(s, 1);
        g.Children.Add(s);
        if (destra != null)
        {
            var d = Tema.Scritta(destra, 12, Tema.BiancoTenue);
            d.Margin = new Thickness(10, 0, 0, 0);
            Grid.SetColumn(d, 2);
            g.Children.Add(d);
        }
        var t = new Tasto(g, double.NaN, 34) { Margin = new Thickness(0) };
        t.HorizontalAlignment = HorizontalAlignment.Stretch;
        t.Sinistro = () => { if (chiudi) Chiudi(); azione?.Invoke(); };
        return t;
    }

    static FrameworkElement Linea() => new Border { Height = 1, Background = Tema.Separatore, Margin = new Thickness(8, 6, 8, 6) };

    public static UIElement Tray()
    {
        var icone = App.Io.Tray.Visibili().ToList();
        if (icone.Count == 0) return null;
        var griglia = new UniformGrid { Columns = Math.Min(5, icone.Count), Margin = new Thickness(10) };
        foreach (var i in icone)
        {
            var img = new Image { Source = i.Immagine, Width = 18, Height = 18 };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            var t = new Tasto(img, 40, 40) { Margin = new Thickness(1) };
            var icona = i;
            t.Sinistro = () => { icona.Clic(0x201); icona.Clic(0x202); };
            t.Destro = () => { icona.Clic(0x204); icona.Clic(0x205); };
            t.Centrale = () => { icona.Clic(0x207); icona.Clic(0x208); };
            t.Doppio = () => icona.Clic(0x203);
            if (!string.IsNullOrWhiteSpace(i.Suggerimento)) t.ToolTip = Barra.Suggerimento(i.Suggerimento);
            griglia.Children.Add(t);
        }
        return griglia;
    }

    public static UIElement Rete()
    {
        var c = Colonna(300);
        c.Children.Add(Titolo("Rete"));
        var card = new Grid { Margin = new Thickness(8, 0, 8, 6) };
        card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        card.ColumnDefinitions.Add(new ColumnDefinition());
        var icon=Tema.Glifo(BarraVascend.Rete.Glifo(),22,Tema.LimePennello);card.Children.Add(icon);
        var testi=new StackPanel {VerticalAlignment=VerticalAlignment.Center};
        var nome=Tema.Scritta("",14,null,FontWeights.SemiBold);testi.Children.Add(nome);
        var dettaglio=Tema.Scritta("",11.5,Tema.BiancoTenue);dettaglio.TextWrapping=TextWrapping.Wrap;testi.Children.Add(dettaglio);
        void AggiornaRete()
        {
            string stato=BarraVascend.Rete.Tipo switch {"ethernet"=>"Ethernet","wifi"=>"Wi-Fi",_=>"Non connesso"};
            nome.Text=BarraVascend.Rete.Tipo == "nessuna" ? stato : BarraVascend.Rete.Nome;
            icon.Text=BarraVascend.Rete.Glifo();
            string segnale=BarraVascend.Rete.Tipo == "wifi" ? (BarraVascend.Rete.SegnaleDisponibile ? $" · {BarraVascend.Rete.Segnale}%" : " · segnale non disponibile") : "";
            dettaglio.Text=BarraVascend.Rete.Tipo == "nessuna" ? "" : stato+segnale+(BarraVascend.Rete.Ip.Length > 0 ? " · "+BarraVascend.Rete.Ip : "");
            dettaglio.Visibility=dettaglio.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        AggiornaRete();
        c.Loaded += (_,_)=>{BarraVascend.Rete.Cambiata+=AggiornaRete;BarraVascend.Rete.Aggiorna();};
        c.Unloaded += (_,_)=>BarraVascend.Rete.Cambiata-=AggiornaRete;
        Grid.SetColumn(testi, 1);
        card.Children.Add(testi);
        c.Children.Add(card);

        var reti = new StackPanel();
        c.Children.Add(reti);
        BarraVascend.Rete.Reti().ContinueWith(t =>
        {
            var elenco = t.Result;
            App.Io.Dispatcher.BeginInvoke(() =>
            {
                if (elenco.Count == 0) return;
                reti.Children.Add(Linea());
                reti.Children.Add(Titolo("Wi-Fi"));
                foreach (var w in elenco.Take(8))
                {
                    string g = w.Segnale >= 75 ? "\uE701" : w.Segnale >= 50 ? "\uE874" : w.Segnale >= 25 ? "\uE873" : "\uE872";
                    bool attuale = BarraVascend.Rete.Tipo == "wifi" && w.Ssid == BarraVascend.Rete.Nome;
                    var ssid = w.Ssid;
                    reti.Children.Add(Riga(g, w.Ssid, () => BarraVascend.Rete.Connetti(ssid), attuale ? "connessa" : (w.Sicura ? "\uD83D\uDD12" : null), attuale));
                }
            });
        });

        c.Children.Add(Linea());
        c.Children.Add(Riga("\uE713", "Impostazioni di rete", () => Apri("ms-settings:network"), icona: Ico.Impostazioni));
        return c;
    }

    public static UIElement Volume()
    {
        var c = Colonna(320);
        var testa = new Grid();
        testa.Children.Add(Titolo("Volume"));
        var perc = Tema.Scritta($"{Math.Round(Audio.Livello * 100)}%", 12, Tema.BiancoTenue, FontWeights.SemiBold);
        perc.HorizontalAlignment = HorizontalAlignment.Right;
        perc.Margin = new Thickness(0, 2, 8, 8);
        perc.VerticalAlignment = VerticalAlignment.Top;
        testa.Children.Add(perc);
        c.Children.Add(testa);

        var riga = new Grid { Margin = new Thickness(0, 0, 8, 4) };
        riga.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        riga.ColumnDefinitions.Add(new ColumnDefinition());
        var glifo = Tema.Glifo(Audio.Glifo(), 16);
        var muto = new Tasto(glifo, 36, 34);
        var cur = new Cursore { Valore = Audio.Livello, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        void Aggiorna()
        {
            glifo.Text = Audio.Glifo();
            perc.Text = Audio.Muto ? "muto" : $"{Math.Round(Audio.Livello * 100)}%";
        }
        muto.Sinistro = () => { Audio.Muto = !Audio.Muto; Aggiorna(); };
        cur.Cambiato += v => { Audio.Livello = (float)v; Aggiorna(); };
        Aggiorna();
        Grid.SetColumn(cur, 1);
        riga.Children.Add(muto);
        riga.Children.Add(cur);
        c.Children.Add(riga);

        var uscite = Audio.Uscite();
        if (uscite.Count > 1)
        {
            c.Children.Add(Linea());
            c.Children.Add(Titolo("Uscita"));
            foreach (var u in uscite)
            {
                var id = u.Id;
                c.Children.Add(Riga(u.Predefinita ? "\uE73E" : "\uE7F5", u.Nome, () => Audio.Predefinita(id), null, u.Predefinita));
            }
        }
        c.Children.Add(Linea());
        c.Children.Add(Riga("\uE9E9", "Mixer volume", () => Apri("ms-settings:apps-volume"), icona: Ico.Impostazioni));
        return c;
    }

    public static UIElement Batteria()
    {
        var c = Colonna(280);
        c.Children.Add(Titolo("Batteria"));
        var g = new Grid { Margin = new Thickness(8, 0, 8, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.Children.Add(Tema.Glifo(BarraVascend.Batteria.Glifo(), 26, Tema.LimePennello));
        var t = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        t.Children.Add(Tema.Scritta($"{BarraVascend.Batteria.Percento}%", 26, null, FontWeights.SemiBold));
        int sec = BarraVascend.Batteria.SecondiRimasti;
        string stato = BarraVascend.Batteria.InCarica ? (BarraVascend.Batteria.Percento >= 100 ? "Carica completa" : "In carica") : "A batteria";
        if (!BarraVascend.Batteria.InCarica && sec > 0) stato += $" · {sec / 3600} h {sec % 3600 / 60:00} min";
        t.Children.Add(Tema.Scritta(stato, 12, Tema.BiancoTenue));
        Grid.SetColumn(t, 1);
        g.Children.Add(t);
        c.Children.Add(g);
        c.Children.Add(Linea());
        c.Children.Add(Riga("\uEBB6", "Risparmio energia", () => Apri("ms-settings:batterysaver"), icona: Ico.Impostazioni));
        c.Children.Add(Riga("\uE7E8", "Alimentazione e sospensione", () => Apri("ms-settings:powersleep"), icona: Ico.Impostazioni));
        return c;
    }

    public static UIElement Calendario()
    {
        var it = CultureInfo.GetCultureInfo("it-IT");
        var c = Colonna(300);
        var ora = Tema.Scritta(DateTime.Now.ToString("HH:mm:ss"), 34, null, FontWeights.SemiBold);
        ora.Margin = new Thickness(6, 0, 0, 0);
        var oggi = Tema.Scritta(DateTime.Now.ToString("dddd d MMMM yyyy", it), 12.5, Tema.BiancoTenue);
        oggi.Margin = new Thickness(7, 0, 0, 10);
        c.Children.Add(ora);
        c.Children.Add(oggi);
        var tic = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        tic.Tick += (_, _) => ora.Text = DateTime.Now.ToString("HH:mm:ss");
        tic.Start();
        c.Unloaded += (_, _) => tic.Stop();

        var mese = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var testa = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        var nomeMese = Tema.Scritta("", 13.5, null, FontWeights.SemiBold);
        nomeMese.Margin = new Thickness(7, 0, 0, 0);
        testa.Children.Add(nomeMese);
        var frecce = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var griglia = new UniformGrid { Columns = 7 };
        void Disegna()
        {
            nomeMese.Text = it.TextInfo.ToTitleCase(mese.ToString("MMMM yyyy", it));
            griglia.Children.Clear();
            foreach (var g in new[] { "L", "M", "M", "G", "V", "S", "D" })
            {
                var s = Tema.Scritta(g, 11, Tema.BiancoTenue, FontWeights.SemiBold);
                s.HorizontalAlignment = HorizontalAlignment.Center;
                s.Margin = new Thickness(0, 4, 0, 4);
                griglia.Children.Add(s);
            }
            int vuoti = ((int)mese.DayOfWeek + 6) % 7;
            var inizio = mese.AddDays(-vuoti);
            for (int i = 0; i < 42; i++)
            {
                var d = inizio.AddDays(i);
                bool delMese = d.Month == mese.Month, eOggi = d == DateTime.Today;
                var s = Tema.Scritta(d.Day.ToString(), 12.5, eOggi ? Tema.Pennello(20, 28, 36, 1) : delMese ? Tema.Bianco : Tema.Pennello(255, 255, 255, .32), eOggi ? FontWeights.Bold : FontWeights.Normal);
                s.HorizontalAlignment = HorizontalAlignment.Center;
                var cella = new Cornice { Profilo = Gradini.Minimo, Child = s, Height = 32, Margin = new Thickness(1), Fondo = eOggi ? Tema.LimePennello : null };
                griglia.Children.Add(cella);
            }
        }
        var prima = new Tasto(Tema.Glifo("\uE76B", 11), 30, 28) { Sinistro = () => { mese = mese.AddMonths(-1); Disegna(); } };
        var dopo = new Tasto(Tema.Glifo("\uE76C", 11), 30, 28) { Sinistro = () => { mese = mese.AddMonths(1); Disegna(); } };
        frecce.Children.Add(prima);
        frecce.Children.Add(dopo);
        testa.Children.Add(frecce);
        c.Children.Add(testa);
        c.Children.Add(griglia);
        // la rotella sfoglia i mesi, con la griglia che scorre nella direzione giusta
        var scorri = new TranslateTransform();
        griglia.RenderTransform = scorri;
        void Sfoglia(int verso)
        {
            mese = mese.AddMonths(verso);
            Disegna();
            var e = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            scorri.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(18 * verso, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = e });
            griglia.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240)));
        }
        prima.Sinistro = () => Sfoglia(-1);
        dopo.Sinistro = () => Sfoglia(1);
        c.MouseWheel += (_, e) => { Sfoglia(e.Delta > 0 ? -1 : 1); e.Handled = true; };
        Disegna();
        return c;
    }

    public static UIElement MenuStart()
    {
        var c = Colonna(240);
        string casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        c.Children.Add(Riga("\uE80F", "Home", () => { Chiudi(); Esplora.Apri(casa); }, icona: Ico.Cartella(casa)));
        c.Children.Add(Riga("\uE896", "Download", () => { Chiudi(); Esplora.Apri(System.IO.Path.Combine(casa, "Downloads")); }, icona: Ico.Cartella(System.IO.Path.Combine(casa, "Downloads"))));
        c.Children.Add(Riga("\uE8A5", "Documenti", () => { Chiudi(); Esplora.Apri(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); }, icona: Ico.Cartella(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments))));
        c.Children.Add(Riga("\uE8B7", "Progetti (WSL)", () => { Chiudi(); Esplora.Apri(System.IO.Path.Combine(PercorsiUtente.WslHome,"progetti")); }, icona: Ico.Linux));
        c.Children.Add(Linea());
        c.Children.Add(Riga("\uEC50", "Esplora file", () => { Chiudi(); Esplora.Apri(); }, icona: Ico.Esplora));
        c.Children.Add(Riga("\uE756", "Terminale", () => Apri("wt.exe"), icona: Ico.Terminale));
        c.Children.Add(Riga("\uE9D9", "Gestione attività", () => Apri("taskmgr.exe"), icona: Ico.Attivita));
        c.Children.Add(Riga("\uE713", "Impostazioni", () => Apri("ms-settings:"), icona: Ico.Impostazioni));
        return c;
    }

    public static UIElement MenuBarra()
    {
        var c = Colonna(240);
        c.Children.Add(Riga("\uE9D9", "Gestione attività", () => Apri("taskmgr.exe"), icona: Ico.Attivita));
        c.Children.Add(Riga("\uE8B7", "Cartella della barra", () => { Chiudi(); Esplora.Apri(App.Cartella); }, icona: Ico.Cartella(App.Cartella)));
        c.Children.Add(Linea());
        c.Children.Add(Riga("\uE72C", "Riavvia barra", App.Riavvia));
        c.Children.Add(Riga("\uE711", "Chiudi barra", () => App.Current.Shutdown()));
        return c;
    }

    public static void NuovaIstanza(Gruppo g)
    {
        if (g.Fissata != null) { g.Fissata.Avvia(); return; }
        bool store = g.Aumid != null && (g.Exe == null || g.Exe.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase));
        if (store) { Apri("explorer.exe", @"shell:AppsFolder\" + g.Aumid); return; }
        if (g.Exe != null) Apri(g.Exe);
    }

    public static UIElement MenuApp(Gruppo g)
    {
        var c = Colonna(260);
        var testa = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 8) };
        var img = new Image { Source = g.Icona, Width = 20, Height = 20, Margin = new Thickness(0, 0, 10, 0) };
        RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
        testa.Children.Add(img);
        var nome = Tema.Scritta(g.Nome ?? "", 13.5, null, FontWeights.SemiBold);
        nome.MaxWidth = 200;
        nome.TextTrimming = TextTrimming.CharacterEllipsis;
        testa.Children.Add(nome);
        c.Children.Add(testa);
        c.Children.Add(Riga("\uE8A7", g.Finestre.Count == 0 ? "Apri" : "Nuova finestra", () => NuovaIstanza(g)));
        if (g.Fissata != null) c.Children.Add(Riga("\uE77A", "Togli dalla barra", () => { App.Io.Fissate.Togli(g.Fissata); App.Io.Finestre.Aggiorna(); }));
        else if (g.Exe != null || g.Aumid != null) c.Children.Add(Riga("\uE718", "Fissa sulla barra", () => { App.Io.Fissate.Fissa(g); App.Io.Finestre.Aggiorna(); }));
        if (g.Finestre.Count > 0)
        {
            c.Children.Add(Linea());
            var finestre = g.Finestre.Select(f => f.H).ToList();
            c.Children.Add(Riga("\uE711", finestre.Count == 1 ? "Chiudi finestra" : $"Chiudi tutte le finestre ({finestre.Count})",
                () => { foreach (var h in finestre) N.PostMessage(h, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }));
        }
        return c;
    }

    // Anteprime dal vivo (miniature DWM) delle finestre di un'app.
    public static Pannello Anteprima(Barra barra, TastoApp t)
    {
        var g = t.Gruppo;
        if (g.Finestre.Count == 0) return null;
        var fila = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8) };
        var posti = new List<(Border posto, IntPtr h)>();
        foreach (var f in g.Finestre.Take(6))
        {
            var col = new StackPanel { Width = 232 };
            var testa = new Grid { Margin = new Thickness(6, 2, 0, 6) };
            testa.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            testa.ColumnDefinitions.Add(new ColumnDefinition());
            testa.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var img = new Image { Source = g.Icona, Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Left };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            testa.Children.Add(img);
            var tit = Tema.Scritta(f.Titolo, 12);
            tit.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(tit, 1);
            testa.Children.Add(tit);
            var h = f.H;
            var chiudi = new Tasto(Tema.Glifo("\uE711", 10), 24, 22) { Sinistro = () => N.PostMessage(h, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero) };
            Grid.SetColumn(chiudi, 2);
            testa.Children.Add(chiudi);
            col.Children.Add(testa);
            var posto = new Border { Height = 132, Margin = new Thickness(6, 0, 6, 4) };
            var icona = new Image { Source = g.Icona, Width = 40, Height = 40, Opacity = .35 };
            posto.Child = icona;
            col.Children.Add(posto);
            var card = new Tasto(col, double.NaN, double.NaN) { Margin = new Thickness(2), Sinistro = () => { Chiudi(); N.Attiva(h); } };
            card.Centrale = () => N.PostMessage(h, N.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            fila.Children.Add(card);
            posti.Add((posto, h));
        }
        var p = new Pannello(barra, t, fila, false) { Tag = g.Firma };
        var miniature = new List<IntPtr>();
        void Aggiorna()
        {
            if (p.Hwnd == IntPtr.Zero) return;
            double s = VisualTreeHelper.GetDpi(p).DpiScaleX;
            for (int i = 0; i < posti.Count; i++)
            {
                if (i >= miniature.Count)
                {
                    N.DwmRegisterThumbnail(p.Hwnd, posti[i].h, out var th);
                    miniature.Add(th);
                }
                var th2 = miniature[i];
                if (th2 == IntPtr.Zero) continue;
                N.DwmQueryThumbnailSourceSize(th2, out var dim);
                var posto = posti[i].posto;
                Point o;
                try { o = posto.TransformToAncestor(p).Transform(new Point(0, 0)); } catch { continue; }
                double bw = posto.ActualWidth * s, bh = posto.ActualHeight * s;
                double k = dim.cx > 0 && dim.cy > 0 ? Math.Min(bw / dim.cx, bh / dim.cy) : 1;
                double w = dim.cx * k, hh = dim.cy * k;
                int x = (int)Math.Round(o.X * s + (bw - w) / 2), y = (int)Math.Round(o.Y * s + (bh - hh) / 2);
                var pr = new N.DWM_THUMBNAIL_PROPERTIES
                {
                    dwFlags = 0x1 | 0x4 | 0x8 | 0x10,
                    rcDestination = new N.RECT { L = x, T = y, R = x + (int)Math.Round(w), B = y + (int)Math.Round(hh) },
                    opacity = 255,
                    fVisible = true,
                    fSourceClientAreaOnly = false,
                };
                N.DwmUpdateThumbnailProperties(th2, ref pr);
                ((Image)posto.Child).Visibility = dim.cx > 0 && !N.IsIconic(posti[i].h) ? Visibility.Hidden : Visibility.Visible;
            }
        }
        p.ContentRendered += (_, _) => Aggiorna();
        p.SizeChanged += (_, _) => p.Dispatcher.BeginInvoke(Aggiorna, DispatcherPriority.Background);
        p.DpiChanged += (_, _) => p.Dispatcher.BeginInvoke(Aggiorna, DispatcherPriority.Background);
        p.Closed += (_, _) => { foreach (var m in miniature) if (m != IntPtr.Zero) N.DwmUnregisterThumbnail(m); };
        return p;
    }
}
