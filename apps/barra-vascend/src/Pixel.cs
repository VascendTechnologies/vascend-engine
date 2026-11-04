using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BarraVascend;

// Una finestra che sa quanto vale un "pixel" della grafica in DIP sul suo schermo.
interface IFinestraPixel
{
    double UnitaDip { get; }
}

static class Tema
{
    public static readonly FontFamily Icone = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public static readonly FontFamily Testo = new("Vascend Grotesk, Segoe UI Variable Text, Segoe UI");
    // accento e vetro vengono dallo stile scelto in Vascend Engine
    public static readonly Color Lime = Stile.Colore(Stile.Attuale.Accento);
    static readonly Color vetro = Stile.Colore(Stile.Attuale.Vetro);
    // ogni tono è il vetro scalato canale per canale come nella tinta originale (222,240,252 in alto)
    static Color V(double r, double g, double b, double a) => Color.FromArgb((byte)Math.Clamp(Math.Round(a * Stile.Attuale.Trasparenza), 0, 255),
        (byte)Math.Round(vetro.R * r), (byte)Math.Round(vetro.G * g), (byte)Math.Round(vetro.B * b));
    public static readonly SolidColorBrush Bianco = Pennello(255, 255, 255, 1);
    public static readonly SolidColorBrush BiancoTenue = Pennello(255, 255, 255, .62);
    public static readonly SolidColorBrush LimePennello = Congela(new SolidColorBrush(Lime));
    public static readonly SolidColorBrush Sopra = Pennello(255, 255, 255, .16);
    public static readonly SolidColorBrush Premuto = Pennello(255, 255, 255, .26);
    public static readonly SolidColorBrush Separatore = Pennello(255, 255, 255, .18);

    // Vetro ghiacciato: la stessa tinta che aveva la barra di YASB, sopra la sfocatura di Windows.
    public static readonly Brush Ghiaccio = Congela(new LinearGradientBrush(new GradientStopCollection
    {
        new GradientStop(V(1, 1, 1, 87), 0),
        new GradientStop(V(176 / 222.0, 206 / 240.0, 228 / 252.0, 61), .55),
        new GradientStop(V(130 / 222.0, 162 / 240.0, 188 / 252.0, 66), 1),
    }, 90));

    // I pannelli stanno sopra finestre di ogni colore: un ghiaccio un po' più scuro per leggere bene.
    public static readonly Brush GhiaccioPannello = Congela(new LinearGradientBrush(new GradientStopCollection
    {
        new GradientStop(V(120 / 222.0, 150 / 240.0, 175 / 252.0, 120), 0),
        new GradientStop(V(50 / 222.0, 70 / 240.0, 92 / 252.0, 150), 1),
    }, 90));

    public static readonly Brush Bordo = Congela(new LinearGradientBrush(new GradientStopCollection
    {
        new GradientStop(Color.FromArgb(160, 255, 255, 255), 0),
        new GradientStop(Color.FromArgb(80, 225, 242, 255), .3),
        new GradientStop(Color.FromArgb(56, 225, 242, 255), 1),
    }, 90));

    public static readonly Brush Rilievo = Congela(new LinearGradientBrush(new GradientStopCollection
    {
        new GradientStop(Color.FromArgb(46, 255, 255, 255), 0),
        new GradientStop(Color.FromArgb(0, 255, 255, 255), .35),
        new GradientStop(Color.FromArgb(0, 0, 0, 0), .65),
        new GradientStop(Color.FromArgb(40, 0, 10, 20), 1),
    }, 90));

    public static SolidColorBrush Pennello(byte r, byte g, byte b, double a) => Congela(new SolidColorBrush(Color.FromArgb((byte)Math.Round(a * 255), r, g, b)));

    public static T Congela<T>(T f) where T : Freezable
    {
        f.Freeze();
        return f;
    }

    public static TextBlock Glifo(string g, double size = 16, Brush colore = null) => new()
    {
        Text = g,
        FontFamily = Icone,
        FontSize = size,
        Foreground = colore ?? Bianco,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    public static TextBlock Scritta(string t, double size = 13, Brush colore = null, FontWeight? peso = null) => new()
    {
        Text = t,
        FontFamily = Testo,
        FontSize = size,
        FontWeight = peso ?? FontWeights.Normal,
        Foreground = colore ?? Bianco,
        VerticalAlignment = VerticalAlignment.Center,
    };
}

// Angoli a gradini: Profilo[i] è di quante unità rientra la riga i partendo dal bordo.
static class Gradini
{
    public static readonly int[] Barra = { 4, 2, 1, 1 };
    public static readonly int[] Piccolo = { 2, 1 };
    public static readonly int[] Minimo = { 1 };

