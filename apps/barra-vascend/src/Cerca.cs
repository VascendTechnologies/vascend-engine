using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace BarraVascend;

// Elenco delle app installate (anche quelle dello Store) e quante volte le hai aperte.
static class Indice
{
    public class Voce { public string Nome, Percorso; }
    public static List<Voce> App = new();
    static Dictionary<string, int> usi = new();
    static string FileUsi => Path.Combine(BarraVascend.App.Cartella, "cerca-usi.txt");

    public static void Carica()
    {
        try
        {
            foreach (var r in File.ReadAllLines(FileUsi))
            {
                var p = r.Split('\t');
                if (p.Length == 2 && int.TryParse(p[0], out var n)) usi[p[1]] = n;
            }
        }
        catch { }
        var t = new Thread(() =>
        {
            try
            {
                var l = new List<Voce>();
                dynamic sh = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
                dynamic f = sh.NameSpace("shell:AppsFolder");
                foreach (dynamic it in f.Items())
                {
                    string nome = it.Name, p = it.Path;
                    if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(p)) continue;
                    if (p.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                    l.Add(new Voce { Nome = nome, Percorso = p });
                }
                App = l;
            }
            catch (Exception e) { BarraVascend.App.Log("indice app: " + e.Message); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    public static int Usi(string p) => usi.TryGetValue(p, out var n) ? n : 0;

    public static void Usata(string p)
    {
        usi[p] = Usi(p) + 1;
        try { File.WriteAllLines(FileUsi, usi.Select(x => $"{x.Value}\t{x.Key}")); } catch { }
    }
}

// Launcher con Alt+Spazio: app, calcoli (=), ricerca sul web (?).
class Cerca : Window, IFinestraPixel
{
    class Risultato { public string Titolo, Sotto, Glifo; public ImageSource Icona; public Action Azione; }

    readonly TextBox campo;
    readonly StackPanel elenco = new();
    List<Risultato> risultati = new();
    int scelto;
    double scala = 1;
    int unitaPx = 2;
    IntPtr hwnd;
    readonly Schermo schermo;
    bool chiuso;

    public double UnitaDip => unitaPx / scala;

    public Cerca(Schermo s)
    {
        schermo = s;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Transparent;
        SizeToContent = SizeToContent.Height;
        Width = 640;
        UseLayoutRounding = true;
        Title = "BarraVascend cerca";
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

        var c = new StackPanel { Margin = new Thickness(14) };
        var riga = new Grid { Margin = new Thickness(6, 2, 6, 6) };
        riga.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        riga.ColumnDefinitions.Add(new ColumnDefinition());
        var lente = Tema.Glifo("", 17, Tema.BiancoTenue);
        lente.HorizontalAlignment = HorizontalAlignment.Left;
        riga.Children.Add(lente);
        campo = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Tema.Bianco,
            CaretBrush = Tema.LimePennello,
            SelectionBrush = Tema.LimePennello,
            FontFamily = Tema.Testo,
            FontSize = 19,
            VerticalContentAlignment = VerticalAlignment.Center,
            Height = 36,
        };
        var segnaposto = Tema.Scritta("Cerca app, calcola con =, web con ?", 19, Tema.Pennello(255, 255, 255, .38));
        segnaposto.IsHitTestVisible = false;
        segnaposto.Margin = new Thickness(2, 0, 0, 0);
        Grid.SetColumn(campo, 1);
        Grid.SetColumn(segnaposto, 1);
        riga.Children.Add(segnaposto);
        riga.Children.Add(campo);
        c.Children.Add(riga);
        c.Children.Add(elenco);
        Content = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.GhiaccioPannello, Bordo = Tema.Bordo, Child = c };

        campo.TextChanged += (_, _) => { segnaposto.Visibility = campo.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Filtra(); };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Chiudi(); e.Handled = true; }
            else if (e.Key == Key.Down) { Scegli(scelto + 1); e.Handled = true; }
            else if (e.Key == Key.Up) { Scegli(scelto - 1); e.Handled = true; }
            else if (e.Key == Key.Enter) { Lancia(scelto); e.Handled = true; }
        };
        Deactivated += (_, _) => Chiudi();
        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            var src = HwndSource.FromHwnd(hwnd);
            src.CompositionTarget.BackgroundColor = Colors.Transparent;
            src.AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool g) => { if (m == N.WM_DPICHANGED) Dispatcher.BeginInvoke(Posiziona); return IntPtr.Zero; });
            N.StileStrumento(hwnd, false);
            N.Vetro(hwnd);
            N.SetWindowRgn(hwnd, N.CreateRectRgn(0, 0, 0, 0), false);
        };
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(Posiziona);
        Filtra();
    }

    public static Cerca Aperta;

    public static void Apri(Schermo s)
    {
        if (Aperta != null && !Aperta.chiuso) { Aperta.Chiudi(); return; }
        App.Io.Pannello?.Chiudi();
        var c = new Cerca(s);
        Aperta = c;
        new WindowInteropHelper(c).EnsureHandle();
        var a = s.Area;
        N.SetWindowPos(c.hwnd, N.HWND_TOPMOST, a.L + a.W / 3, a.T + a.H / 4, 0, 0, N.SWP_NOSIZE | N.SWP_NOACTIVATE);
        c.Show();
        N.SetForegroundWindow(c.hwnd);
        c.Activate();
        // entrata: il contenuto scende appena e sfuma dentro
        if (c.Content is Cornice cc && cc.Child is UIElement dentro)
        {
            var sp = new TranslateTransform(0, -10);
            dentro.RenderTransform = sp;
            var e = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            sp.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(260)) { EasingFunction = e });
            dentro.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200)));
        }
        c.campo.Focus();
        Keyboard.Focus(c.campo);
        c.Posiziona();
    }

    void Chiudi()
    {
        if (chiuso) return;
        chiuso = true;
        try { Close(); } catch { }
    }

    void Posiziona()
    {
        if (chiuso || hwnd == IntPtr.Zero) return;
        N.GetDpiForMonitor(schermo.H, 0, out uint dx, out _);
        scala = dx / 96.0;
        unitaPx = Math.Max(2, (int)Math.Round(2 * scala, MidpointRounding.AwayFromZero));
        N.GetWindowRect(hwnd, out var r);
        var a = schermo.Area;
        N.SetWindowPos(hwnd, N.HWND_TOPMOST, a.L + (a.W - r.W) / 2, a.T + (int)(a.H * .22), 0, 0, N.SWP_NOSIZE | N.SWP_NOACTIVATE);
        N.SetWindowRgn(hwnd, Gradini.Regione(r.W, r.H, unitaPx, Gradini.Barra), true);
        Pannello.Ridisegna(this);
    }

    // Punteggio: inizio del nome, inizio di una parola, contenuto, lettere in ordine; poi quante volte l'hai aperta.
    public static int Punteggio(string nome, string q)
    {
        var n = nome.ToLowerInvariant();
        if (n == q) return 1000;
        if (n.StartsWith(q)) return 800;
        if (n.Split(' ', '-', '.', '(').Any(p => p.StartsWith(q))) return 600;
        if (n.Contains(q)) return 400;
        int i = 0;
        foreach (var ch in n) if (i < q.Length && ch == q[i]) i++;
        return i == q.Length && q.Length >= 2 ? 150 : 0;
    }

    void Filtra()
    {
        var q = campo.Text.Trim();
        var r = new List<Risultato>();
        string ql = q.ToLowerInvariant();
        if (q.StartsWith("=") || (q.Length > 1 && q.Any(char.IsDigit) && q.All(ch => "0123456789+-*/().,% ".Contains(ch))))
        {
            var esito = Calcola(q.TrimStart('='));
            if (esito != null) r.Add(new Risultato { Titolo = esito, Sotto = "Copia il risultato", Glifo = "", Icona = Contenuti.Ico.Calcolatrice, Azione = () => Clipboard.SetText(esito) });
        }
        if (q.StartsWith("?") && q.Length > 1)
        {
            var w = q[1..].Trim();
            r.Add(Web(w));
        }
        else
        {
            var app = Indice.App
                .Select(a => (a, p: q.Length == 0 ? Indice.Usi(a.Percorso) > 0 ? 1 : 0 : Punteggio(a.Nome, ql)))
                .Where(x => x.p > 0)
                .OrderByDescending(x => x.p + Math.Min(300, Indice.Usi(x.a.Percorso) * 25))
                .ThenBy(x => x.a.Nome)
                .Take(q.Length == 0 ? 6 : 7);
            foreach (var (a, _) in app)
            {
                var v = a;
                r.Add(new Risultato { Titolo = v.Nome, Sotto = "App", Icona = Shell.Icona(@"shell:AppsFolder\" + v.Percorso, 48), Azione = () => { Indice.Usata(v.Percorso); Process.Start("explorer.exe", @"shell:AppsFolder\" + v.Percorso); } });
            }
            if (q.Length > 0) r.Add(Web(q));
        }
        risultati = r;
        Disegna();
        Scegli(0);
    }

    static Risultato Web(string q) => new()
    {
        Titolo = $"Cerca «{q}» sul web",
        Sotto = "Google",
        Glifo = "",
        Icona = Contenuti.Ico.Browser,
        Azione = () => Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(q)) { UseShellExecute = true }),
    };

    static string Calcola(string e)
    {
        try
        {
            var v = new System.Data.DataTable().Compute(e.Replace(",", ".").Replace("%", "/100"), null);
            double d = Convert.ToDouble(v);
            if (double.IsNaN(d) || double.IsInfinity(d)) return null;
            return d.ToString("0.##########", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
        }
        catch { return null; }
    }

    void Disegna()
    {
        elenco.Children.Clear();
        if (risultati.Count > 0) elenco.Children.Add(new Border { Height = 1, Background = Tema.Separatore, Margin = new Thickness(4, 2, 4, 8) });
        for (int i = 0; i < risultati.Count; i++)
        {
            var x = risultati[i];
            var g = new Grid { Margin = new Thickness(10, 0, 12, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (x.Icona != null)
            {
                var img = new Image { Source = x.Icona, Width = 26, Height = 26, HorizontalAlignment = HorizontalAlignment.Left };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                g.Children.Add(img);
            }
            else
            {
                var gl = Tema.Glifo(x.Glifo ?? "", 17, Tema.LimePennello);
                gl.HorizontalAlignment = HorizontalAlignment.Left;
                gl.Margin = new Thickness(4, 0, 0, 0);
                g.Children.Add(gl);
            }
            var t = Tema.Scritta(x.Titolo, 14.5);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(t, 1);
            g.Children.Add(t);
            var s = Tema.Scritta(x.Sotto, 11.5, Tema.BiancoTenue);
            Grid.SetColumn(s, 2);
            g.Children.Add(s);
            int k = i;
            var tasto = new Tasto(g, double.NaN, 44) { Margin = new Thickness(0), Sinistro = () => Lancia(k) };
            tasto.MouseEnter += (_, _) => Scegli(k);
            elenco.Children.Add(tasto);
        }
    }

    void Scegli(int i)
    {
        if (risultati.Count == 0) return;
        scelto = (i + risultati.Count) % risultati.Count;
        int k = 0;
        foreach (var c in elenco.Children)
            if (c is Tasto t) t.Acceso = k++ == scelto;
    }

    void Lancia(int i)
    {
        if (i < 0 || i >= risultati.Count) return;
        var a = risultati[i].Azione;
        Chiudi();
        try { a?.Invoke(); } catch (Exception e) { App.Log("cerca: " + e.Message); }
    }
}
