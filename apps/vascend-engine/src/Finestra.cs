using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using BarraVascend;

namespace VascendEngine;

// Catalogo estensioni con pagine interne e impostazioni di Vascend Technologies.
partial class Finestra : Window, IFinestraPixel
{
    IntPtr hwnd;
    double scala = 1;
    int unitaPx = 2;
    public double UnitaDip => unitaPx / scala;

    Stile stile = Stile.Attuale.Clona();
    readonly ContentControl lavoro = new() { Focusable = false };
    readonly ContentControl piede = new() { Focusable = false };
    readonly TextBlock titolo = Tema.Scritta("", 17, Tema.Bianco, FontWeights.SemiBold);
    TextBlock esito;
    Grid fasciaAzioni;
    string sezione = "Stile";

    // Sezioni interne dell’estensione integrata Vascend Technologies.
    static readonly (string categoria, (string nome, string glifo)[] voci)[] Categorie =
    {
        ("Vascend Suite", new[] { ("Vascend IDE", "ide") }),
        ("Aspetto", new[] { ("Stile", "\ue790"), ("Font", "\uE8D2") }),
        ("Desktop", new[] { ("Barra", "\ue7c4"), ("Isola", "\uE8A1"), ("Widget", "\uECA5") }),
        ("Sistema", new[] { ("Windows", "\ue7f4") }),
        ("Sviluppo", new[] { ("VS Code", "\ue943"), ("Terminali", "\ue756") }),
    };
    static IEnumerable<(string nome, string glifo)> Sezioni => Categorie.SelectMany(c => c.voci);

    public Finestra(string inizio = null)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        Background = Brushes.Transparent;
        UseLayoutRounding = true;
        Title=I18n.Translate("Vascend Engine");
        FontFamily = Tema.Testo;
        Foreground = Tema.Bianco;
        Width = 1220; Height = 780; MinWidth = 960; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        // Il frame nativo viene gestito dal gancio, senza WindowChrome che sovrascrive la regione a gradini.

        var radice = new Grid();
        // spazio di lavoro: titolo con i tasti della finestra, contenuto, piede con le azioni
        var destra = new Grid();
        destra.RowDefinitions.Add(new RowDefinition { Height = new GridLength(58) });
        destra.RowDefinitions.Add(new RowDefinition());
        destra.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        radice.Children.Add(destra);

