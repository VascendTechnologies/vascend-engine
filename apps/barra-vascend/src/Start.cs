using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BarraVascend;

// Menu Start al posto di quello di Windows: ricerca, preferiti, recenti, tutte le app, profilo e spegnimento.
// Si apre dal tasto della volpe e dal tasto Win premuto da solo.
static class Start
{
    const double Largo = 600, Alto = 620;
    static string FilePreferiti => Path.Combine(App.Cartella, "start.txt");

    // I preferiti partono da quelli che avevi nello Start di Windows.
    static readonly string[] Iniziali = { "Visual Studio Code", "Google Chrome", "Microsoft Store", "KeePass", "OpenVPN GUI", "Impostazioni", "Foto", "Calcolatrice", "Orologio", "Blocco note", "Strumento di cattura", "Esplora file" };

    static List<string> Preferiti()
    {
        try { if (File.Exists(FilePreferiti)) return File.ReadAllLines(FilePreferiti).Where(r => r.Length > 0).ToList(); } catch { }
        var l = new List<string>();
        foreach (var n in Iniziali)
        {
            var v = Indice.App.FirstOrDefault(a => a.Nome.Equals(n, StringComparison.OrdinalIgnoreCase))
                 ?? Indice.App.FirstOrDefault(a => a.Nome.StartsWith(n, StringComparison.OrdinalIgnoreCase));
            if (v != null && !l.Contains(v.Percorso)) l.Add(v.Percorso);
        }
        if (l.Count > 0) Salva(l);
        return l;
    }

    static void Salva(List<string> l) { try { File.WriteAllLines(FilePreferiti, l); } catch { } }

