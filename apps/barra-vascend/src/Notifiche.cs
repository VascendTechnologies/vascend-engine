using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace BarraVascend;

// Centro notifiche al posto di quello di Windows: legge le notifiche dal sistema, le raggruppa per app
// e le toglie davvero da Windows quando le chiudi. Si apre dalla campanella e con Win+N.
static class Notifiche
{
    static UserNotificationListener ascolto;
    static bool permesso, inCorso;
    static string firma = "";
    public static List<UserNotification> Elenco = new();
    public static event Action Cambiate;
    static readonly HashSet<uint> viste = new();

    public static async void Avvia()
    {
        try
        {
            ascolto = UserNotificationListener.Current;
            var s = await ascolto.RequestAccessAsync();
            permesso = s == UserNotificationListenerAccessStatus.Allowed;
            if (!permesso) App.Log("notifiche: accesso " + s);
            Aggiorna();
        }
        catch (Exception e) { App.Log("notifiche: " + e.Message); }
    }

    // Il sistema non avvisa le app senza pacchetto: si rilegge ogni pochi secondi, ed è leggero.
    public static async void Aggiorna()
    {
        if (!permesso || inCorso) return;
        inCorso = true;
        try
        {
            var n = await ascolto.GetNotificationsAsync(NotificationKinds.Toast);
            var f = string.Join(",", n.Select(x => x.Id));
            if (f != firma)
            {
                firma = f;
                Elenco = n.OrderByDescending(x => x.CreationTime).ToList();
                Cambiate?.Invoke();
            }
        }
        catch (Exception e) { App.Log("notifiche: " + e.Message); }
        finally { inCorso = false; }
    }

    static void Togli(uint id)
    {
        try { ascolto.RemoveNotification(id); } catch { }
        Elenco.RemoveAll(x => x.Id == id);
        firma = string.Join(",", Elenco.Select(x => x.Id));
        Cambiate?.Invoke();
    }

    static void Svuota()
    {
        try { ascolto.ClearNotifications(); } catch { }
        Elenco.Clear();
        firma = "";
        Cambiate?.Invoke();
    }