    // Contorno in senso orario, in qualsiasi unità di misura (DIP per WPF, pixel per la regione Win32).
    public static List<Point> Forma(double w, double h, double u, int[] p)
    {
        int n = p.Length;
        var angolo = new List<Point> { new(0, n) };
        for (int i = n - 1; i >= 0; i--)
        {
            angolo.Add(new Point(p[i], i + 1));
            angolo.Add(new Point(p[i], i));
        }
        var tutti = new List<Point>();
        foreach (var a in angolo) tutti.Add(new Point(a.X * u, a.Y * u));
        for (int i = angolo.Count - 1; i >= 0; i--) tutti.Add(new Point(w - angolo[i].X * u, angolo[i].Y * u));
        foreach (var a in angolo) tutti.Add(new Point(w - a.X * u, h - a.Y * u));
        for (int i = angolo.Count - 1; i >= 0; i--) tutti.Add(new Point(angolo[i].X * u, h - angolo[i].Y * u));
        return tutti;
    }

    public static Geometry Riempimento(double w, double h, double u, int[] p)
    {
        var punti = Forma(w, h, u, p);
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(punti[0], true, true);
            for (int i = 1; i < punti.Count; i++) c.LineTo(punti[i], false, false);
        }
        g.Freeze();
        return g;
    }

    // Bordo spesso un'unità che segue i gradini, come un contorno disegnato a pixel.
    public static Geometry Contorno(double w, double h, double u, int[] p)
    {
        int n = p.Length;
        var g = new GeometryGroup();
        void R(double x, double y, double cw, double ch)
        {
            if (cw > 0 && ch > 0) g.Children.Add(new RectangleGeometry(new Rect(x, y, cw, ch)));
        }
        // righe in alto e in basso
        R(p[0] * u, 0, w - 2 * p[0] * u, u);
        R(p[0] * u, h - u, w - 2 * p[0] * u, u);
        for (int i = 1; i < n; i++)
        {
            double da = p[i] * u, a = Math.Max(p[i - 1], p[i] + 1) * u;
            R(da, i * u, a - da, u);
            R(w - a, i * u, a - da, u);
            R(da, h - (i + 1) * u, a - da, u);
            R(w - a, h - (i + 1) * u, a - da, u);
        }
        // colonne ai lati
        double lato = Math.Max(p[n - 1], 1) * u;
        R(0, n * u, lato, h - 2 * n * u);
        R(w - lato, n * u, lato, h - 2 * n * u);
        g.Freeze();
        return g;
    }

    // Profilo dei gradini per il contorno interno, spostato di un'unità verso il centro.
    public static int[] Interno(int[] p)
    {
        var r = new List<int>();
        for (int i = 1; i < p.Length; i++) r.Add(Math.Max(0, p[i] - 1));
        while (r.Count > 0 && r[^1] == 0) r.RemoveAt(r.Count - 1);
        return r.Count == 0 ? new[] { 0 } : r.ToArray();
    }

    public static IntPtr Regione(int w, int h, int u, int[] p)
    {
        var punti = new List<N.POINT>();
        foreach (var a in Forma(w, h, u, p)) punti.Add(new N.POINT { X = (int)Math.Round(a.X), Y = (int)Math.Round(a.Y) });
        return N.Regione(punti);
    }
}

// Cornice a pixel: fondo e bordo a gradini, figlio al centro.
class Cornice : Decorator
{
    public int[] Profilo = Gradini.Piccolo;
    public bool Rilievo = true;
    Brush fondo, bordo;

    public Brush Fondo { get => fondo; set { fondo = value; InvalidateVisual(); } }
    public Brush Bordo { get => bordo; set { bordo = value; InvalidateVisual(); } }

    // chi non vuole i gradini (fondo chiaro, angoli lisci) accende Liscia: stesso profilo, ma arrotondato
    public static bool Liscia;
    static double Raggio(int[] p) => p.Length >= 4 ? 8 : p.Length >= 2 ? 6 : 4;

    public double Unita => Window.GetWindow(this) is IFinestraPixel f ? f.UnitaDip : 2;

    // Durante il morph il fondo e il bordo si disegnano solo nel rettangolo che si sta espandendo.
    public Rect? Area { get => area; set { area = value; InvalidateVisual(); } }
    Rect? area;

