using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VascendEngine;

// Palette dell'host indipendente dallo stile desktop configurato da Ghiaccio.
static class Tema
{
    public static readonly FontFamily Icone = BarraVascend.Tema.Icone;
    public static readonly FontFamily Testo = BarraVascend.Tema.Testo;
    public static readonly Color Lime = Color.FromRgb(211, 230, 93);
    public static readonly SolidColorBrush Bianco = Pennello(236, 235, 230, 1);
    public static readonly SolidColorBrush BiancoTenue = Pennello(159, 161, 150, 1);
    public static readonly SolidColorBrush LimePennello = Congela(new SolidColorBrush(Lime));
    public static readonly Brush GhiaccioPannello = Pennello(19, 20, 18, 1);
    public static readonly Brush Ghiaccio = Pennello(34, 37, 30, 1);
    public static readonly Brush Bordo = Pennello(57, 61, 49, 1);
    public static SolidColorBrush Pennello(byte r, byte g, byte b, double a) => BarraVascend.Tema.Pennello(r,g,b,a);
    public static T Congela<T>(T value) where T : Freezable { value.Freeze(); return value; }
    public static TextBlock Scritta(string text, double size = 13, Brush color = null, FontWeight? weight = null)
        => BarraVascend.Tema.Scritta(text,size,color ?? Bianco,weight);
    public static TextBlock Glifo(string glyph, double size = 16, Brush color = null)
        => BarraVascend.Tema.Glifo(glyph,size,color ?? Bianco);
}