    static List<string> Testi(UserNotification n)
    {
        try
        {
            var b = n.Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric);
            if (b != null) return b.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        }
        catch { }
        return new List<string>();
    }

    static string Aumid(UserNotification n) { try { return n.AppInfo?.AppUserModelId; } catch { return null; } }
    static string NomeApp(UserNotification n) { try { return n.AppInfo?.DisplayInfo?.DisplayName ?? "App"; } catch { return "App"; } }

    // Clic su una notifica: porta davanti l'app (o la apre) e la notifica se ne va, come in Windows.
    static void ApriApp(UserNotification n)
    {
        var a = Aumid(n);
        Togli(n.Id);
        App.Io.Pannello?.Chiudi();
        if (a == null) return;
        var g = App.Io.Finestre.Gruppi.FirstOrDefault(x => x.Aumid != null && x.Aumid.Equals(a, StringComparison.OrdinalIgnoreCase) && x.Finestre.Count > 0);
        if (g != null) { N.Attiva(g.Finestre[0].H); return; }
        try { Process.Start("explorer.exe", @"shell:AppsFolder\" + a); } catch { }
    }

    public static UIElement Pannello()
    {
        var radice = new StackPanel { Width = 380, Margin = new Thickness(12) };
        var testa = new Grid { Margin = new Thickness(6, 2, 0, 10) };
        var titolo = Tema.Scritta("Notifiche", 13, Tema.Bianco, FontWeights.SemiBold);
        titolo.VerticalAlignment = VerticalAlignment.Center;
        testa.Children.Add(titolo);
        var scritta = new Border { Child = Tema.Scritta("Cancella tutto", 12, Tema.BiancoTenue), Padding = new Thickness(10, 0, 10, 1) };
        var tutto = new Tasto(scritta, double.NaN, 28) { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0), Sinistro = Svuota };
        testa.Children.Add(tutto);
        radice.Children.Add(testa);

        var elenco = new StackPanel();
        var scorri = new ScrollViewer { Content = elenco, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Focusable = false, MaxHeight = 620 };
        radice.Children.Add(scorri);

        void Disegna()
        {
            elenco.Children.Clear();
            tutto.Visibility = Elenco.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (Elenco.Count == 0)
            {
                var vuoto = new StackPanel { Margin = new Thickness(0, 26, 0, 30), HorizontalAlignment = HorizontalAlignment.Center };
                var g = Tema.Glifo("", 26, Tema.Pennello(255, 255, 255, .30));
                g.HorizontalAlignment = HorizontalAlignment.Center;
                vuoto.Children.Add(g);
                var t = Tema.Scritta("Nessuna notifica", 12.5, Tema.BiancoTenue);
                t.Margin = new Thickness(0, 10, 0, 0);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                vuoto.Children.Add(t);
                elenco.Children.Add(vuoto);
                return;
            }
            int k = 0;
            foreach (var gruppo in Elenco.GroupBy(n => Aumid(n) ?? NomeApp(n)))
            {
                var lista = gruppo.ToList();
                elenco.Children.Add(TestaApp(lista));
                foreach (var n in lista.Take(6))
                {
                    var c = Scheda(n);
                    // le nuove entrano scivolando, quelle già viste restano ferme
                    if (viste.Add(n.Id)) Entra(c, 30 * k++);
                    elenco.Children.Add(c);
                }
            }
        }
        Disegna();
        Action cambiate = () => radice.Dispatcher.BeginInvoke(Disegna);
        Cambiate += cambiate;
        radice.Unloaded += (_, _) => Cambiate -= cambiate;
        Aggiorna();
        return radice;
    }

    static FrameworkElement TestaApp(List<UserNotification> lista)
    {
        var g = new Grid { Margin = new Thickness(8, 8, 0, 6) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var a = Aumid(lista[0]);
        var ic = a != null ? Shell.IconaApp(a) : null;
        if (ic != null)
        {
            var img = new Image { Source = ic, Width = 16, Height = 16, HorizontalAlignment = HorizontalAlignment.Left };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            g.Children.Add(img);
        }
        var n = Tema.Scritta(NomeApp(lista[0]), 12, Tema.BiancoTenue, FontWeights.SemiBold);
        n.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(n, 1);
        g.Children.Add(n);
        var via = new Tasto(Tema.Glifo("", 9, Tema.BiancoTenue), 26, 22) { Margin = new Thickness(0), ToolTip = Barra.Suggerimento("Togli tutte") };
        via.Sinistro = () => { foreach (var x in lista) try { ascolto.RemoveNotification(x.Id); } catch { } Aggiorna(); };
        Grid.SetColumn(via, 2);
        g.Children.Add(via);
        return g;
    }

    static FrameworkElement Scheda(UserNotification n)
    {
        var testi = Testi(n);
        var g = new Grid { Margin = new Thickness(12, 10, 6, 11) };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var col = new StackPanel();
        var riga = new DockPanel();
        var quando = Tema.Scritta(Start.Fa(n.CreationTime.LocalDateTime), 11, Tema.BiancoTenue);
        quando.Margin = new Thickness(8, 1, 0, 0);
        DockPanel.SetDock(quando, Dock.Right);
        riga.Children.Add(quando);
        var t = Tema.Scritta(testi.Count > 0 ? testi[0] : NomeApp(n), 13, Tema.Bianco, FontWeights.SemiBold);
        t.TextTrimming = TextTrimming.CharacterEllipsis;
        riga.Children.Add(t);
        col.Children.Add(riga);
        foreach (var r in testi.Skip(1).Take(3))
        {
            var c = Tema.Scritta(r, 12, Tema.Pennello(255, 255, 255, .78));
            c.TextWrapping = TextWrapping.Wrap;
            c.MaxHeight = 52;
            c.TextTrimming = TextTrimming.CharacterEllipsis;
            c.Margin = new Thickness(0, 3, 0, 0);
            col.Children.Add(c);
        }
        g.Children.Add(col);

        var chiudi = new Tasto(Tema.Glifo("", 9), 24, 22) { Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top, Opacity = 0 };
        Grid.SetColumn(chiudi, 1);
        g.Children.Add(chiudi);

        var fondo = new Cornice { Profilo = Gradini.Piccolo, Fondo = Tema.Pennello(255, 255, 255, .07), Bordo = Tema.Pennello(255, 255, 255, .12), Child = g };
        var scheda = new Tasto(fondo, double.NaN, double.NaN) { Margin = new Thickness(0, 0, 0, 6), Sinistro = () => ApriApp(n) };
        scheda.Centrale = () => Esci(scheda, () => Togli(n.Id));
        chiudi.Sinistro = () => Esci(scheda, () => Togli(n.Id));
        // la crocetta compare solo passandoci sopra
        scheda.MouseEnter += (_, _) => chiudi.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(140)));
        scheda.MouseLeave += (_, _) => chiudi.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(180)));
        return scheda;
    }

    static void Entra(UIElement c, int ritardo)
    {
        var tr = new TranslateTransform(18, 0);
        c.RenderTransform = tr;
        c.Opacity = 0;
        var inizio = TimeSpan.FromMilliseconds(120 + ritardo);
        tr.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(280)) { BeginTime = inizio, EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        c.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = inizio });
    }

    // Chiusura: la scheda scivola via a destra e sfuma, poi sparisce.
    static void Esci(UIElement c, Action fatto)
    {
        c.IsHitTestVisible = false;
        var tr = new TranslateTransform();
        c.RenderTransform = tr;
        var a = new DoubleAnimation(60, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        a.Completed += (_, _) => fatto();
        tr.BeginAnimation(TranslateTransform.XProperty, a);
        c.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(160)));
    }
}