    protected override void OnRender(DrawingContext dc)
    {
        var r = area ?? new Rect(0, 0, ActualWidth, ActualHeight);
        double w = r.Width, h = r.Height, u = Unita;
        if (w <= 0 || h <= 0) return;
        dc.PushTransform(new TranslateTransform(r.X, r.Y));
        if (Liscia)
        {
            double k = Raggio(Profilo);
            var pen = bordo != null ? new Pen(bordo, 1) : null;
            double m = pen != null ? .5 : 0;
            dc.DrawRoundedRectangle(fondo, pen, new Rect(m, m, w - 2 * m, h - 2 * m), k, k);
            dc.Pop();
            return;
        }
        if (fondo != null) dc.DrawGeometry(fondo, null, Gradini.Riempimento(w, h, u, Profilo));
        if (bordo != null)
        {
            dc.DrawGeometry(bordo, null, Gradini.Contorno(w, h, u, Profilo));
            // rilievo a pixel: un filo chiaro appena dentro in alto, più scuro in basso
            if (Rilievo && w > 6 * u && h > 6 * u)
            {
                var dentro = Gradini.Interno(Profilo);
                dc.PushTransform(new TranslateTransform(u, u));
                dc.DrawGeometry(Tema.Rilievo, null, Gradini.Contorno(w - 2 * u, h - 2 * u, u, dentro));
                dc.Pop();
            }
        }
        dc.Pop();
    }

    // Lo sfondo trasparente fa arrivare i clic anche negli spazi vuoti.
    protected override HitTestResult HitTestCore(PointHitTestParameters p) => new PointHitTestResult(this, p.HitPoint);
}

// Tasto della barra: evidenziato a pixel al passaggio, azioni sui tre pulsanti e sulla rotella.
class Tasto : Cornice
{
    public Action Sinistro, Destro, Centrale, Doppio;
    public Action<int> Rotella;
    bool sopra, giu;
    public bool Acceso { get => acceso; set { acceso = value; Aggiorna(); } }
    bool acceso;

    public Tasto(UIElement contenuto, double larghezza = double.NaN, double altezza = 36)
    {
        Child = contenuto;
        Width = larghezza;
        Height = altezza;
        Profilo = Gradini.Piccolo;
        VerticalAlignment = VerticalAlignment.Center;
        Margin = new Thickness(1, 0, 1, 0);
        MouseEnter += (_, _) => { sopra = true; Aggiorna(); };
        MouseLeave += (_, _) => { sopra = false; giu = false; Aggiorna(); };
        MouseDown += (_, e) =>
        {
            giu = true; Aggiorna(); CaptureMouse(); e.Handled = true;
            if (e.ClickCount == 2 && e.ChangedButton == System.Windows.Input.MouseButton.Left) Doppio?.Invoke();
        };
        MouseUp += (_, e) =>
        {
            bool eraGiu = giu;
            giu = false;
            ReleaseMouseCapture();
            Aggiorna();
            e.Handled = true;
            if (!eraGiu || !IsMouseOver) return;
            switch (e.ChangedButton)
            {
                case System.Windows.Input.MouseButton.Left: Sinistro?.Invoke(); break;
                case System.Windows.Input.MouseButton.Right: Destro?.Invoke(); break;
                case System.Windows.Input.MouseButton.Middle: Centrale?.Invoke(); break;
            }
        };
        MouseWheel += (_, e) => { if (Rotella != null) { Rotella(e.Delta); e.Handled = true; } };
    }

    // Evidenziazione che sfuma e pressione elastica: si animano solo opacità e scala.
    // chi usa i tasti su un fondo chiaro può cambiare tinta e forza dell'evidenza (la barra resta bianca)
    public static Color Evidenza = Colors.White;
    public static double Forza = 1;
    readonly SolidColorBrush evidenza = new(Evidenza) { Opacity = 0 };
    readonly ScaleTransform pressione = new(1, 1);
    bool montato;

    void Aggiorna()
    {
        if (!montato)
        {
            montato = true;
            Fondo = evidenza;
            if (Child is UIElement c) { c.RenderTransform = pressione; c.RenderTransformOrigin = new Point(.5, .5); }
        }
        double o = (giu ? .26 : (sopra || acceso) ? .16 : 0) * Forza;
        evidenza.BeginAnimation(Brush.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(o, TimeSpan.FromMilliseconds(giu ? 60 : 140)));
        InvalidateVisual();
        var a = giu
            ? new System.Windows.Media.Animation.DoubleAnimation(.9, TimeSpan.FromMilliseconds(80)) { EasingFunction = new System.Windows.Media.Animation.QuadraticEase() }
            : new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(260)) { EasingFunction = new System.Windows.Media.Animation.BackEase { Amplitude = .6, EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
        pressione.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        pressione.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