        var alto = new Grid { Background = Brushes.Transparent };
        alto.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Massimizza(); else Trascina(); };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(28, 0, 250, 0), VerticalAlignment = VerticalAlignment.Center };
        var logo = Logo(26);
        logo.Margin = new Thickness(0, 0, 12, 0);
        brand.Children.Add(logo);
        titolo.FontSize = 14;
        brand.Children.Add(titolo);
        alto.Children.Add(brand);
        var tastiFin = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
        var lingue=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};
        var italiano=Pulsante("IT",()=>I18n.SetLanguage("it"));var english=Pulsante("EN",()=>I18n.SetLanguage("en"));
        foreach(var languageButton in new[]{italiano,english}){languageButton.Padding=new Thickness(8,4,8,4);languageButton.Margin=new Thickness(0,0,3,0);languageButton.MinWidth=32;lingue.Children.Add(languageButton);}
        italiano.ToolTip="Italiano";english.ToolTip="English";
        System.Windows.Automation.AutomationProperties.SetName(italiano,"Italiano");System.Windows.Automation.AutomationProperties.SetName(english,"English");
        void AggiornaLingua(object sender,EventArgs args)
        {
            italiano.BorderBrush=I18n.Language == "it" ? Tema.LimePennello : Contorno;
            english.BorderBrush=I18n.Language == "en" ? Tema.LimePennello : Contorno;
        }
        AggiornaLingua(null,EventArgs.Empty);I18n.LanguageChanged+=AggiornaLingua;
        Closed+=(_,_)=>I18n.LanguageChanged-=AggiornaLingua;
        tastiFin.Children.Add(lingue);
        tastiFin.Children.Add(new Tasto(IconeEngine.Create("minimize",14), 40, 32) { Sinistro = () => WindowState = WindowState.Minimized });
        tastiFin.Children.Add(new Tasto(IconeEngine.Create("maximize",14), 40, 32) { Sinistro = Massimizza });
        tastiFin.Children.Add(new Tasto(IconeEngine.Create("close",14), 40, 32) { Sinistro = Close });
        alto.Children.Add(tastiFin);
        destra.Children.Add(alto);

        lavoro.Margin = new Thickness(26, 0, 22, 0);
        Grid.SetRow(lavoro, 1);
        destra.Children.Add(lavoro);

        var basso = fasciaAzioni = new Grid { Margin = new Thickness(22, 14, 18, 16) };
        basso.ColumnDefinitions.Add(new ColumnDefinition());
        basso.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        esito = Tema.Scritta("", 12, Tema.BiancoTenue);
        esito.HorizontalAlignment = HorizontalAlignment.Right;
        esito.TextWrapping = TextWrapping.Wrap;
        esito.MaxWidth = 700;
        esito.Margin = new Thickness(0, 0, 14, 0);
        basso.Children.Add(esito);
        Grid.SetColumn(piede, 1);
        basso.Children.Add(piede);
        Grid.SetRow(basso, 2);
        destra.Children.Add(basso);

        Content = new CorniceEngine { Profilo = Gradini.Barra, Fondo = Tema.GhiaccioPannello, Bordo = Tema.Bordo, Rilievo = false, Child = radice };

        Closed += (_, _) => ChiudiEstensioni();
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            var source=HwndSource.FromHwnd(hwnd);
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            source.AddHook(GancioCornice);
            ConfiguraCorniceNativa();
            N.SetWindowPos(hwnd,IntPtr.Zero,0,0,0,0,0x0037); // SWP_FRAMECHANGED senza spostare/ridimensionare
            Forma();
        };
        SizeChanged += (_, _) => PianificaForma();
        DpiChanged += (_, _) => PianificaForma();
        KeyDown += (_, e) => { if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) azioneApplica?.Invoke(); };
        if (Sezioni.Any(x => x.nome == inizio)) Apri(inizio); else Catalogo();
    }

    void Trascina() { try { DragMove(); } catch { } }

    // ---------- finestra ----------

    bool formaInAttesa;
    int ultimaLarghezza, ultimaAltezza, ultimaUnita;
    void PianificaForma()
    {
        if (formaInAttesa) return;
        formaInAttesa = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() => {
            formaInAttesa = false;
            Forma();
        }));
    }

    void Forma()
    {
        if (hwnd == IntPtr.Zero || N.IsIconic(hwnd)) return;
        var mon = N.MonitorFromWindow(hwnd, 2);
        N.GetDpiForMonitor(mon, 0, out uint dx, out _);
        double s = dx / 96.0;
        bool cambiata = Math.Abs(s - scala) > .001;
        scala = s;
        unitaPx = Math.Max(2, (int)Math.Round(2 * scala, MidpointRounding.AwayFromZero));
        N.GetWindowRect(hwnd, out var r);
        if (r.W != ultimaLarghezza || r.H != ultimaAltezza || unitaPx != ultimaUnita)
        {
            N.SetWindowRgn(hwnd, Gradini.Regione(r.W, r.H, unitaPx, Gradini.Barra), true);
            ultimaLarghezza = r.W; ultimaAltezza = r.H; ultimaUnita = unitaPx;
        }
        if(Content is CorniceEngine frame)frame.AggiornaRitaglio();
        if (cambiata) Ridisegna(this);
    }

    void ConfiguraCorniceNativa()
    {
        if(hwnd == IntPtr.Zero)return;
        // Evita sia il frame DWM sia i bordi 3D GDI quando cambia l'attivazione.
        int disabled=1;N.DwmSetWindowAttribute(hwnd,2,ref disabled,4); // DWMWA_NCRENDERING_POLICY / DWMNCRP_DISABLED
        int dark=1;N.DwmSetWindowAttribute(hwnd,20,ref dark,4);
        int corners=1;N.DwmSetWindowAttribute(hwnd,33,ref corners,4);
        int border=unchecked((int)0xFFFFFFFE);N.DwmSetWindowAttribute(hwnd,34,ref border,4);
        long style=(long)N.GetWindowLongPtr(hwnd,-20);
        long clean=style&~0x300L; // WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE
        if(style != clean)N.SetWindowLongPtr(hwnd,-20,(IntPtr)clean);
    }

    IntPtr GancioCornice(IntPtr h,int messaggio,IntPtr wParam,IntPtr lParam,ref bool gestito)
    {
        if(messaggio == 0x007C && wParam.ToInt64() == -20 && lParam != IntPtr.Zero)
        {
            // WPF aggiorna gli stili anche dopo SourceInitialized e al cambio stato.
            int proposed=Marshal.ReadInt32(lParam,4);Marshal.WriteInt32(lParam,4,proposed&~0x300);
        }
        if(messaggio == 0x0085){gestito=true;return IntPtr.Zero;} // WM_NCPAINT: solo cornice Engine
        if(messaggio == 0x0086){gestito=true;return (IntPtr)1;} // WM_NCACTIVATE: niente bordo chiaro inattivo
        if(messaggio is 0x031A or 0x031E)ConfiguraCorniceNativa();
        if(messaggio == 0x0083){gestito=true;return IntPtr.Zero;} // WM_NCCALCSIZE: client a tutta finestra
        if(messaggio == 0x0084 && !N.IsIconic(h)) // WM_NCHITTEST: ridimensionamento nativo sui bordi
        {
            long point=lParam.ToInt64();int x=(short)(point&0xffff),y=(short)((point>>16)&0xffff);
            N.GetWindowRect(h,out var r);int border=Math.Max(5,(int)Math.Ceiling(6*scala));
            bool left=x<r.L+border,right=x>=r.R-border,top=y<r.T+border,bottom=y>=r.B-border;
            int hit=top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 0;
            if(hit != 0){gestito=true;return (IntPtr)hit;}
        }
        return IntPtr.Zero;
    }

    static void Ridisegna(DependencyObject d)
    {
        if (d is UIElement u) u.InvalidateVisual();
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) Ridisegna(VisualTreeHelper.GetChild(d, i));
    }

    // massimizzata = area di lavoro dello schermo, cioè accanto alla barra
    N.RECT? normale;
    void Massimizza()
    {
        if (normale is N.RECT r) { normale = null; N.SetWindowPos(hwnd, IntPtr.Zero, r.L, r.T, r.W, r.H, 0x14); return; }
        N.GetWindowRect(hwnd, out var ora);
        normale = ora;
        var mi = new N.MONITORINFOEX { cbSize = Marshal.SizeOf<N.MONITORINFOEX>() };
        N.GetMonitorInfo(N.MonitorFromWindow(hwnd, 2), ref mi);
        var w = mi.rcWork;
        N.SetWindowPos(hwnd, IntPtr.Zero, w.L, w.T, w.W, w.H, 0x14);
    }

    public static ToolTip Suggerimento(string t) => new()
    {
        Content = Tema.Scritta(t, 12),
        Background = Tema.Pennello(24, 34, 44, .96),
        BorderBrush = Tema.Pennello(235, 246, 255, .18),
        Padding = new Thickness(9, 5, 9, 6),
    };

    // ---------- sezioni ----------

    Action azioneApplica;

    void Apri(string nome)
    {
        var manifest=ExtensionActivation.Builtin();
        bool readOnly=SolaLettura(manifest);
        fasciaAzioni.Visibility = Visibility.Visible;
        sezione = nome;
        titolo.Text = "Vascend Technologies / " + nome;
        esito.Text = "";
        azioneApplica = null;
        piede.Content = null;
        var contenuto = nome switch
        {
            "Stile" => SezioneStile(),
            "Font" => SezioneFontInclusi(),
            "Barra" => SezioneBarra(),
            "Isola" => SezioneIsola(),
            "Widget" => SezioneWidget(),
            "Windows" => SezioneWindows(),
            "VS Code" => SezioneVsCode(),
            "Vascend IDE" => SezioneIde(),
            _ => SezioneTerminali(),
        };
        contenuto.Opacity = 0;
        var sposta = new TranslateTransform(0, 8);
        contenuto.RenderTransform = sposta;
        var pagina = new DockPanel();
        var navigazione = NavigazioneGhiaccio(nome);
        DockPanel.SetDock(navigazione, Dock.Top);
        pagina.Children.Add(navigazione);
        if(readOnly)
        {
            var notice=AvvisoSolaLettura(manifest);DockPanel.SetDock(notice,Dock.Top);pagina.Children.Add(notice);
            pagina.Children.Add(new OpzioniSolaLettura(contenuto));
            azioneApplica=null;piede.Content=null;fasciaAzioni.Visibility=Visibility.Collapsed;
        }
        else pagina.Children.Add(contenuto);
        lavoro.Content = pagina;
        contenuto.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(200)));
        sposta.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    // Piede: tasti secondari e il tasto pieno che applica.
    void Piede(Action applica, string testo = "Applica", params UIElement[] altri)
    {
        azioneApplica = applica;
        var st = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var a in altri) st.Children.Add(a);
        if (applica != null)
        {
            st.Children.Add(Pulsante(testo, applica, primary: true));
        }
        piede.Content = st;
    }

    void Esito(string t, bool male = false)
    {
        fasciaAzioni.Visibility = Visibility.Visible;
        esito.Text = t;
        esito.Foreground = male ? Tema.Pennello(255, 107, 94, 1) : Tema.BiancoTenue;
        esito.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
    }

    // Lavoro lento (file, riavvii) fuori dalla finestra, poi l'esito nel piede.
    bool occupato;
    async void Esegui(params (string meta, Func<string> fai)[] cose)
    {
        if (occupato) return;
        occupato = true;
        Esito("…");
        var fatti = new List<string>();
        string errore = null;
        await Task.Run(() =>
        {
            foreach (var (meta, fai) in cose)
                try { fatti.Add(fai()); }
                catch (Exception e) { errore = meta + ": " + e.Message; break; }
        });
        occupato = false;
        if (errore != null) Esito(errore, true);
        else Esito(string.Join(" · ", fatti.Where(f => !string.IsNullOrEmpty(f))));
    }

    // ---------- Stile ----------

    static readonly (string gruppo, (string prop, string nome)[] voci)[] Voci =
    {
        ("Interfaccia", new[] { ("Accento", "Accento"), ("Testo", "Testo"), ("Ghiaccio", "Ghiaccio"), ("Vetro", "Vetro") }),
        ("Fondi", new[] { ("Fondo", "Editor"), ("Lato", "Pannelli"), ("Barra", "Barre"), ("Alto", "Campi") }),
        ("Codice", new[] { ("Azzurro", "Funzioni"), ("VerdeAcqua", "Tipi"), ("Stringhe", "Stringhe"), ("Arancio", "Numeri"), ("Viola", "Attributi"), ("Rosa", "this"), ("Rosso", "Errori"), ("Commenti", "Commenti") }),
    };

    readonly Dictionary<string, bool> mete = new() { ["Barra"] = true, ["VS Code"] = true, ["Vascend IDE"] = true, ["Terminali"] = true };
    Border anteprima;
    Action aggiornaAnteprima;

    FrameworkElement SezioneStile()
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        g.ColumnDefinitions.Add(new ColumnDefinition());

        var col = new StackPanel();
        var preset = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        foreach (var p in Stile.Preset) preset.Children.Add(TastoPreset(p));
        col.Children.Add(preset);
        foreach (var (gruppo, voci) in Voci)
        {
            col.Children.Add(Titolo(gruppo));
            foreach (var (prop, nome) in voci) col.Children.Add(RigaColore(prop, nome));
        }
        g.Children.Add(Scorri(col));

        anteprima = new Border { Margin = new Thickness(2) };
        aggiornaAnteprima = () => anteprima.Child = Anteprima.Costruisci(stile);
        aggiornaAnteprima();
        var c = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.Pennello(0, 0, 0, .18), Bordo = Tema.Pennello(235, 246, 255, .14), Child = anteprima };
        Grid.SetColumn(c, 2);
        g.Children.Add(c);

        var scelte = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 10, 0) };
        foreach (var m in mete.Keys.ToList()) scelte.Children.Add(Casella(m));
        var esporta = new Tasto(Tema.Glifo("", 13), 40, 38) { ToolTip = Suggerimento("Esporta il tema VS Code (.vsix)"), Sinistro = Esporta };
        Piede(ApplicaStile, "Applica", scelte, esporta);
        return g;
    }

    void ApplicaStile()
    {
        var s = stile.Clona();
        var cose = new List<(string, Func<string>)> { ("stile", () => { s.Salva(); return "stile salvato"; }) };
        if (mete["VS Code"] && Generatori.VsCodePresente) cose.Add(("VS Code", () => Generatori.ApplicaVsCode(s)));
        if (mete["Vascend IDE"] && (Generatori.IdePresente(0) || Generatori.IdePresente(1))) cose.Add(("Vascend IDE", () => Generatori.ApplicaIde(s)));
        if (mete["Terminali"] && Generatori.TerminalePresente) cose.Add(("Terminali", () => Generatori.ApplicaTerminale(s)));
        if (mete["Barra"] && Generatori.BarraPresente) cose.Add(("Barra", Generatori.ApplicaBarra));
        Esegui(cose.ToArray());
    }

    FrameworkElement Casella(string meta)
    {
        bool presente = meta switch { "Barra" => Generatori.BarraPresente, "VS Code" => Generatori.VsCodePresente, "Vascend IDE" => Generatori.IdePresente(0) || Generatori.IdePresente(1), _ => Generatori.TerminalePresente };
        if (!presente) mete[meta] = false;
        var segno = Tema.Glifo("", 11, Tema.LimePennello);
        var casella = new Cornice { Profilo = Gradini.Minimo, Width = 18, Height = 18, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .35), Child = segno, Margin = new Thickness(0, 0, 8, 0) };
        var st = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 12, 0) };
        st.Children.Add(casella);
        st.Children.Add(Tema.Scritta(meta, 12.5));
        void Mostra() { segno.Opacity = mete[meta] ? 1 : 0; st.Opacity = presente ? 1 : .4; }
        var t = new Tasto(st, double.NaN, 38) { IsEnabled = presente, Sinistro = () => { mete[meta] = !mete[meta]; Mostra(); } };
        Mostra();
        return t;
    }

    FrameworkElement TastoPreset(Stile p)
    {
        bool scelto = p.Nome == stile.Nome;
        var col = new StackPanel { Margin = new Thickness(8, 6, 8, 7) };
        var strisce = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var hex in new[] { p.Fondo, p.Ghiaccio, p.Accento, p.Azzurro, p.Arancio })
            strisce.Children.Add(new Border { Width = 9, Height = 18, Background = new SolidColorBrush(Stile.Colore(hex)) });
        col.Children.Add(strisce);
        var n = Tema.Scritta(p.Nome, 11, scelto ? Tema.Bianco : Tema.BiancoTenue, scelto ? FontWeights.SemiBold : FontWeights.Normal);
        n.HorizontalAlignment = HorizontalAlignment.Center;
        n.Margin = new Thickness(0, 5, 0, 0);
        col.Children.Add(n);
        var t = new Tasto(col, double.NaN, double.NaN) { Acceso = scelto, Margin = new Thickness(0, 0, 4, 4) };
        t.Sinistro = () =>
        {
            // il preset cambia i colori; caratteri, barra e terminali restano come sono
            var tieni = stile;
            stile = p.Clona();
            stile.FontCodice = tieni.FontCodice; stile.DimEditor = tieni.DimEditor; stile.DimTerminale = tieni.DimTerminale;
            stile.Acrilico = tieni.Acrilico; stile.Cursore = tieni.Cursore; stile.PosizioneBarra = tieni.PosizioneBarra;
            Apri("Stile");
        };
        return t;
    }

    FrameworkElement RigaColore(string prop, string nome)
    {
        var pi = typeof(Stile).GetProperty(prop);
        string Valore() => (string)pi.GetValue(stile);
        return RigaColoreLibera(nome, Valore, v =>
        {
            pi.SetValue(stile, v);
            if (!Stile.Preset.Any(x => x.Nome == stile.Nome && (string)pi.GetValue(x) == v)) stile.Nome = "Personale";
            Presto();
        });
    }

    FrameworkElement RigaColoreLibera(string nome, Func<string> leggi, Action<string> scrivi)
    {
        var g = new Grid { Margin = new Thickness(8, 0, 8, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var campione = new Cornice { Profilo = Gradini.Minimo, Width = 20, Height = 20, Rilievo = false, Fondo = new SolidColorBrush(Stile.Colore(leggi())), Bordo = Tema.Pennello(255, 255, 255, .25), HorizontalAlignment = HorizontalAlignment.Left };
        g.Children.Add(campione);
        var n = Etichetta(nome);
        Grid.SetColumn(n, 1);
        g.Children.Add(n);
        var hex = Tema.Scritta(Stile.Hex(Stile.Colore(leggi())), 11.5, Tema.BiancoTenue);
        hex.FontFamily = new FontFamily("JetBrainsMono Nerd Font, Cascadia Mono, Consolas");
        Grid.SetColumn(hex, 2);
        g.Children.Add(hex);
        var t = new Tasto(g, double.NaN, 34) { Margin = new Thickness(0, 1, 0, 1) };
        t.Sinistro = () =>
        {
            var sel = new Selettore(Stile.Colore(leggi())) { PlacementTarget = t };
            sel.Cambiato += c =>
            {
                campione.Fondo = new SolidColorBrush(c);
                hex.Text = Stile.Hex(c);
                scrivi(Stile.Hex(c));
            };
            sel.IsOpen = true;
        };
        return t;
    }

    // l'anteprima si ridisegna al massimo ogni 40 ms mentre si trascina nel selettore
    System.Windows.Threading.DispatcherTimer presto;
    void Presto()
    {
        presto ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(40), System.Windows.Threading.DispatcherPriority.Render, (_, _) => { presto.Stop(); aggiornaAnteprima?.Invoke(); }, Dispatcher);
        if (!presto.IsEnabled) presto.Start();
    }

    void Esporta()
    {
        var d = new Microsoft.Win32.SaveFileDialog { FileName = "tema-ghiaccio-1.0.0.vsix", Filter = I18n.Translate("Estensione VS Code|*.vsix"), InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads" };
        if (d.ShowDialog(this) != true) return;
        try { Generatori.Vsix(stile, d.FileName); Esito("tema esportato"); }
        catch (Exception e) { Esito(e.Message, true); }
    }

    // ---------- Barra ----------

    FrameworkElement SezioneBarra()
    {
        var col = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        col.Children.Add(Titolo("Posizione"));
        var tessere = new WrapPanel();
        foreach (var (lato, nome) in new[] { ("sinistra", "Sinistra"), ("destra", "Destra"), ("alto", "In alto"), ("basso", "In basso") })
            tessere.Children.Add(TesseraLato(lato, nome));
        col.Children.Add(tessere);

        col.Children.Add(Titolo("Vetro"));
        col.Children.Add(RigaNumero("Opacità", () => (int)Math.Round(stile.Trasparenza * 100), v => stile.Trasparenza = v / 100.0, 40, 200, 10, v => v + "%"));

        col.Children.Add(Titolo("Avvio"));
        col.Children.Add(Interruttore("Avvia la barra con Windows", () => Sistema.BarraAllAvvio, v => Sistema.BarraAllAvvio = v));
        col.Children.Add(Interruttore("Nascondi la barra di Windows all'accesso", () => Sistema.NascondiBarraWindows, v => Sistema.NascondiBarraWindows = v));
        col.Children.Add(Interruttore("Riapplica lo stile all'accesso", () => Sistema.EngineAllAvvio, v => Sistema.EngineAllAvvio = v));

        Piede(() =>
        {
            var s = Stile.Attuale.Clona();
            s.PosizioneBarra = stile.PosizioneBarra;
            s.Trasparenza = stile.Trasparenza;
            Esegui(("barra", () => { s.Salva(); return Generatori.BarraPresente ? Generatori.ApplicaBarra() : "barra non trovata"; }));
        });
        return Scorri(col);
    }

    // Tessera con uno schermo in miniatura e la barra sul suo lato.
    FrameworkElement TesseraLato(string lato, string nome)
    {
        bool scelto = stile.PosizioneBarra == lato || (stile.PosizioneBarra == null && lato == "basso");
        var schermo = new Grid { Width = 120, Height = 72 };
        schermo.Children.Add(new Border { Background = new LinearGradientBrush(Color.FromRgb(46, 62, 82), Color.FromRgb(20, 26, 36), 60) });
        var vetro = Tema.Pennello(222, 240, 252, .55);
        var b = new Border { Background = scelto ? Tema.LimePennello : vetro, Margin = new Thickness(3) };
        switch (lato)
        {
            case "sinistra": b.Width = 8; b.HorizontalAlignment = HorizontalAlignment.Left; break;
            case "destra": b.Width = 8; b.HorizontalAlignment = HorizontalAlignment.Right; break;
            case "alto": b.Height = 8; b.VerticalAlignment = VerticalAlignment.Top; break;
            default: b.Height = 8; b.VerticalAlignment = VerticalAlignment.Bottom; break;
        }
        schermo.Children.Add(b);
        // una finestra che occupa lo spazio rimasto
        var fin = new Border
        {
            Background = Tema.Pennello(255, 255, 255, .10), BorderBrush = Tema.Pennello(255, 255, 255, .18), BorderThickness = new Thickness(1),
            Margin = lato switch { "sinistra" => new Thickness(18, 8, 8, 8), "destra" => new Thickness(8, 8, 18, 8), "alto" => new Thickness(8, 18, 8, 8), _ => new Thickness(8, 8, 8, 18) },
        };
        schermo.Children.Add(fin);
        var col = new StackPanel { Margin = new Thickness(10) };
        col.Children.Add(new Cornice { Profilo = Gradini.Minimo, Rilievo = false, Bordo = scelto ? Tema.LimePennello : Tema.Pennello(255, 255, 255, .2), Child = schermo });
        var n = Tema.Scritta(nome, 12, scelto ? Tema.Bianco : Tema.BiancoTenue, scelto ? FontWeights.SemiBold : FontWeights.Normal);
        n.HorizontalAlignment = HorizontalAlignment.Center;
        n.Margin = new Thickness(0, 8, 0, 0);
        col.Children.Add(n);
        return new Tasto(col, double.NaN, double.NaN) { Acceso = scelto, Margin = new Thickness(0, 0, 8, 8), Sinistro = () => { stile.PosizioneBarra = lato; Apri("Barra"); } };
    }

    // ---------- Isola ----------

    FrameworkElement SezioneIsola()
    {
        stile.IsolaModuli ??= new();
        var col = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        var anteprima = new ContentControl();
        void Anteprima() { anteprima.Content = AnteprimaIsola(); }
        // ogni modifica arriva subito all'isola vera: la barra rilegge lo stile quando cambia
        void Cambiato() { Anteprima(); SalvaIsola(); }
        void Disegna()
        {
            col.Children.Clear();
            Anteprima();
            col.Children.Add(anteprima);

            col.Children.Add(Titolo("Isola"));
            col.Children.Add(Interruttore("Mostra l'isola", () => stile.IsolaAttiva, v => { stile.IsolaAttiva = v; Cambiato(); }));
            col.Children.Add(Scelta("Posizione", new[] { ("Sinistra", "sinistra"), ("Centro", "centro"), ("Destra", "destra") }, () => stile.IsolaPosizione ?? "centro", v => { stile.IsolaPosizione = v; Cambiato(); }));
            col.Children.Add(Scelta("Schermi", new[] { ("Tutti", "tutti"), ("Principale", "principale") }, () => stile.IsolaSchermi ?? "tutti", v => { stile.IsolaSchermi = v; Cambiato(); }));

            col.Children.Add(Titolo("Aspetto"));
            col.Children.Add(RigaNumero("Dimensione", () => stile.IsolaScala, v => { stile.IsolaScala = v; Cambiato(); }, 60, 200, 10, v => v + "%"));
            col.Children.Add(RigaNumero("Opacità del vetro", () => stile.IsolaOpacita, v => { stile.IsolaOpacita = v; Cambiato(); }, 10, 100, 10, v => v + "%"));
            col.Children.Add(RigaNumero("Distanza dal bordo", () => stile.IsolaDistanza, v => { stile.IsolaDistanza = v; Cambiato(); }, 0, 120, 2, v => v + " px"));

            col.Children.Add(Titolo("Moduli"));
            var ordine = stile.IsolaModuli.Where(m => Stile.ModuliIsola.Any(x => x.id == m)).Concat(Stile.ModuliIsola.Select(x => x.id).Where(m => !stile.IsolaModuli.Contains(m))).ToList();
            foreach (var id in ordine) col.Children.Add(RigaModulo(id, () => { SalvaIsola(); Disegna(); }));

            if (stile.IsolaModuli.Contains("musica"))
            {
                col.Children.Add(Titolo("Musica"));
                col.Children.Add(Interruttore("Copertina", () => stile.IsolaCopertina, v => { stile.IsolaCopertina = v; Cambiato(); }));
                col.Children.Add(Interruttore("Comandi", () => stile.IsolaComandi, v => { stile.IsolaComandi = v; Cambiato(); }));
                col.Children.Add(Interruttore("Barra di avanzamento", () => stile.IsolaAvanzamento, v => { stile.IsolaAvanzamento = v; Cambiato(); }));
                col.Children.Add(Interruttore("Solo mentre suona", () => stile.IsolaSoloSuona, v => { stile.IsolaSoloSuona = v; Cambiato(); }));
                col.Children.Add(RigaNumero("Larghezza del titolo", () => stile.IsolaTitolo, v => { stile.IsolaTitolo = v; Cambiato(); }, 60, 400, 10, v => v + " px"));
            }
            if (stile.IsolaModuli.Contains("claude"))
            {
                col.Children.Add(Titolo("Claude"));
                col.Children.Add(Interruttore("Limite di 5 ore", () => stile.IsolaCinqueOre, v => { stile.IsolaCinqueOre = v; Cambiato(); }));
                col.Children.Add(Interruttore("Limite di 7 giorni", () => stile.IsolaSetteGiorni, v => { stile.IsolaSetteGiorni = v; Cambiato(); }));
            }
            if (stile.IsolaModuli.Contains("sistema"))
            {
                col.Children.Add(Titolo("CPU e memoria"));
                col.Children.Add(Interruttore("CPU", () => stile.IsolaCpu, v => { stile.IsolaCpu = v; Cambiato(); }));
                col.Children.Add(Interruttore("Memoria", () => stile.IsolaRam, v => { stile.IsolaRam = v; Cambiato(); }));
            }
        }
        Disegna();
        var ripristina = TastoBordato("\uE72C", "Ripristina", () =>
        {
            var d = new Stile();
            foreach (var p in typeof(Stile).GetProperties())
                if (p.Name.StartsWith("Isola") && p.CanWrite) p.SetValue(stile, p.GetValue(d));
            SalvaIsola();
            Disegna();
        });
        Piede(null, "Applica", ripristina);
        return Scorri(col);
    }

    System.Windows.Threading.DispatcherTimer salvaIsola;
    void SalvaIsola()
    {
        salvaIsola ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(350), System.Windows.Threading.DispatcherPriority.Background, (_, _) =>
        {
            salvaIsola.Stop();
            try
            {
                var s = Stile.Attuale.Clona();
                foreach (var p in typeof(Stile).GetProperties())
                    if (p.Name.StartsWith("Isola") && p.CanWrite) p.SetValue(s, p.Name == "IsolaModuli" ? new List<string>(stile.IsolaModuli) : p.GetValue(stile));
                s.Salva();
                Esito("isola aggiornata");
            }
            catch (Exception e) { Esito(e.Message, true); }
        }, Dispatcher);
        salvaIsola.Stop();
        salvaIsola.Start();
    }

    FrameworkElement RigaModulo(string id, Action ridisegna)
    {
        var lista = stile.IsolaModuli;
        int pos = lista.IndexOf(id);
        bool acceso = pos >= 0;
        var g = new Grid { Margin = new Thickness(8, 0, 4, 0), Height = 38 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var segno = new Cornice
        {
            Profilo = Gradini.Minimo, Width = 18, Height = 18, Rilievo = false, HorizontalAlignment = HorizontalAlignment.Left,
            Fondo = acceso ? Tema.LimePennello : null, Bordo = acceso ? null : Tema.Pennello(255, 255, 255, .35),
            Child = acceso ? Tema.Glifo("\uE73E", 11, Tema.Pennello(20, 28, 36, 1)) : null,
        };
        var t = new Tasto(segno, 30, 30) { HorizontalAlignment = HorizontalAlignment.Left, Sinistro = () => { if (acceso) lista.Remove(id); else lista.Add(id); ridisegna(); } };
        g.Children.Add(t);
        var n = Etichetta(Stile.ModuliIsola.First(x => x.id == id).nome, acceso ? Tema.Bianco : Tema.BiancoTenue, "modulo:" + id);
        Grid.SetColumn(n, 1);
        g.Children.Add(n);
        if (acceso)
        {
            var frecce = new StackPanel { Orientation = Orientation.Horizontal };
            void Sposta(int d) { int a = pos + d; if (a < 0 || a >= lista.Count) return; (lista[pos], lista[a]) = (lista[a], lista[pos]); ridisegna(); }
            var su = new Tasto(Tema.Glifo("\uE70E", 10), 30, 28) { Sinistro = () => Sposta(-1), Opacity = pos > 0 ? 1 : .3 };
            var giu = new Tasto(Tema.Glifo("\uE70D", 10), 30, 28) { Sinistro = () => Sposta(1), Opacity = pos < lista.Count - 1 ? 1 : .3 };
            frecce.Children.Add(su);
            frecce.Children.Add(giu);
            Grid.SetColumn(frecce, 2);
            g.Children.Add(frecce);
        }
        return g;
    }

    // Lo schermo in miniatura con l'isola com'è adesso.
    FrameworkElement AnteprimaIsola()
    {
        var schermo = new Grid { Width = 600, Height = 130 };
        schermo.Children.Add(new Border { Background = new LinearGradientBrush(Color.FromRgb(46, 62, 82), Color.FromRgb(20, 26, 36), 60) });
        if (stile.IsolaAttiva && stile.IsolaModuli.Count > 0)
        {
            var riga = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };
            TextBlock Num(string t) => Tema.Scritta(t, 12, Tema.Pennello(244, 244, 239, 1), FontWeights.Bold);
            TextBlock Uni(string t) { var x = Tema.Scritta(t, 10, Tema.Pennello(244, 248, 250, .65), FontWeights.Medium); x.Margin = new Thickness(3, 1, 10, 0); return x; }
            foreach (var id in stile.IsolaModuli)
            {
                var m = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 4, 0) };
                switch (id)
                {
                    case "musica":
                        if (stile.IsolaCopertina) m.Children.Add(new Border { Width = 22, Height = 22, Background = new LinearGradientBrush(Stile.Colore(stile.Accento), Stile.Colore(stile.Azzurro), 45), Margin = new Thickness(0, 0, 10, 0) });
                        var tc = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Width = Math.Clamp(stile.IsolaTitolo, 60, 400) };
                        var tt = Tema.Scritta("Brano in ascolto · Artista", 12, Tema.Bianco, FontWeights.SemiBold);
                        tt.TextTrimming = TextTrimming.CharacterEllipsis;
                        tc.Children.Add(tt);
                        if (stile.IsolaAvanzamento)
                        {
                            var bar = new Grid { Height = 2, Margin = new Thickness(0, 3, 0, 0) };
                            bar.Children.Add(new Border { Background = Tema.Pennello(255, 255, 255, .2) });
                            bar.Children.Add(new Border { Background = Tema.LimePennello, HorizontalAlignment = HorizontalAlignment.Left, Width = tc.Width * .4 });
                            tc.Children.Add(bar);
                        }
                        m.Children.Add(tc);
                        if (stile.IsolaComandi)
                            foreach (var gl in new[] { "\uE892", "\uE768", "\uE893" }) { var x = Tema.Glifo(gl, 10, Tema.Pennello(255, 255, 255, .85)); x.Margin = new Thickness(10, 0, 0, 0); m.Children.Add(x); }
                        break;
                    case "claude":
                        if (stile.IsolaCinqueOre) { m.Children.Add(Num("42%")); m.Children.Add(Uni("5h")); }
                        if (stile.IsolaSetteGiorni) { m.Children.Add(Num("18%")); m.Children.Add(Uni("7g")); }
                        if (m.Children.Count == 0) m.Children.Add(Uni("Claude"));
                        break;
                    case "ide":
                        var lupo = new Image { Width = 16, Height = 16, Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/ide.png")), Margin = new Thickness(0, 0, 8, 0) };
                        m.Children.Add(lupo);
                        m.Children.Add(Tema.Scritta("Sistema la barra", 12, Tema.Bianco, FontWeights.SemiBold));
                        m.Children.Add(new Border { Width = 8, Height = 8, Background = Tema.LimePennello, Margin = new Thickness(8, 0, 0, 0) });
                        break;
                    case "sistema":
                        if (stile.IsolaCpu || !stile.IsolaRam) { m.Children.Add(Num("23%")); m.Children.Add(Uni("cpu")); }
                        if (stile.IsolaRam) { m.Children.Add(Num("41%")); m.Children.Add(Uni("ram")); }
                        break;
                    default: continue;
                }
                if (riga.Children.Count > 0) riga.Children.Add(new Border { Width = 1, Height = 16, Background = Tema.Pennello(235, 246, 255, .24), Margin = new Thickness(4, 0, 0, 0) });
                riga.Children.Add(m);
            }
            var vetro = Tema.Ghiaccio.Clone();
            vetro.Opacity = Math.Clamp(stile.IsolaOpacita, 10, 100) / 100.0;
            double k = Math.Clamp(stile.IsolaScala, 60, 200) / 100.0;
            var isola = new Cornice
            {
                Profilo = Gradini.Barra, Fondo = vetro, Bordo = Tema.Bordo, Height = 38, Child = riga, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(12, Math.Clamp(stile.IsolaDistanza, 0, 120) * .6, 12, 0),
                HorizontalAlignment = stile.IsolaPosizione switch { "sinistra" => HorizontalAlignment.Left, "destra" => HorizontalAlignment.Right, _ => HorizontalAlignment.Center },
                LayoutTransform = new ScaleTransform(k * .8, k * .8),
            };
            schermo.Children.Add(isola);
        }
        return new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .2), Child = new Border { ClipToBounds = true, Child = schermo }, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 8, 0, 4) };
    }

    // ---------- Widget ----------

    int scelto;

    static readonly string[] Punti = { "alto-sinistra", "alto", "alto-destra", "sinistra", "centro", "destra", "basso-sinistra", "basso", "basso-destra" };

    FrameworkElement SezioneWidget()
    {
        stile.Scrivania ??= new();
        var col = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        var anteprima = new ContentControl { HorizontalAlignment = HorizontalAlignment.Left };
        void AggiornaAnteprima() { anteprima.Content = SchermoWidget(Disegna); }
        void Disegna()
        {
            col.Children.Clear();
            var lista = stile.Scrivania;
            if (scelto >= lista.Count) scelto = lista.Count - 1;
            AggiornaAnteprima();
            col.Children.Add(anteprima);

            col.Children.Add(Titolo("Aggiungi"));
            var tipi = new WrapPanel { Margin = new Thickness(4, 0, 0, 0) };
            foreach (var (id, nome, glifo, _) in Widget.Tipi)
            {
                var st = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 14, 0) };
                st.Children.Add(Tema.Glifo(glifo, 13));
                var n = Tema.Scritta(nome, 12.5);
                n.Margin = new Thickness(8, 0, 0, 1);
                st.Children.Add(n);
                var tipo = id;
                tipi.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .22), Child = st }, double.NaN, 34)
                {
                    Margin = new Thickness(0, 0, 6, 6),
                    Sinistro = () => { lista.Add(Widget.Nuovo(tipo)); scelto = lista.Count - 1; Disegna(); },
                });
            }
            col.Children.Add(tipi);

            if (scelto < 0) return;
            var w = lista[scelto];
            var tipoW = Widget.Tipi.FirstOrDefault(x => x.id == w.Tipo);
            col.Children.Add(Titolo(tipoW.nome ?? w.Tipo));
            col.Children.Add(Titolo("Stile del widget"));
            var stili = new WrapPanel { Margin = new Thickness(8,4,0,12) };
            foreach (var (id,nome,descrizione) in WidgetAspetto.Stili)
            {
                var sample = new Widget {Tipo="orologio",Dimensione=26,Aspetto=id,NomeFirma=w.NomeFirma,Firma=w.Firma,Colore=w.Colore};
                var content = new StackPanel { Width=126 };
                var sampleBody = new StackPanel();
                var ora = WidgetAspetto.Testo(sample,"12:45",26);
                ora.HorizontalAlignment=HorizontalAlignment.Center;sampleBody.Children.Add(ora);
                var firma=WidgetAspetto.Firma(sample,26);
                if(firma != null) sampleBody.Children.Add(firma);
                var visual=new Viewbox {Height=60,Stretch=Stretch.Uniform,Child=WidgetAspetto.Decora(sample,sampleBody,.5),Margin=new Thickness(0,0,0,8)};
                content.Children.Add(visual);
                var label=Tema.Scritta(nome,12,Tema.Bianco,FontWeights.SemiBold);
                label.HorizontalAlignment=HorizontalAlignment.Center;content.Children.Add(label);
                var selected=id;
                var card=Bottone(content,()=>{w.Aspetto=selected;Disegna();},WidgetAspetto.StileDi(w)==id);
                card.Padding=new Thickness(12);card.Margin=new Thickness(0,0,8,8);card.ToolTip=Suggerimento(descrizione);
                System.Windows.Automation.AutomationProperties.SetName(card,"Stile "+nome);
                stili.Children.Add(card);
            }
            col.Children.Add(stili);
            col.Children.Add(RigaPosizione(w, Disegna));
            col.Children.Add(Scelta("Schermi", new[] { ("Tutti", "tutti"), ("Principale", "principale") }, () => w.Schermi ?? "tutti", v => { w.Schermi = v; AggiornaAnteprima(); }));
            col.Children.Add(RigaNumero("Dimensione", () => w.Dimensione, v => { w.Dimensione = v; AggiornaAnteprima(); }, 6, 400, w.Tipo is "orologio" or "immagine" ? 8 : 2, v => v.ToString()));
            col.Children.Add(RigaNumero("Opacità", () => (int)Math.Round(w.Opacita * 100), v => { w.Opacita = v / 100.0; AggiornaAnteprima(); }, 10, 100, 5, v => v + "%"));
            if (w.Tipo != "immagine") col.Children.Add(RigaColoreLibera("Colore", () => w.Colore ?? "#FFFFFF", v => { w.Colore = v; AggiornaAnteprima(); }));
            if (w.Tipo == "orologio")
            {
                col.Children.Add(Interruttore("Secondi", () => w.Secondi, v => { w.Secondi = v; AggiornaAnteprima(); }));
                col.Children.Add(Interruttore("Firma sotto l'orologio", () => w.Firma, v => { w.Firma = v; AggiornaAnteprima(); }));
                var nomeRow=new Grid { Margin=new Thickness(8,10,0,8) };
                nomeRow.ColumnDefinitions.Add(new ColumnDefinition());
                nomeRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(280)});
                nomeRow.Children.Add(Tema.Scritta("Nome sotto l'orologio",12.5));
                var nome=CampoRepository(w.NomeFirma ?? "Vascend");
                nome.MaxLength=60;
                System.Windows.Automation.AutomationProperties.SetName(nome,"Nome sotto l'orologio");
                nome.TextChanged += (_,_)=>{w.NomeFirma=nome.Text;AggiornaAnteprima();};
                Grid.SetColumn(nome,1);nomeRow.Children.Add(nome);
                col.Children.Add(nomeRow);
            }
            if (w.Tipo == "testo") col.Children.Add(RigaTesto(w));
            if (w.Tipo == "immagine") col.Children.Add(RigaImmagine(w, Disegna));

            var azioni = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 14, 0, 0) };
            azioni.Children.Add(TastoBordato("\uE74D", "Rimuovi", () => { lista.RemoveAt(scelto); scelto = Math.Min(scelto, lista.Count - 1); Disegna(); }, true));
            col.Children.Add(azioni);
        }
        Disegna();
        Piede(() =>
        {
            var s = Stile.Attuale.Clona();
            s.Scrivania = stile.Clona().Scrivania;
            Esegui(("widget", () => { s.Salva(); return "widget aggiornati"; }));
        });
        return Scorri(col);
    }

    Tasto TastoBordato(string glifo, string testo, Action fai, bool rosso = false)
    {
        var st = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 14, 0) };
        var colore = rosso ? Tema.Pennello(255, 130, 118, 1) : Tema.Bianco;
        st.Children.Add(Tema.Glifo(glifo, 12, colore));
        var n = Tema.Scritta(testo, 12.5, colore);
        n.Margin = new Thickness(8, 0, 0, 1);
        st.Children.Add(n);
        return new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = rosso ? Tema.Pennello(255, 107, 94, .45) : Tema.Pennello(255, 255, 255, .22), Child = st }, double.NaN, 34)
        {
            Margin = new Thickness(0, 0, 6, 0), Sinistro = fai,
        };
    }

    // Lo schermo in miniatura coi widget al loro posto; un clic sceglie quello da modificare.
    FrameworkElement SchermoWidget(Action ridisegna)
    {
        var schermo = new Grid { Width = 560, Height = 315 };
        schermo.Children.Add(new Border { Background = new LinearGradientBrush(Color.FromRgb(46, 62, 82), Color.FromRgb(20, 26, 36), 60) });
        var lista = stile.Scrivania;
        foreach (var punto in Punti)
        {
            var qui = lista.Select((w, i) => (w, i)).Where(x => (x.w.Posizione ?? "centro") == punto).ToList();
            if (qui.Count == 0) continue;
            var allinea = punto.EndsWith("sinistra") ? HorizontalAlignment.Left : punto.EndsWith("destra") ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            var sp = new StackPanel
            {
                Margin = new Thickness(18), HorizontalAlignment = allinea,
                VerticalAlignment = punto.StartsWith("alto") ? VerticalAlignment.Top : punto.StartsWith("basso") ? VerticalAlignment.Bottom : VerticalAlignment.Center,
            };
            foreach (var (w, i) in qui)
            {
                var indice = i;
                var el = Miniatura(w);
                el.HorizontalAlignment = allinea;
                bool sel = i == scelto;
                var c = new Cornice { Profilo = Gradini.Minimo, Rilievo = false, Bordo = sel ? Tema.LimePennello : Tema.Pennello(255, 255, 255, .0), Child = new Border { Padding = new Thickness(6, 3, 6, 3), Child = el } };
                sp.Children.Add(new Tasto(c, double.NaN, double.NaN) { Acceso = sel, HorizontalAlignment = allinea, Margin = new Thickness(0, 2, 0, 2), Sinistro = () => { scelto = indice; ridisegna(); } });
            }
            schermo.Children.Add(sp);
        }
        return new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .2), Child = schermo, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 8, 0, 4) };
    }

    // Il widget in piccolo, a un quarto circa della sua misura vera.
    FrameworkElement Miniatura(Widget w)
    {
        double d = Math.Max(6, w.Dimensione) * .3;
        var colore = new SolidColorBrush(Stile.Colore(w.Colore ?? "#FFFFFF"));
        TextBlock T(string t, double dim, FontWeight? peso = null) { var x = WidgetAspetto.Testo(w,t,Math.Max(7,dim),peso); x.HorizontalAlignment = HorizontalAlignment.Center; return x; }
        FrameworkElement el;
        switch (w.Tipo)
        {
            case "orologio":
                var o = new StackPanel();
                o.Children.Add(T(DateTime.Now.ToString(w.Secondi ? "HH:mm:ss" : "HH:mm"), d));
                var firma = WidgetAspetto.Firma(w,d);
                if(firma != null) o.Children.Add(firma);
                el = o;
                break;
            case "data":
                var it = I18n.Culture;
                var sp = new StackPanel();
                sp.Children.Add(T(it.TextInfo.ToTitleCase(DateTime.Now.ToString("dddd", it)), d * .55));
                sp.Children.Add(T(DateTime.Now.ToString("d MMMM", it), d, FontWeights.SemiBold));
                el = sp;
                break;
            case "testo":
                el = T(string.IsNullOrEmpty(w.Testo) ? "…" : w.Testo, d, FontWeights.SemiBold);
                break;
            case "immagine":
                if (!string.IsNullOrEmpty(w.Immagine) && System.IO.File.Exists(w.Immagine))
                {
                    try
                    {
                        var b = new System.Windows.Media.Imaging.BitmapImage();
                        b.BeginInit(); b.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; b.UriSource = new Uri(w.Immagine); b.DecodePixelHeight = 120; b.EndInit();
                        el = new Image { Height = Math.Max(12, d), Source = b };
                        break;
                    }
                    catch { }
                }
                el = Tema.Glifo("\uE91B", Math.Max(12, d * .6), Tema.BiancoTenue);
                break;
            case "sistema":
                el = T("CPU 23%   RAM 41%", Math.Max(7, d * 1.4));
                break;
            default:
                var m = new StackPanel { Orientation = Orientation.Horizontal };
                m.Children.Add(new Border { Width = 18, Height = 18, Background = new LinearGradientBrush(Stile.Colore(stile.Accento), Stile.Colore(stile.Azzurro), 45), Margin = new Thickness(0, 0, 6, 0) });
                m.Children.Add(T("Brano in ascolto", Math.Max(7, d * 1.4), FontWeights.SemiBold));
                el = m;
                break;
        }
        el = WidgetAspetto.Decora(w,el,.3);
        el.Opacity = Math.Clamp(w.Opacita, .1, 1);
        return el;
    }

    // Nove punti dello schermo, come un tastierino.
    FrameworkElement RigaPosizione(Widget w, Action ridisegna)
    {
        var g = new Grid { Margin = new Thickness(8, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var n = Tema.Scritta("Posizione", 12.5);
        n.VerticalAlignment = VerticalAlignment.Top;
        n.Margin = new Thickness(0, 6, 0, 0);
        g.Children.Add(n);
        var griglia = new UniformGrid { Columns = 3, Rows = 3 };
        foreach (var p in Punti)
        {
            bool sel = (w.Posizione ?? "centro") == p;
            var punto = p;
            var c = new Cornice { Profilo = Gradini.Minimo, Rilievo = false, Fondo = sel ? Tema.LimePennello : Tema.Pennello(255, 255, 255, .08), Width = 34, Height = 22 };
            griglia.Children.Add(new Tasto(c, 38, 26) { Sinistro = () => { w.Posizione = punto; ridisegna(); } });
        }
        Grid.SetColumn(griglia, 1);
        g.Children.Add(griglia);
        return g;
    }

    FrameworkElement RigaTesto(Widget w)
    {
        var g = new Grid { Margin = new Thickness(8, 2, 0, 2), Height = 40 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Tema.Scritta("Testo", 12.5));
        var box = new TextBox
        {
            Width = 300, Text = w.Testo ?? "", Background = Tema.Pennello(255, 255, 255, .08), BorderThickness = new Thickness(0), Foreground = Tema.Bianco,
            CaretBrush = Tema.Bianco, FontFamily = Tema.Testo, FontSize = 13, Padding = new Thickness(8, 5, 8, 5), VerticalContentAlignment = VerticalAlignment.Center,
        };
        box.TextChanged += (_, _) => w.Testo = box.Text;
        Grid.SetColumn(box, 1);
        g.Children.Add(box);
        return g;
    }

    FrameworkElement RigaImmagine(Widget w, Action ridisegna)
    {
        var g = new Grid { Margin = new Thickness(8, 2, 0, 2), Height = 40 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var n = Tema.Scritta(string.IsNullOrEmpty(w.Immagine) ? "Nessuna immagine" : System.IO.Path.GetFileName(w.Immagine), 12.5, string.IsNullOrEmpty(w.Immagine) ? Tema.BiancoTenue : Tema.Bianco);
        n.TextTrimming = TextTrimming.CharacterEllipsis;
        g.Children.Add(n);
        var t = TastoBordato("\uE8E5", "Scegli", () =>
        {
            var d = new Microsoft.Win32.OpenFileDialog { Filter = I18n.Translate("Immagini|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp"), InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) };
            if (d.ShowDialog(this) != true) return;
            w.Immagine = d.FileName;
            ridisegna();
        });
        Grid.SetColumn(t, 1);
        g.Children.Add(t);
        return g;
    }

    // ---------- Windows ----------

    FrameworkElement SezioneWindows()
    {
        var col = new StackPanel { MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
        col.Children.Add(Titolo("Tema"));
        col.Children.Add(Scelta("App", new[] { ("Scure", true), ("Chiare", false) }, () => Sistema.AppScure, v => Sistema.AppScure = v));
        col.Children.Add(Scelta("Windows", new[] { ("Scuro", true), ("Chiaro", false) }, () => Sistema.SistemaScuro, v => Sistema.SistemaScuro = v));
        col.Children.Add(Interruttore("Effetti di trasparenza", () => Sistema.Trasparenza, v => Sistema.Trasparenza = v));
        col.Children.Add(Interruttore("Animazioni delle finestre", () => Sistema.Animazioni, v => Sistema.Animazioni = v));

        col.Children.Add(Titolo("Colore d'accento"));
        string accentoWin = Sistema.ColoreAccento;
        col.Children.Add(RigaColoreLibera("Accento di Windows", () => accentoWin, v => { accentoWin = v; Sistema.ImpostaAccentoPiano(v); }));
        var usa = Tema.Scritta("Usa l'accento Vascend", 12.5);
        usa.Margin = new Thickness(12, 0, 12, 0);
        col.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .22), Child = usa }, double.NaN, 34)
        {
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(8, 6, 0, 4),
            Sinistro = () => { Sistema.ImpostaAccento(Stile.Attuale.Accento); Apri("Windows"); Esito("accento di Windows aggiornato"); },
        });
        col.Children.Add(Interruttore("Accento su barre del titolo e bordi", () => Sistema.AccentoSuTitoli, v => Sistema.AccentoSuTitoli = v));

        col.Children.Add(Titolo("Sfondo"));
        col.Children.Add(RigaSfondo());
        return Scorri(col);
    }

    FrameworkElement RigaSfondo()
    {
        var g = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 0, 0) };
        var img = new Image { Width = 240, Height = 135, Stretch = Stretch.UniformToFill };
        void Carica()
        {
            try
            {
                var p = Sistema.Sfondo;
                if (p == null) return;
                var b = new System.Windows.Media.Imaging.BitmapImage();
                b.BeginInit(); b.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; b.DecodePixelWidth = 480;
                b.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                b.UriSource = new Uri(p); b.EndInit(); img.Source = b;
            }
            catch { }
        }
        Carica();
        g.Children.Add(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .2), Child = new Border { ClipToBounds = true, Child = img, Margin = new Thickness(2) } });
        var scegli = Tema.Scritta("Scegli un'immagine", 12.5);
        scegli.Margin = new Thickness(12, 0, 12, 0);
        g.Children.Add(new Tasto(new Cornice { Profilo = Gradini.Piccolo, Rilievo = false, Bordo = Tema.Pennello(255, 255, 255, .22), Child = scegli }, double.NaN, 34)
        {
            VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(14, 0, 0, 0),
            Sinistro = () =>
            {
                var d = new Microsoft.Win32.OpenFileDialog { Filter = I18n.Translate("Immagini|*.jpg;*.jpeg;*.png;*.bmp;*.webp"), InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) };
                if (d.ShowDialog(this) != true) return;
                Sistema.ImpostaSfondo(d.FileName);
                Carica();
                Esito("sfondo cambiato");
            },
        });
        return g;
    }

    // ---------- VS Code ----------

    FrameworkElement SezioneIde()
    {
        var g = DueColonne(out var col, out var destra);
        stile.IdeDestinazioni ??= new();
        bool alcuno = false;
        col.Children.Add(Titolo("Installazioni"));
        for (int i = 0; i < Generatori.Ide.Length; i++)
        {
            if (!Generatori.IdePresente(i)) continue;
            alcuno = true;
            var nome = Generatori.Ide[i].nome;
            col.Children.Add(Interruttore(nome, () => stile.IdeDestinazioni.Contains(nome), v => { if (v) stile.IdeDestinazioni.Add(nome); else stile.IdeDestinazioni.Remove(nome); }));
        }
        col.Children.Add(Titolo("Tema"));
        col.Children.Add(Interruttore("Tema Vascend", () => stile.IdeTema, v => { stile.IdeTema = v; Presto(); }));
        col.Children.Add(Titolo("Caratteri"));
        col.Children.Add(RigaFont(() => string.IsNullOrEmpty(stile.IdeFont) ? stile.FontCodice : stile.IdeFont, v => { stile.IdeFont = v; Presto(); }));
        col.Children.Add(RigaNumero("Dimensione", () => stile.IdeDim, v => stile.IdeDim = v, 9, 28, 1, v => v + " pt"));
        col.Children.Add(Titolo("Horizon"));
        col.Children.Add(Interruttore("Agenti nell'isola di Vascend Engine", () => stile.IdeIsola, v => stile.IdeIsola = v));
        bool prestazioni = stile.IdePrestazioni ?? Enumerable.Range(0, Generatori.Ide.Length).Select(i => Generatori.LeggiIde(i, "horizon.performanceMode")).FirstOrDefault(x => x != null) ?? false;
        col.Children.Add(Interruttore("Modalità prestazioni", () => prestazioni, v => { prestazioni = v; stile.IdePrestazioni = v; }));
        aggiornaAnteprima = () => destra.Child = Anteprima.VsCode(stile, string.IsNullOrEmpty(stile.IdeFont) ? null : new FontFamily(stile.IdeFont + ", Consolas"));
        aggiornaAnteprima();
        Piede(alcuno ? () =>
        {
            var s = Stile.Attuale.Clona();
            s.IdeDestinazioni = new List<string>(stile.IdeDestinazioni);
            s.IdeTema = stile.IdeTema; s.IdeFont = stile.IdeFont; s.IdeDim = stile.IdeDim; s.IdePrestazioni = stile.IdePrestazioni; s.IdeIsola = stile.IdeIsola;
            // con l'isola di Vascend Engine il modulo dell'IDE entra fra quelli dell'isola
            if (s.IdeIsola && !s.IsolaModuli.Contains("ide")) { s.IsolaModuli.Add("ide"); stile.IsolaModuli = new List<string>(s.IsolaModuli); }
            Esegui(("Vascend IDE", () => { s.Salva(); return Generatori.ApplicaIde(s); }));
        } : null);
        if (!alcuno) Esito("Vascend IDE non trovato", true);
        return g;
    }

    FrameworkElement SezioneVsCode()
    {
        var g = DueColonne(out var col, out var destra);
        col.Children.Add(Titolo("Caratteri"));
        col.Children.Add(RigaFont());
        col.Children.Add(RigaNumero("Dimensione", () => stile.DimEditor, v => stile.DimEditor = v, 9, 28, 1, v => v + " pt"));
        aggiornaAnteprima = () => destra.Child = Anteprima.VsCode(stile);
        aggiornaAnteprima();
        var esporta = new Tasto(Tema.Glifo("", 13), 40, 38) { ToolTip = Suggerimento("Esporta il tema VS Code (.vsix)"), Sinistro = Esporta };
        Piede(Generatori.VsCodePresente ? () =>
        {
            var s = Stile.Attuale.Clona();
            s.FontCodice = stile.FontCodice; s.DimEditor = stile.DimEditor;
            Esegui(("VS Code", () => { s.Salva(); return Generatori.ApplicaVsCode(s); }));
        } : null, "Applica", esporta);
        if (!Generatori.VsCodePresente) Esito("VS Code non trovato", true);
        return g;
    }

    // ---------- Terminali ----------

    FrameworkElement SezioneTerminali()
    {
        var g = DueColonne(out var col, out var destra);
        col.Children.Add(Titolo("Caratteri"));
        col.Children.Add(RigaFont(stile.FontDelTerminale, v => stile.FontTerminale = v));
        col.Children.Add(RigaNumero("Dimensione", () => stile.DimTerminale, v => stile.DimTerminale = v, 8, 24, 1, v => v + " pt"));
        col.Children.Add(Titolo("Aspetto"));
        col.Children.Add(RigaNumero("Opacità", () => stile.Acrilico, v => stile.Acrilico = v, 50, 100, 5, v => v + "%"));
        col.Children.Add(Scelta("Cursore", new[] { ("Blocco", "filledBox"), ("Barra", "bar"), ("Linea", "underscore"), ("Vuoto", "emptyBox") }, () => stile.Cursore ?? "filledBox", v => { stile.Cursore = v; Presto(); }));
        aggiornaAnteprima = () => destra.Child = Anteprima.Terminale(stile);
        aggiornaAnteprima();
        Piede(Generatori.TerminalePresente ? () =>
        {
            var s = Stile.Attuale.Clona();
            s.FontTerminale = stile.FontTerminale; s.DimTerminale = stile.DimTerminale; s.Acrilico = stile.Acrilico; s.Cursore = stile.Cursore;
            Esegui(("Terminali", () => { s.Salva(); return Generatori.ApplicaTerminale(s); }));
        } : null);
        if (!Generatori.TerminalePresente) Esito("Windows Terminal non trovato", true);
        return g;
    }

    Grid DueColonne(out StackPanel sinistra, out Border destra)
    {
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(320) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        sinistra = new StackPanel();
        g.Children.Add(Scorri(sinistra));
        destra = new Border { Margin = new Thickness(16) };
        var c = new Cornice
        {
            Profilo = Gradini.Barra, Bordo = Tema.Pennello(235, 246, 255, .14), Child = destra,
            Fondo = new LinearGradientBrush(Color.FromArgb(120, 38, 52, 70), Color.FromArgb(120, 14, 18, 26), 60),
        };
        Grid.SetColumn(c, 2);
        g.Children.Add(c);
        return g;
    }

    // ---------- controlli ----------

    static ScrollViewer Scorri(UIElement c) => new() { Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false, Padding = new Thickness(0, 0, 0, 12) };

    // Nome dell'opzione con accanto la scenetta che mostra cosa fa.
    FrameworkElement Etichetta(string nome, Brush colore = null, string chiave = null)
    {
        var testo = Tema.Scritta(nome, 12.5, colore);
        testo.VerticalAlignment = VerticalAlignment.Center;
        var vignetta = Vignette.Per(sezione, chiave ?? nome);
        if (vignetta == null) return testo;
        var st = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        st.Children.Add(vignetta);
        st.Children.Add(testo);
        return st;
    }

    static FrameworkElement Titolo(string t)
    {
        var s = Tema.Scritta(t, 11.5, Tema.BiancoTenue, FontWeights.SemiBold);
        s.Margin = new Thickness(8, 18, 0, 6);
        return s;
    }

    // Interruttore a pixel: si applica subito.
    FrameworkElement Interruttore(string nome, Func<bool> leggi, Action<bool> scrivi)
    {
        bool v;
        try { v = leggi(); } catch { v = false; }
        var g = new Grid { Margin = new Thickness(8, 0, 8, 0), Height = 38 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Etichetta(nome));
        var pomello = new Border { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(4, 0, 0, 0) };
        var sposta = new TranslateTransform(v ? 18 : 0, 0);
        pomello.RenderTransform = sposta;
        var binario = new Cornice { Profilo = Gradini.Minimo, Width = 38, Height = 20, Rilievo = false, Child = pomello, VerticalAlignment = VerticalAlignment.Center };
        void Mostra()
        {
            binario.Fondo = v ? Tema.LimePennello : Tema.Pennello(255, 255, 255, .12);
            binario.Bordo = v ? null : Tema.Pennello(255, 255, 255, .3);
            pomello.Background = v ? Tema.Pennello(20, 28, 36, 1) : Tema.Pennello(255, 255, 255, .75);
            sposta.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(v ? 18 : 0, TimeSpan.FromMilliseconds(160)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        Mostra();
        Grid.SetColumn(binario, 1);
        g.Children.Add(binario);
        return new Tasto(g, double.NaN, 40)
        {
            Margin = new Thickness(0, 1, 0, 1),
            Sinistro = () =>
            {
                try { scrivi(!v); v = !v; Mostra(); }
                catch (Exception e) { Esito(e.Message, true); }
            },
        };
    }

    // Scelta fra poche opzioni, a tasselli affiancati.
    FrameworkElement Scelta<T>(string nome, (string etichetta, T valore)[] opzioni, Func<T> leggi, Action<T> scrivi)
    {
        var g = new Grid { Margin = new Thickness(8, 1, 0, 1), Height = 40 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Etichetta(nome));
        var st = new StackPanel { Orientation = Orientation.Horizontal };
        void Disegna()
        {
            st.Children.Clear();
            T ora;
            try { ora = leggi(); } catch { ora = default; }
            foreach (var (etichetta, valore) in opzioni)
            {
                bool sel = EqualityComparer<T>.Default.Equals(valore, ora);
                var s = Tema.Scritta(etichetta, 12, sel ? Tema.Pennello(20, 28, 36, 1) : Tema.Bianco, sel ? FontWeights.SemiBold : FontWeights.Normal);
                s.Margin = new Thickness(12, 0, 12, 1);
                var c = new Cornice { Profilo = Gradini.Minimo, Rilievo = false, Fondo = sel ? Tema.LimePennello : Tema.Pennello(255, 255, 255, .08), Child = s };
                var val = valore;
                st.Children.Add(new Tasto(c, double.NaN, 30)
                {
                    Margin = new Thickness(2, 0, 0, 0),
                    Sinistro = () => { try { scrivi(val); } catch (Exception e) { Esito(e.Message, true); } Disegna(); },
                });
            }
        }
        Disegna();
        Grid.SetColumn(st, 1);
        g.Children.Add(st);
        return g;
    }

    FrameworkElement RigaNumero(string nome, Func<int> leggi, Action<int> scrivi, int min, int max, int passo, Func<int, string> mostra)
    {
        var g = new Grid { Margin = new Thickness(8, 1, 0, 1), Height = 36 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.Children.Add(Etichetta(nome));
        var st = new StackPanel { Orientation = Orientation.Horizontal };
        var valore = Tema.Scritta(mostra(leggi()), 12, Tema.Bianco, FontWeights.SemiBold);
        valore.Width = 56;
        valore.TextAlignment = TextAlignment.Center;
        void Cambia(int d) { scrivi(Math.Clamp(leggi() + d, min, max)); valore.Text = mostra(leggi()); Presto(); }
        st.Children.Add(new Tasto(Tema.Glifo("", 10), 30, 28) { Sinistro = () => Cambia(-passo) });
        st.Children.Add(valore);
        st.Children.Add(new Tasto(Tema.Glifo("", 10), 30, 28) { Sinistro = () => Cambia(passo) });
        g.MouseWheel += (_, e) => Cambia(e.Delta > 0 ? passo : -passo);
        Grid.SetColumn(st, 1);
        g.Children.Add(st);
        return g;
    }

    FrameworkElement RigaFont() => RigaFont(() => stile.FontCodice, v => stile.FontCodice = v);

    FrameworkElement RigaFont(Func<string> leggi, Action<string> scrivi)
    {
        var g = new Grid { Margin = new Thickness(8, 0, 8, 0) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var n = Tema.Scritta(leggi(), 12.5);
        n.FontFamily = new FontFamily(leggi() + ", Consolas");
        n.TextTrimming = TextTrimming.CharacterEllipsis;
        g.Children.Add(n);
        var giu = Tema.Glifo("", 9, Tema.BiancoTenue);
        Grid.SetColumn(giu, 1);
        g.Children.Add(giu);
        var t = new Tasto(g, double.NaN, 36) { Margin = new Thickness(0, 1, 0, 1) };
        t.Sinistro = () =>
        {
            var lista = new StackPanel();
            var p = new Popup { StaysOpen = false, AllowsTransparency = true, PlacementTarget = t, Placement = PlacementMode.Right, HorizontalOffset = 10 };
            void Usa(string nome) { if(nome == FontInclusi.Famiglia)FontInclusi.Installa(); scrivi(nome); p.IsOpen = false; n.Text = nome; n.FontFamily = new FontFamily(nome + ", Consolas"); Presto(); }
            var cerca = new TextBox
            {
                Background = Tema.Pennello(255, 255, 255, .08), BorderThickness = new Thickness(0), Foreground = Tema.Bianco, CaretBrush = Tema.Bianco,
                FontFamily = Tema.Testo, FontSize = 12.5, Padding = new Thickness(8, 5, 8, 5), VerticalContentAlignment = VerticalAlignment.Center,
            };
            var daFile = new Tasto(Tema.Glifo("\uE8E5", 13, Tema.Bianco), 34, 30) { Margin = new Thickness(6, 0, 0, 0), ToolTip = Suggerimento("Installa da file") };
            daFile.Sinistro = () =>
            {
                p.IsOpen = false;
                var d = new Microsoft.Win32.OpenFileDialog { Filter = "Font|*.ttf;*.otf;*.ttc" };
                if (d.ShowDialog() != true) return;
                try { Usa(Sistema.InstallaFont(d.FileName)); }
                catch (Exception e) { Esito("Font non installato: " + e.Message, true); }
            };
            var testa = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            testa.ColumnDefinitions.Add(new ColumnDefinition());
            testa.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            testa.Children.Add(cerca);
            Grid.SetColumn(daFile, 1);
            testa.Children.Add(daFile);
            var tutti = Fonts.SystemFontFamilies.Select(f => f.Source).Append(FontInclusi.Famiglia).Distinct().OrderBy(f => f).ToList();
            var mono = FontMonospazio();
            void Elenca()
            {
                lista.Children.Clear();
                var q = cerca.Text.Trim();
                var voci = (q.Length == 0 ? mono : tutti.Where(f => f.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(80)).Prepend(FontInclusi.Famiglia).Where(f=>q.Length == 0 || f.Contains(q,StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
                foreach (var f in voci)
                {
                    var s = Tema.Scritta(f == FontInclusi.Famiglia ? f+" · Incluso" : f, 12.5);
                    s.FontFamily = f == FontInclusi.Famiglia ? FontInclusi.Anteprima : new FontFamily(f);
                    s.Margin = new Thickness(10, 0, 10, 0);
                    var nome = f;
                    lista.Children.Add(new Tasto(s, double.NaN, 30) { Acceso = f == leggi(), HorizontalAlignment = HorizontalAlignment.Stretch, Sinistro = () => Usa(nome) });
                }
            }
            cerca.TextChanged += (_, _) => Elenca();
            cerca.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter || cerca.Text.Trim().Length == 0) return;
                var q = cerca.Text.Trim();
                Usa(tutti.FirstOrDefault(f => f.Equals(q, StringComparison.OrdinalIgnoreCase)) ?? q);
            };
            Elenca();
            var corpo = new DockPanel { Width = 290, Margin = new Thickness(6) };
            DockPanel.SetDock(testa, Dock.Top);
            corpo.Children.Add(testa);
            corpo.Children.Add(new ScrollViewer { Content = lista, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden });
            p.Child = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.Pennello(24, 34, 44, .98), Bordo = Tema.Bordo, Child = corpo };
            p.Opened += (_, _) => cerca.Focus();
            p.IsOpen = true;
        };
        return t;
    }

    static List<string> FontMonospazio()
    {
        var chiavi = new[] { "Mono", "Code", "Consolas", "Courier", "Nerd", "Fira", "Hack", "Iosevka", "Menlo", "Source Code" };
        return Fonts.SystemFontFamilies.Select(f => f.Source).Where(n => chiavi.Any(k => n.Contains(k, StringComparison.OrdinalIgnoreCase)))
            .Distinct().OrderBy(n => n).ToList();
    }
}
