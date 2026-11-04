using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using BarraVascend;

namespace VascendEngine;

// Selettore di colore: quadrato saturazione/luminosità, striscia della tinta e codice esadecimale.
class Selettore : Popup
{
    public event Action<Color> Cambiato;
    double h, sat, val;
    readonly Rectangle tinta = new();
    readonly Border segnoSv = Segno(), segnoH = Segno();
    readonly Grid sv, striscia;
    readonly TextBox hex;
    readonly Border campione = new() { Width = 28, Height = 28 };
    bool scrivo;

    static Border Segno() => new()
    {
        Width = 10, Height = 10, BorderBrush = Brushes.White, BorderThickness = new Thickness(2),
        HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false,
        Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = .8 },
    };

    public Selettore(Color iniziale)
    {
        StaysOpen = false;
        AllowsTransparency = true;
        PopupAnimation = PopupAnimation.Fade;
        Placement = PlacementMode.Right;
        HorizontalOffset = 10;

        sv = new Grid { Width = 220, Height = 160, Cursor = Cursors.Cross, ClipToBounds = true };
        sv.Children.Add(tinta);
        sv.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0) });
        sv.Children.Add(new Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90) });
        sv.Children.Add(segnoSv);
        Trascina(sv, p => { sat = Math.Clamp(p.X / sv.Width, 0, 1); val = 1 - Math.Clamp(p.Y / sv.Height, 0, 1); Aggiorna(true); });

        var arcobaleno = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        for (int i = 0; i <= 6; i++) arcobaleno.GradientStops.Add(new GradientStop(DaHsv(i * 60 % 360, 1, 1), i / 6.0));
        striscia = new Grid { Width = 220, Height = 14, Margin = new Thickness(0, 10, 0, 0), Cursor = Cursors.Hand };
        striscia.Children.Add(new Rectangle { Fill = arcobaleno });
        segnoH.Height = 14;
        segnoH.Width = 6;
        striscia.Children.Add(segnoH);
        Trascina(striscia, p => { h = Math.Clamp(p.X / striscia.Width, 0, .9999) * 360; Aggiorna(true); });

        hex = new TextBox
        {
            Width = 110, Background = Tema.Pennello(255, 255, 255, .08), BorderThickness = new Thickness(0), Foreground = Tema.Bianco,
            CaretBrush = Tema.Bianco, FontFamily = new FontFamily("JetBrainsMono Nerd Font, Cascadia Mono, Consolas"), FontSize = 13,
            Padding = new Thickness(8, 5, 8, 5), VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0),
        };
        hex.TextChanged += (_, _) =>
        {
            if (scrivo) return;
            var t = hex.Text.Trim().TrimStart('#');
            if (t.Length != 6 || !int.TryParse(t, System.Globalization.NumberStyles.HexNumber, null, out _)) return;
            Imposta(Stile.Colore(t), false);
            Cambiato?.Invoke(Stile.Colore(t));
        };
        var riga = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        riga.Children.Add(campione);
        riga.Children.Add(hex);

        var col = new StackPanel { Margin = new Thickness(14) };
        col.Children.Add(sv);
        col.Children.Add(striscia);
        col.Children.Add(riga);
        Child = new Cornice { Profilo = Gradini.Barra, Fondo = Tema.Pennello(24, 34, 44, .98), Bordo = Tema.Bordo, Child = col };
        Imposta(iniziale, true);
        Opened += (_, _) => { hex.Focus(); hex.SelectAll(); };
    }

    void Imposta(Color c, bool scriviHex)
    {
        DaRgb(c, out h, out sat, out val);
        Aggiorna(false, scriviHex);
    }

    void Aggiorna(bool avvisa, bool scriviHex = true)
    {
        var c = DaHsv(h, sat, val);
        tinta.Fill = new SolidColorBrush(DaHsv(h, 1, 1));
        segnoSv.Margin = new Thickness(sat * sv.Width - 5, (1 - val) * sv.Height - 5, 0, 0);
        segnoH.Margin = new Thickness(h / 360 * striscia.Width - 3, 0, 0, 0);
        campione.Background = new SolidColorBrush(c);
        if (scriviHex) { scrivo = true; hex.Text = Stile.Hex(c); scrivo = false; }
        if (avvisa) Cambiato?.Invoke(c);
    }

    static void Trascina(FrameworkElement el, Action<Point> fai)
    {
        el.MouseLeftButtonDown += (_, e) => { el.CaptureMouse(); fai(e.GetPosition(el)); e.Handled = true; };
        el.MouseMove += (_, e) => { if (el.IsMouseCaptured) fai(e.GetPosition(el)); };
        el.MouseLeftButtonUp += (_, _) => el.ReleaseMouseCapture();
    }

    public static Color DaHsv(double h, double s, double v)
    {
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch
        {
            < 60 => (c, x, 0d), < 120 => (x, c, 0d), < 180 => (0d, c, x),
            < 240 => (0d, x, c), < 300 => (x, 0d, c), _ => (c, 0d, x),
        };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }

    public static void DaRgb(Color c, out double h, out double s, out double v)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        h = d == 0 ? 0 : max == r ? 60 * ((g - b) / d % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        if (h < 0) h += 360;
        s = max == 0 ? 0 : d / max;
        v = max;
    }
}
