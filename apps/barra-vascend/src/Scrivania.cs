using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace BarraVascend;

// I widget del desktop che stanno nello stesso punto di uno schermo, in colonna.
// Stanno sotto tutte le finestre e lasciano passare i clic.
class Scrivania : Window
{
    readonly Schermo schermo;
    readonly string posizione;
    readonly List<Action> aggiorna = new();
    readonly DispatcherTimer tic;
    IntPtr hwnd;

    public Scrivania(Schermo s, string posizione, IEnumerable<Widget> widget)
    {
        schermo = s;
        this.posizione = posizione;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        UseLayoutRounding = true;
        Title = "BarraVascend widget";
        IsHitTestVisible = false;

        var allinea = posizione.EndsWith("sinistra") ? HorizontalAlignment.Left : posizione.EndsWith("destra") ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        var sp = new StackPanel();
        foreach (var w in widget)
        {
            var el = Crea(w);
            if (el == null) continue;
            el = WidgetAspetto.Decora(w,el);
            el.HorizontalAlignment = allinea;
            el.Opacity = Math.Clamp(w.Opacita, .05, 1);
            el.Margin = new Thickness(0, sp.Children.Count > 0 ? 10 : 0, 0, 0);
            sp.Children.Add(el);
        }
        Content = sp;
        foreach (var a in aggiorna) a();

        SourceInitialized += (_, _) =>
        {
            hwnd = new WindowInteropHelper(this).Handle;
            long ex = (long)N.GetWindowLongPtr(hwnd, N.GWL_EXSTYLE);
            N.SetWindowLongPtr(hwnd, N.GWL_EXSTYLE, (IntPtr)(ex | N.WS_EX_TOOLWINDOW | N.WS_EX_NOACTIVATE | 0x20));
            var src = HwndSource.FromHwnd(hwnd);
            src.AddHook(Fondo.Gancio);
            src.AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool g) => { if (m == N.WM_DPICHANGED) Dispatcher.BeginInvoke(Posiziona); return IntPtr.Zero; });
        };
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(Posiziona);
        // si sveglia una volta al secondo, appena dopo lo scatto, invece di controllare di continuo
        tic = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000 - DateTime.Now.Millisecond + 15) };
        tic.Tick += (_, _) => { foreach (var a in aggiorna) a(); tic.Interval = TimeSpan.FromMilliseconds(1000 - DateTime.Now.Millisecond + 15); };
        if (aggiorna.Count > 0) tic.Start();
        Closed += (_, _) => tic.Stop();
    }

    public bool Vuota => ((StackPanel)Content).Children.Count == 0;

    static SolidColorBrush Pennello(Widget w) => new(Stile.Colore(string.IsNullOrEmpty(w.Colore) ? "#FFFFFF" : w.Colore));

    static TextBlock Scritta(Widget w, double dim, FontWeight? peso = null)
    {
        return WidgetAspetto.Testo(w,"",dim,peso);
    }

    FrameworkElement Crea(Widget w)
    {
        double d = Math.Max(6, w.Dimensione);
        switch (w.Tipo)
        {
            case "orologio":
            {
                var sp = new StackPanel();
                var ora = Scritta(w, d);
                ora.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(ora);
                string formato = w.Secondi ? "HH:mm:ss" : "HH:mm";
                aggiorna.Add(() => { var t = DateTime.Now.ToString(formato); if (ora.Text != t) ora.Text = t; });
                var nome=WidgetAspetto.Firma(w,d);
                if(nome != null) sp.Children.Add(nome);
                return sp;
            }
            case "data":
            {
                var sp = new StackPanel();
                var giorno = Scritta(w, d * .55, FontWeights.Medium);
                giorno.Opacity = .7;
                var data = Scritta(w, d, FontWeights.SemiBold);
                sp.Children.Add(giorno);
                sp.Children.Add(data);
                var it = new CultureInfo("it-IT");
                aggiorna.Add(() =>
                {
                    var n = DateTime.Now;
                    giorno.Text = it.TextInfo.ToTitleCase(n.ToString("dddd", it));
                    data.Text = n.ToString("d MMMM", it);
                });
                return sp;
            }
            case "testo":
            {
                var t = Scritta(w, d, FontWeights.SemiBold);
                t.Text = w.Testo ?? "";
                t.TextAlignment = posizione.EndsWith("sinistra") ? TextAlignment.Left : posizione.EndsWith("destra") ? TextAlignment.Right : TextAlignment.Center;
                return t.Text.Length > 0 ? t : null;
            }
            case "immagine":
            {
                if (string.IsNullOrEmpty(w.Immagine) || !System.IO.File.Exists(w.Immagine)) return null;
                try
                {
                    var b = new BitmapImage();
                    b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.UriSource = new Uri(w.Immagine); b.DecodePixelHeight = (int)(d * 2); b.EndInit();
                    var img = new Image { Height = d, Source = b, Stretch = Stretch.Uniform };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    return img;
                }
                catch { return null; }
            }
            case "sistema":
            {
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(d * 9) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                void Riga(int r, string nome, Func<double> valore)
                {
                    g.RowDefinitions.Add(new RowDefinition());
                    var n = Scritta(w, d * .8, FontWeights.Medium);
                    n.Text = nome; n.Opacity = .7; n.VerticalAlignment = VerticalAlignment.Center; n.Margin = new Thickness(0, 3, d * .6, 3);
                    Grid.SetRow(n, r); g.Children.Add(n);
                    var fondo = new Border { Height = Math.Max(3, d * .3), Background = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center };
                    var pieno = new Border { Height = fondo.Height, Background = Pennello(w), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Width = 0 };
                    var cella = new Grid(); cella.Children.Add(fondo); cella.Children.Add(pieno);
                    Grid.SetRow(cella, r); Grid.SetColumn(cella, 1); g.Children.Add(cella);
                    var v = Scritta(w, d, FontWeights.SemiBold);
                    v.Width = d * 3; v.TextAlignment = TextAlignment.Right; v.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetRow(v, r); Grid.SetColumn(v, 2); g.Children.Add(v);
                    aggiorna.Add(() => { var x = valore(); v.Text = $"{Math.Round(x)}%"; pieno.Width = d * 9 * x / 100; });
                }
                Riga(0, "CPU", () => Prestazioni.Cpu);
                Riga(1, "RAM", () => Prestazioni.Ram);
                return g;
            }
            case "musica":
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var cop = new Image { Width = d * 3.4, Height = d * 3.4, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, d * .8, 0) };
                RenderOptions.SetBitmapScalingMode(cop, BitmapScalingMode.HighQuality);
                var testi = new StackPanel { VerticalAlignment = VerticalAlignment.Center, MaxWidth = d * 18 };
                var tit = Scritta(w, d * 1.15, FontWeights.SemiBold); tit.TextTrimming = TextTrimming.CharacterEllipsis;
                var art = Scritta(w, d * .85); art.Opacity = .7; art.TextTrimming = TextTrimming.CharacterEllipsis;
                testi.Children.Add(tit); testi.Children.Add(art);
                sp.Children.Add(cop); sp.Children.Add(testi);
                aggiorna.Add(() =>
                {
                    sp.Visibility = Musica.Presente ? Visibility.Visible : Visibility.Collapsed;
                    if (tit.Text != Musica.Titolo) tit.Text = Musica.Titolo;
                    if (art.Text != Musica.Artista) art.Text = Musica.Artista;
                    if (cop.Source != Musica.Copertina) cop.Source = Musica.Copertina;
                });
                return sp;
            }
        }
        return null;
    }

    public void Avvia()
    {
        Show();
        Posiziona();
    }

    // Nel punto scelto dell'area libera dello schermo, staccato dai bordi.
    void Posiziona()
    {
        if (hwnd == IntPtr.Zero) return;
        N.GetWindowRect(hwnd, out var r);
        var mi = new N.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.MONITORINFOEX>() };
        var a = N.GetMonitorInfo(schermo.H, ref mi) ? mi.rcWork : schermo.Area;
        N.GetDpiForMonitor(schermo.H, 0, out uint dpi, out _);
        int m = (int)Math.Round(56 * dpi / 96.0);
        int x = posizione.EndsWith("sinistra") ? a.L + m : posizione.EndsWith("destra") ? a.R - m - r.W : a.L + (a.W - r.W) / 2;
        int y = posizione.StartsWith("alto") ? a.T + m : posizione.StartsWith("basso") ? a.B - m - r.H : a.T + (a.H - r.H) / 2;
        N.SetWindowPos(hwnd, (IntPtr)1, x, y, 0, 0, N.SWP_NOSIZE | N.SWP_NOACTIVATE);
    }
}