    static void Lancia(Indice.Voce v)
    {
        App.Io.Pannello?.Chiudi();
        Indice.Usata(v.Percorso);
        try { Process.Start("explorer.exe", @"shell:AppsFolder\" + v.Percorso); } catch (Exception e) { App.Log("start: " + e.Message); }
    }

    static void Apri(string cosa, string arg = null)
    {
        App.Io.Pannello?.Chiudi();
        try { Process.Start(new ProcessStartInfo(cosa) { UseShellExecute = true, Arguments = arg ?? "" }); }
        catch (Exception e) { App.Log("start " + cosa + ": " + e.Message); }
    }

    static Image Icona(ImageSource s, double lato)
    {
        var i = new Image { Source = s, Width = lato, Height = lato };
        RenderOptions.SetBitmapScalingMode(i, BitmapScalingMode.HighQuality);
        return i;
    }

    // Le icone si caricano un po' alla volta, così il menu si apre subito anche con duecento app.
    static void IconaDopo(Image img, string percorso, int lato = 48)
    {
        img.Dispatcher.BeginInvoke(() => img.Source = Shell.Icona(@"shell:AppsFolder\" + percorso, lato), DispatcherPriority.Background);
    }

    static TextBlock Sezione(string t)
    {
        var s = Tema.Scritta(t, 13, Tema.Bianco, FontWeights.SemiBold);
        s.VerticalAlignment = VerticalAlignment.Center;
        return s;
    }

    static Tasto Link(string testo, string glifo, Action a)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 8, 0) };
        sp.Children.Add(Tema.Scritta(testo, 12, Tema.BiancoTenue));
        var g = Tema.Glifo(glifo, 10, Tema.BiancoTenue);
        g.Margin = new Thickness(8, 1, 0, 0);
        sp.Children.Add(g);
        return new Tasto(sp, double.NaN, 28) { Sinistro = a, Margin = new Thickness(0) };
    }

    static Grid Testata(string titolo, Tasto destra)
    {
        var g = new Grid { Margin = new Thickness(10, 0, 0, 8) };
        g.Children.Add(Sezione(titolo));
        if (destra != null) { destra.HorizontalAlignment = HorizontalAlignment.Right; g.Children.Add(destra); }
        return g;
    }

    public static UIElement Crea()
    {
        var radice = new Grid { Width = Largo };
        radice.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        radice.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        radice.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ricerca
        var campo = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Tema.Bianco,
            CaretBrush = Tema.LimePennello,
            SelectionBrush = Tema.LimePennello,
            FontFamily = Tema.Testo,
            FontSize = 15,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        var segnaposto = Tema.Scritta("Cerca app, calcola con =, web con ?", 15, Tema.Pennello(255, 255, 255, .38));
        segnaposto.IsHitTestVisible = false;
        segnaposto.Margin = new Thickness(2, 0, 0, 0);
        segnaposto.VerticalAlignment = VerticalAlignment.Center;
        var rigaCampo = new Grid { Margin = new Thickness(12, 0, 12, 0) };
        rigaCampo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        rigaCampo.ColumnDefinitions.Add(new ColumnDefinition());
        var lente = Tema.Glifo("", 15, Tema.LimePennello);
        lente.HorizontalAlignment = HorizontalAlignment.Left;
        rigaCampo.Children.Add(lente);
        Grid.SetColumn(campo, 1);
        Grid.SetColumn(segnaposto, 1);
        rigaCampo.Children.Add(segnaposto);
        rigaCampo.Children.Add(campo);
        var box = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Pennello(255, 255, 255, .09), Bordo = Tema.Pennello(255, 255, 255, .20), Child = rigaCampo, Height = 42, Margin = new Thickness(20, 20, 20, 10) };
        radice.Children.Add(box);

        // corpo: tre viste che si scambiano con una dissolvenza
        var corpo = new Grid { Margin = new Thickness(14, 6, 14, 14) };
        Grid.SetRow(corpo, 1);
        radice.Children.Add(corpo);
        var home = Home(out var vaiTutte);
        var tutte = new Grid { Visibility = Visibility.Collapsed, Height = Alto - 150 };
        var risultati = new StackPanel { Visibility = Visibility.Collapsed };
        corpo.Children.Add(home);
        corpo.Children.Add(tutte);
        corpo.Children.Add(risultati);

        void Mostra(FrameworkElement v)
        {
            foreach (FrameworkElement c in corpo.Children)
            {
                if (c == v)
                {
                    if (c.Visibility == Visibility.Visible) continue;
                    c.Visibility = Visibility.Visible;
                    var sp = new TranslateTransform(0, 8);
                    c.RenderTransform = sp;
                    var e = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
                    sp.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = e });
                    c.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                }
                else c.Visibility = Visibility.Collapsed;
            }
        }
        vaiTutte.Sinistro = () => { if (tutte.Children.Count == 0) TutteLeApp(tutte, () => Mostra(home)); Mostra(tutte); };

        List<Action> azioni = new();
        campo.TextChanged += (_, _) =>
        {
            segnaposto.Visibility = campo.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (campo.Text.Trim().Length == 0) { Mostra(home); return; }
            azioni = Risultati(risultati, campo.Text.Trim());
            Mostra(risultati);
        };
        campo.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && risultati.Visibility == Visibility.Visible && azioni.Count > 0) { var a = azioni[0]; App.Io.Pannello?.Chiudi(); a(); e.Handled = true; }
            if (e.Key == Key.Escape && campo.Text.Length > 0) { campo.Text = ""; e.Handled = true; }
        };

        var piede = Piede();
        Grid.SetRow(piede, 2);
        radice.Children.Add(piede);

        radice.Loaded += (_, _) => radice.Dispatcher.BeginInvoke(() => { campo.Focus(); Keyboard.Focus(campo); }, DispatcherPriority.Input);
        // scrivere con il menu aperto va sempre nel campo di ricerca
        radice.PreviewTextInput += (_, e) => { if (!campo.IsKeyboardFocused) { campo.Focus(); campo.Text += e.Text; campo.CaretIndex = campo.Text.Length; e.Handled = true; } };
        return radice;
    }

    // Preferiti a griglia e file recenti.
    static FrameworkElement Home(out Tasto vaiTutte)
    {
        var sp = new StackPanel();
        vaiTutte = Link("Tutte le app", "", null);
        sp.Children.Add(Testata("Preferiti", vaiTutte));
        var griglia = new UniformGrid { Columns = 6 };
        sp.Children.Add(griglia);
        void Riempi()
        {
            griglia.Children.Clear();
            var pref = Preferiti();
            foreach (var p in pref.Take(18))
            {
                var v = Indice.App.FirstOrDefault(a => a.Percorso == p);
                if (v == null) continue;
                var img = Icona(null, 32);
                IconaDopo(img, v.Percorso);
                var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                col.Children.Add(img);
                var n = Tema.Scritta(v.Nome, 11.5);
                n.TextAlignment = TextAlignment.Center;
                n.TextTrimming = TextTrimming.CharacterEllipsis;
                n.MaxWidth = 84;
                n.Margin = new Thickness(0, 7, 0, 0);
                n.HorizontalAlignment = HorizontalAlignment.Center;
                col.Children.Add(n);
                var t = new Tasto(col, 92, 82) { Margin = new Thickness(0, 0, 0, 2), Sinistro = () => Lancia(v), ToolTip = Barra.Suggerimento(v.Nome) };
                // tasto destro: toglie dai preferiti
                t.Destro = () => { var l = Preferiti(); l.Remove(v.Percorso); Salva(l); Riempi(); };
                griglia.Children.Add(t);
            }
        }
        Riempi();
        Ricarica = Riempi;

        var recenti = Recenti();
        if (recenti.Count > 0)
        {
            var t = Testata("Recenti", Link("Altri", "", () => Apri("explorer.exe", "shell:Recent")));
            t.Margin = new Thickness(10, 18, 0, 8);
            sp.Children.Add(t);
            var g = new UniformGrid { Columns = 2 };
            foreach (var (nome, percorso, quando) in recenti)
            {
                var r = new Grid { Margin = new Thickness(10, 0, 8, 0) };
                r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                r.ColumnDefinitions.Add(new ColumnDefinition());
                var img = Icona(null, 26);
                img.HorizontalAlignment = HorizontalAlignment.Left;
                var pp = percorso;
                img.Dispatcher.BeginInvoke(() => img.Source = Shell.Icona(pp, 48), DispatcherPriority.Background);
                r.Children.Add(img);
                var testo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var n = Tema.Scritta(nome, 12.5);
                n.TextTrimming = TextTrimming.CharacterEllipsis;
                testo.Children.Add(n);
                testo.Children.Add(Tema.Scritta(quando, 11, Tema.BiancoTenue));
                Grid.SetColumn(testo, 1);
                r.Children.Add(testo);
                g.Children.Add(new Tasto(r, double.NaN, 52) { Margin = new Thickness(0, 0, 2, 2), Sinistro = () => Apri(pp), ToolTip = Barra.Suggerimento(pp) });
            }
            sp.Children.Add(g);
        }
        return sp;
    }

    // Ultimi file aperti, dai collegamenti di Windows in Recent (le cartelle no).
    static List<(string nome, string percorso, string quando)> Recenti()
    {
        var l = new List<(string, string, string)>();
        try
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            foreach (var f in new DirectoryInfo(dir).GetFiles("*.lnk").OrderByDescending(f => f.LastWriteTime).Take(30))
            {
                var d = Shell.DestinazioneLnk(f.FullName);
                if (string.IsNullOrEmpty(d) || !File.Exists(d)) continue;
                if (l.Any(x => x.Item2 == d)) continue;
                l.Add((Path.GetFileNameWithoutExtension(d), d, Fa(f.LastWriteTime)));
                if (l.Count == 6) break;
            }
        }
        catch { }
        return l;
    }

    public static string Fa(DateTime t)
    {
        var d = DateTime.Now - t;
        if (d.TotalMinutes < 1) return "adesso";
        if (d.TotalMinutes < 60) return $"{(int)d.TotalMinutes} min fa";
        if (d.TotalHours < 24) return (int)d.TotalHours == 1 ? "1 ora fa" : $"{(int)d.TotalHours} ore fa";
        if (d.TotalDays < 2) return "ieri";
        if (d.TotalDays < 7) return $"{(int)d.TotalDays} giorni fa";
        return t.ToString("d MMM", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
    }

    // Elenco alfabetico con le lettere, scorrevole.
    static void TutteLeApp(Grid dove, Action indietro)
    {
        dove.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dove.RowDefinitions.Add(new RowDefinition());
        var t = Testata("Tutte le app", null);
        var su = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 10, 0) };
        var gl = Tema.Glifo("", 10, Tema.BiancoTenue);
        gl.Margin = new Thickness(0, 1, 8, 0);
        su.Children.Add(gl);
        su.Children.Add(Tema.Scritta("Indietro", 12, Tema.BiancoTenue));
        t.Children.Add(new Tasto(su, double.NaN, 28) { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0), Sinistro = indietro });
        dove.Children.Add(t);
        var elenco = new StackPanel();
        var scorri = new ScrollViewer { Content = elenco, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false, Margin = new Thickness(0, 0, 0, 6) };
        Grid.SetRow(scorri, 1);
        dove.Children.Add(scorri);
        string lettera = null;
        foreach (var v in Indice.App.OrderBy(a => a.Nome, StringComparer.CurrentCultureIgnoreCase))
        {
            var l = char.IsLetter(v.Nome[0]) ? char.ToUpperInvariant(v.Nome[0]).ToString() : "#";
            if (l != lettera)
            {
                lettera = l;
                var s = Tema.Scritta(l, 12, Tema.LimePennello, FontWeights.SemiBold);
                s.Margin = new Thickness(12, elenco.Children.Count == 0 ? 0 : 10, 0, 4);
                elenco.Children.Add(s);
            }
            elenco.Children.Add(RigaApp(v));
        }
    }

    static Tasto RigaApp(Indice.Voce v, string destra = null)
    {
        var r = new Grid { Margin = new Thickness(10, 0, 12, 0) };
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        r.ColumnDefinitions.Add(new ColumnDefinition());
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var img = Icona(null, 22);
        img.HorizontalAlignment = HorizontalAlignment.Left;
        IconaDopo(img, v.Percorso);
        r.Children.Add(img);
        var n = Tema.Scritta(v.Nome, 13);
        n.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(n, 1);
        r.Children.Add(n);
        var pref = Tema.Glifo("", 11, Tema.LimePennello);
        pref.Visibility = Preferiti().Contains(v.Percorso) ? Visibility.Visible : Visibility.Collapsed;
        pref.Margin = new Thickness(8, 0, 0, 0);
        var dx = new StackPanel { Orientation = Orientation.Horizontal };
        if (destra != null) dx.Children.Add(Tema.Scritta(destra, 11, Tema.BiancoTenue));
        dx.Children.Add(pref);
        Grid.SetColumn(dx, 2);
        r.Children.Add(dx);
        var t = new Tasto(r, double.NaN, 38) { Margin = new Thickness(0, 0, 0, 1), Sinistro = () => Lancia(v) };
        // tasto destro: aggiunge o toglie dai preferiti (la stellina lo segna)
        t.Destro = () =>
        {
            var l = Preferiti();
            if (!l.Remove(v.Percorso)) l.Add(v.Percorso);
            Salva(l);
            pref.Visibility = l.Contains(v.Percorso) ? Visibility.Visible : Visibility.Collapsed;
            Ricarica?.Invoke();
        };
        return t;
    }

    static Action Ricarica;

    static List<Action> Risultati(StackPanel dove, string q)
    {
        dove.Children.Clear();
        var azioni = new List<Action>();
        if (q.StartsWith("=") || (q.Length > 1 && q.Any(char.IsDigit) && q.All(ch => "0123456789+-*/().,% ".Contains(ch))))
        {
            var esito = Calcola(q.TrimStart('='));
            if (esito != null)
            {
                Action a = () => Clipboard.SetText(esito);
                azioni.Add(a);
                dove.Children.Add(Voce(Contenuti.Ico.Calcolatrice, esito, "Copia", a));
            }
        }
        if (q.StartsWith("?") && q.Length > 1) q = q[1..].Trim();
        else
        {
            string ql = q.ToLowerInvariant();
            var app = Indice.App
                .Select(a => (a, p: Cerca.Punteggio(a.Nome, ql)))
                .Where(x => x.p > 0)
                .OrderByDescending(x => x.p + Math.Min(300, Indice.Usi(x.a.Percorso) * 25))
                .ThenBy(x => x.a.Nome)
                .Take(8);
            foreach (var (v, _) in app)
            {
                var vv = v;
                azioni.Add(() => Lancia(vv));
                var t = RigaApp(v, "App");
                t.Height = 44;
                dove.Children.Add(t);
            }
        }
        Action web = () => Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(q)) { UseShellExecute = true });
        azioni.Add(web);
        dove.Children.Add(Voce(Contenuti.Ico.Browser, $"Cerca «{q}» sul web", "Google", web));
        if (dove.Children.Count > 0 && dove.Children[0] is Tasto primo) primo.Acceso = true;
        return azioni;
    }

    static Tasto Voce(ImageSource icona, string testo, string destra, Action a)
    {
        var r = new Grid { Margin = new Thickness(10, 0, 12, 0) };
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
        r.ColumnDefinitions.Add(new ColumnDefinition());
        r.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var img = Icona(icona, 22);
        img.HorizontalAlignment = HorizontalAlignment.Left;
        r.Children.Add(img);
        var n = Tema.Scritta(testo, 13);
        n.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(n, 1);
        r.Children.Add(n);
        var d = Tema.Scritta(destra, 11, Tema.BiancoTenue);
        Grid.SetColumn(d, 2);
        r.Children.Add(d);
        return new Tasto(r, double.NaN, 44) { Margin = new Thickness(0, 0, 0, 1), Sinistro = () => { App.Io.Pannello?.Chiudi(); a(); } };
    }

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

    // Profilo a sinistra; cartelle, impostazioni e spegnimento a destra. Il tasto di spegnimento apre le sue scelte al posto delle scorciatoie.
    static FrameworkElement Piede()
    {
        var fascia = new Border { Background = Tema.Pennello(0, 0, 0, .16), Padding = new Thickness(18, 10, 18, 12) };
        var g = new Grid();
        fascia.Child = g;

        var chi = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 12, 0) };
        var foto = Foto();
        if (foto != null)
        {
            var img = Icona(foto, 30);
            img.Clip = new RectangleGeometry(new Rect(0, 0, 30, 30), 3, 3);
            chi.Children.Add(img);
        }
        else chi.Children.Add(Tema.Glifo("", 18));
        var nome = Tema.Scritta(NomeUtente(), 13);
        nome.Margin = new Thickness(12, 0, 0, 1);
        nome.VerticalAlignment = VerticalAlignment.Center;
        chi.Children.Add(nome);
        var profilo = new Tasto(chi, double.NaN, 44) { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0), Sinistro = () => Apri("ms-settings:accounts") };
        g.Children.Add(profilo);

        var casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var scorciatoie = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        Tasto Piccolo(ImageSource ic, string glifo, string sugg, Action a)
        {
            UIElement dentro = ic != null ? Icona(ic, 18) : Tema.Glifo(glifo, 16);
            return new Tasto(dentro, 40, 40) { Sinistro = a, ToolTip = Barra.Suggerimento(sugg) };
        }
        scorciatoie.Children.Add(Piccolo(Contenuti.Ico.Cartella(Path.Combine(casa, "Downloads")), null, "Download", () => { App.Io.Pannello?.Chiudi(); Esplora.Apri(Path.Combine(casa, "Downloads")); }));
        scorciatoie.Children.Add(Piccolo(Contenuti.Ico.Esplora, null, "Esplora file", () => { App.Io.Pannello?.Chiudi(); Esplora.Apri(); }));
        scorciatoie.Children.Add(Piccolo(Contenuti.Ico.Impostazioni, null, "Impostazioni", () => Apri("ms-settings:")));

        var spegni = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Visibility = Visibility.Collapsed };
        spegni.Children.Add(Scelta("", "Blocca", () => LockWorkStation()));
        spegni.Children.Add(Scelta("", "Esci", () => Apri("shutdown.exe", "/l")));
        spegni.Children.Add(Scelta("", "Sospendi", () => SetSuspendState(false, false, false)));
        spegni.Children.Add(Scelta("", "Riavvia", () => Apri("shutdown.exe", "/r /t 0")));
        spegni.Children.Add(Scelta("", "Arresta", () => Apri("shutdown.exe", "/s /t 0")));

        var interruttore = new Tasto(Tema.Glifo("", 16), 40, 40) { ToolTip = Barra.Suggerimento("Spegni") };
        interruttore.Sinistro = () =>
        {
            bool apri = spegni.Visibility != Visibility.Visible;
            interruttore.Acceso = apri;
            scorciatoie.Visibility = apri ? Visibility.Collapsed : Visibility.Visible;
            spegni.Visibility = apri ? Visibility.Visible : Visibility.Collapsed;
            var vista = apri ? (UIElement)spegni : scorciatoie;
            var tr = new TranslateTransform(12, 0);
            vista.RenderTransform = tr;
            tr.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
            vista.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        };
        var destra = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var gruppi = new Grid();
        gruppi.Children.Add(scorciatoie);
        gruppi.Children.Add(spegni);
        destra.Children.Add(gruppi);
        destra.Children.Add(interruttore);
        g.Children.Add(destra);
        return fascia;
    }

    static Tasto Scelta(string glifo, string testo, Action a)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0) };
        sp.Children.Add(Tema.Glifo(glifo, 13));
        var s = Tema.Scritta(testo, 12);
        s.Margin = new Thickness(7, 0, 0, 1);
        sp.Children.Add(s);
        return new Tasto(sp, double.NaN, 40) { Sinistro = () => { App.Io.Pannello?.Chiudi(); a(); } };
    }

    [DllImport("user32.dll")] static extern bool LockWorkStation();
    [DllImport("powrprof.dll")] static extern bool SetSuspendState(bool hibernate, bool force, bool disableWake);
    [DllImport("secur32.dll", CharSet = CharSet.Unicode)] static extern bool GetUserNameEx(int formato, StringBuilder nome, ref int lunghezza);
    [DllImport("shell32.dll", EntryPoint = "#261", CharSet = CharSet.Unicode)] static extern int GetUserTilePath(string utente, uint flag, StringBuilder percorso, int lunghezza);

    static string nomeUtente;
    static string NomeUtente()
    {
        if (nomeUtente != null) return nomeUtente;
        var sb = new StringBuilder(256);
        int n = sb.Capacity;
        nomeUtente = GetUserNameEx(3, sb, ref n) && sb.Length > 0 ? sb.ToString() : Environment.UserName;
        return nomeUtente;
    }

    static ImageSource foto;
    static bool fotoCercata;
    static ImageSource Foto()
    {
        if (fotoCercata) return foto;
        fotoCercata = true;
        try
        {
            var sb = new StringBuilder(1024);
            if (GetUserTilePath(null, 0x80000000, sb, sb.Capacity) == 0 && File.Exists(sb.ToString()))
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.UriSource = new Uri(sb.ToString());
                b.DecodePixelWidth = 96;
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.EndInit();
                b.Freeze();
                foto = b;
            }
        }
        catch { }
        return foto;
    }

    // Tasto Win da solo: apre questo menu invece di quello di Windows. Le combinazioni (Win+E, Win+Maiusc+S…) restano di Windows.
    // L'aggancio della tastiera sta su un thread suo, così non salta se l'interfaccia è occupata.
    delegate IntPtr ProcTastiera(int codice, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, ProcTastiera proc, IntPtr mod, uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr h, int codice, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string n);
    [DllImport("user32.dll")] static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr h; public uint m; public IntPtr w, l; public uint t; public int x, y; }
    static ProcTastiera proc;
    static bool winGiu, altro;

    public static void AscoltaWin(Action apri, Action notifiche)
    {
        var t = new Thread(() =>
        {
            proc = (codice, w, l) =>
            {
                if (codice >= 0)
                {
                    int vk = Marshal.ReadInt32(l), flag = Marshal.ReadInt32(l, 8);
                    bool giu = (int)w is 0x100 or 0x104, su = (int)w is 0x101 or 0x105, finto = (flag & 0x10) != 0;
                    if (vk is 0x5B or 0x5C)
                    {
                        if (giu && !winGiu) { winGiu = true; altro = false; }
                        else if (su && winGiu)
                        {
                            winGiu = false;
                            if (!altro && !finto)
                            {
                                // un tasto senza significato prima del rilascio: Windows lo prende per una combinazione e non apre il suo Start
                                keybd_event(0xE8, 0, 0, UIntPtr.Zero);
                                keybd_event(0xE8, 0, 2, UIntPtr.Zero);
                                Application.Current.Dispatcher.BeginInvoke(apri);
                            }
                        }
                    }
                    else if (winGiu && giu && vk != 0xE8)
                    {
                        altro = true;
                        // Win+N: il nostro centro notifiche al posto di quello di Windows
                        if (vk == 0x4E && !finto) { Application.Current.Dispatcher.BeginInvoke(notifiche); return (IntPtr)1; }
                        // Win+E: il nostro Esplora
                        if (vk == 0x45 && !finto) { Application.Current.Dispatcher.BeginInvoke(() => Esplora.Apri()); return (IntPtr)1; }
                    }
                }
                return CallNextHookEx(IntPtr.Zero, codice, w, l);
            };
            if (SetWindowsHookEx(13, proc, GetModuleHandle(null), 0) == IntPtr.Zero) { App.Log("tasto Win non agganciato"); return; }
            while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
        }) { IsBackground = true, Name = "tasto Win" };
        t.Start();
    }
}
