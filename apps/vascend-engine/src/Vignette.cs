using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using BarraVascend;

namespace VascendEngine;

// Accanto a ogni opzione una scenetta in loop che mostra cosa fa. Solo transform e opacità.
static class Vignette
{
    const double L = 48, H = 32;

    static readonly Brush Lime = Tema.LimePennello;
    static readonly Brush Pieno = Tema.Pennello(255, 255, 255, .85);
    static readonly Brush Tenue = Tema.Pennello(255, 255, 255, .28);
    static readonly Brush Velo = Tema.Pennello(255, 255, 255, .1);
    static readonly Brush Scuro = Tema.Pennello(20, 28, 36, 1);
    static readonly Brush Blu = Tema.Pennello(96, 165, 250, 1);
    static readonly Brush Rosa = Tema.Pennello(244, 114, 182, 1);

    /// <summary>La scenetta dell'opzione, o null se non ne ha una.</summary>
    public static FrameworkElement Per(string sezione, string nome)
    {
        var s = new Scena();
        Action<Scena> disegna = (sezione, nome) switch
        {
            ("Barra", "Opacità") => BarraOpacita,
            ("Barra", "Avvia la barra con Windows") => AvviaBarra,
            ("Barra", "Nascondi la barra di Windows all'accesso") => NascondiWindows,
            ("Barra", "Riapplica lo stile all'accesso") => Riapplica,
            ("Isola", "Mostra l'isola") => MostraIsola,
            ("Isola", "Posizione") => PosizioneIsola,
            ("Isola", "Schermi") => x => Schermi(x, true),
            ("Isola", "Dimensione") => DimensioneIsola,
            ("Isola", "Opacità del vetro") => VetroIsola,
            ("Isola", "Distanza dal bordo") => DistanzaIsola,
            ("Isola", "Copertina") => Copertina,
            ("Isola", "Comandi") => Comandi,
            ("Isola", "Barra di avanzamento") => Avanzamento,
            ("Isola", "Solo mentre suona") => SoloSuona,
            ("Isola", "Larghezza del titolo") => LarghezzaTitolo,
            ("Isola", "Limite di 5 ore") => x => Limite(x, "5h", .72),
            ("Isola", "Limite di 7 giorni") => x => Limite(x, "7g", .4),
            ("Isola", "CPU") => Cpu,
            ("Isola", "Memoria") => Memoria,
            ("Isola", "modulo:musica") => Musica,
            ("Isola", "modulo:claude") => Claude,
            ("Isola", "modulo:sistema") => Cpu,
            ("Isola", "modulo:ide") => AgentiIsola,
            ("Widget", "Schermi") => x => Schermi(x, false),
            ("Widget", "Dimensione") => DimensioneWidget,
            ("Widget", "Opacità") => OpacitaWidget,
            ("Widget", "Colore") => ColoreWidget,
            ("Widget", "Secondi") => Secondi,
            ("Widget", "Firma sotto l'orologio") => Firma,
            ("Windows", "App") => TemaApp,
            ("Windows", "Windows") => TemaWindows,
            ("Windows", "Effetti di trasparenza") => Trasparenza,
            ("Windows", "Animazioni delle finestre") => AnimazioniFinestre,
            ("Windows", "Accento di Windows") => Accento,
            ("Windows", "Accento su barre del titolo e bordi") => AccentoBordi,
            ("Vascend IDE", "Tema Vascend") or ("VS Code", "Tema Vascend") => TemaCodice,
            ("Vascend IDE", "Dimensione") or ("VS Code", "Dimensione") or ("Terminali", "Dimensione") => Dimensione,
            ("Vascend IDE", "Agenti nell'isola di Vascend Engine") => AgentiIsola,
            ("Vascend IDE", "Modalità prestazioni") => Prestazioni,
            ("Terminali", "Opacità") => OpacitaTerminale,
            ("Terminali", "Cursore") => Cursore,
            _ => null,
        };
        if (disegna == null) return null;
        disegna(s);
        return s.Incornicia();
    }

    // ---------- Barra ----------

    static void BarraOpacita(Scena s)
    {
        Finestra(s, 8, 4, 32, 18);
        var b = s.R(0, 26, L, 6, Lime);
        s.K(b, UIElement.OpacityProperty, 2.6, (0, 1), (.35, 1), (.55, .25), (.85, .25), (1, 1));
    }

    static void AvviaBarra(Scena s)
    {
        var desk = s.R(0, 0, L, H, Velo);
        s.K(desk, UIElement.OpacityProperty, 3, (0, 0), (.15, 1), (.9, 1), (1, 0));
        var b = s.R(0, 26, L, 6, Lime);
        var t = s.Sposta(b);
        s.K(t, TranslateTransform.YProperty, 3, (0, 8), (.2, 8), (.35, 0), (.9, 0), (1, 8));
    }

    static void NascondiWindows(Scena s)
    {
        var win = s.R(0, 26, L, 6, Tenue);
        s.K(s.Sposta(win), TranslateTransform.YProperty, 3, (0, 0), (.25, 0), (.4, 8), (1, 8));
        var b = s.R(0, 26, L, 6, Lime);
        s.K(s.Sposta(b), TranslateTransform.YProperty, 3, (0, 8), (.45, 8), (.6, 0), (.9, 0), (1, 8));
    }

    static void Riapplica(Scena s)
    {
        var f = Finestra(s, 10, 5, 28, 20);
        var tinta = s.R(10, 5, 28, 4, Lime);
        s.K(tinta, UIElement.OpacityProperty, 2.8, (0, 0), (.45, 0), (.6, 1), (.9, 1), (1, 0));
        var g = s.Testo("", 10, Pieno, 19, 12, icona: true);
        s.K(s.Ruota(g, .5, .5), RotateTransform.AngleProperty, 2.8, (0, 0), (.45, 360), (1, 360));
    }

    // ---------- Isola ----------

    static Rectangle Pillola(Scena s, double x = 15, double y = 3, double w = 18, double h = 6, Brush b = null) => s.R(x, y, w, h, b ?? Pieno, h / 2);

    static void MostraIsola(Scena s)
    {
        var p = Pillola(s);
        var sc = s.Scala(p, .5, .5);
        s.K(sc, ScaleTransform.ScaleXProperty, 3, (0, 0), (.15, 1), (.75, 1), (.88, 0), (1, 0));
        s.K(sc, ScaleTransform.ScaleYProperty, 3, (0, 0), (.15, 1), (.75, 1), (.88, 0), (1, 0));
    }

    static void PosizioneIsola(Scena s)
    {
        var p = Pillola(s);
        s.K(s.Sposta(p), TranslateTransform.YProperty, 3.2, (0, 0), (.35, 0), (.5, 20), (.85, 20), (1, 0));
    }

    static void Schermi(Scena s, bool isola)
    {
        s.R(2, 7, 20, 16, Velo);
        s.R(26, 7, 20, 16, Velo);
        var a = isola ? Pillola(s, 7, 9, 10, 4) : s.R(8, 11, 8, 8, Lime);
        var b = isola ? Pillola(s, 31, 9, 10, 4) : s.R(32, 11, 8, 8, Lime);
        s.K(b, UIElement.OpacityProperty, 3, (0, 0), (.4, 0), (.5, 1), (.9, 1), (1, 0));
    }

    static void DimensioneIsola(Scena s)
    {
        var sc = s.Scala(Pillola(s, 13, 4, 22, 7), .5, 0);
        s.K(sc, ScaleTransform.ScaleXProperty, 2.8, (0, .7), (.5, 1.3), (1, .7));
        s.K(sc, ScaleTransform.ScaleYProperty, 2.8, (0, .7), (.5, 1.3), (1, .7));
    }

    static void VetroIsola(Scena s)
    {
        s.R(0, 0, L, H, Tema.Pennello(96, 165, 250, .25));
        s.R(30, 0, 18, H, Tema.Pennello(214, 230, 93, .2));
        var p = Pillola(s, 11, 4, 26, 8);
        s.K(p, UIElement.OpacityProperty, 2.8, (0, 1), (.5, .3), (1, 1));
    }

    static void DistanzaIsola(Scena s)
    {
        s.R(0, 0, L, 1, Tenue);
        var p = Pillola(s, 15, 1, 18, 6);
        s.K(s.Sposta(p), TranslateTransform.YProperty, 2.8, (0, 0), (.5, 9), (1, 0));
    }

    static void Copertina(Scena s)
    {
        Pillola(s, 6, 6, 36, 12, Tenue);
        var c = s.R(8, 8, 8, 8, Lime);
        var sc = s.Scala(c, .5, .5);
        s.K(sc, ScaleTransform.ScaleXProperty, 2.6, (0, 0), (.25, 1), (.8, 1), (1, 0));
        s.K(sc, ScaleTransform.ScaleYProperty, 2.6, (0, 0), (.25, 1), (.8, 1), (1, 0));
        s.R(19, 9, 18, 2, Pieno);
        s.R(19, 13, 12, 2, Tenue);
    }

    static void Comandi(Scena s)
    {
        Pillola(s, 8, 8, 32, 16, Tenue);
        var play = s.Testo("", 9, Lime, 24, 16, icona: true);
        var pausa = s.Testo("", 9, Lime, 24, 16, icona: true);
        s.K(play, UIElement.OpacityProperty, 2.4, (0, 1), (.45, 1), (.5, 0), (.95, 0), (1, 1));
        s.K(pausa, UIElement.OpacityProperty, 2.4, (0, 0), (.45, 0), (.5, 1), (.95, 1), (1, 0));
        s.Testo("", 7, Pieno, 15, 16, icona: true);
        s.Testo("", 7, Pieno, 33, 16, icona: true);
    }

    static void Avanzamento(Scena s)
    {
        Pillola(s, 6, 8, 36, 14, Tenue);
        s.R(10, 12, 20, 2, Pieno);
        s.R(10, 17, 28, 2, Velo);
        var p = s.R(10, 17, 28, 2, Lime);
        s.K(s.Scala(p, 0, .5), ScaleTransform.ScaleXProperty, 3.2, (0, 0), (.9, 1), (1, 1));
    }

    static void SoloSuona(Scena s)
    {
        var p = Pillola(s, 15, 3, 18, 6);
        s.K(p, UIElement.OpacityProperty, 3.2, (0, 1), (.48, 1), (.55, 0), (.95, 0), (1, 1));
        for (int i = 0; i < 4; i++)
        {
            var b = s.R(16 + i * 5, 14, 3, 14, Lime);
            double a = .3 + (i % 2) * .4, c = .9 - (i % 2) * .4;
            s.K(s.Scala(b, .5, 1), ScaleTransform.ScaleYProperty, 3.2, (0, a), (.12, c), (.24, a), (.36, c), (.48, a), (.55, .12), (.95, .12), (1, a));
        }
    }

    static void LarghezzaTitolo(Scena s)
    {
        var p = Pillola(s, 8, 9, 32, 14, Tenue);
        var sc = s.Scala(p, .5, .5);
        s.K(sc, ScaleTransform.ScaleXProperty, 3, (0, .65), (.5, 1.3), (1, .65));
        var t = s.R(14, 15, 20, 2, Pieno);
        s.K(s.Scala(t, .5, .5), ScaleTransform.ScaleXProperty, 3, (0, .65), (.5, 1.3), (1, .65));
    }

    static void Limite(Scena s, string etichetta, double pieno)
    {
        s.Testo(etichetta, 9, Pieno, 12, 11);
        s.R(22, 9, 20, 4, Velo, 2);
        var b = s.R(22, 9, 20, 4, Lime, 2);
        s.K(s.Scala(b, 0, .5), ScaleTransform.ScaleXProperty, 3, (0, .05), (.7, pieno), (.9, pieno), (1, .05));
        s.R(6, 20, 36, 2, Velo);
    }

    static void Cpu(Scena s)
    {
        double[] fasi = { .2, .7, .45, .9, .35 };
        for (int i = 0; i < 5; i++)
        {
            var b = s.R(9 + i * 6, 6, 4, 20, i % 2 == 0 ? Lime : Pieno);
            double f = fasi[i];
            s.K(s.Scala(b, .5, 1), ScaleTransform.ScaleYProperty, 2 + i * .3, (0, f), (.5, 1 - f * .7), (1, f));
        }
    }

    static void Memoria(Scena s)
    {
        s.R(8, 10, 32, 12, Velo, 2);
        var b = s.R(8, 10, 32, 12, Lime, 2);
        s.K(s.Scala(b, 0, .5), ScaleTransform.ScaleXProperty, 4, (0, .35), (.5, .8), (1, .35));
        for (int i = 0; i < 4; i++) s.R(12 + i * 7, 22, 3, 3, Tenue);
    }

    static void Musica(Scena s)
    {
        var n = s.Testo("", 13, Lime, 24, 16, icona: true);
        s.K(s.Sposta(n), TranslateTransform.YProperty, 1.6, (0, 2), (.5, -3), (1, 2));
    }

    static void Claude(Scena s)
    {
        var g = s.Testo("✳", 15, Tema.Pennello(217, 119, 87, 1), 24, 16);
        s.K(s.Ruota(g, .5, .5), RotateTransform.AngleProperty, 4, (0, 0), (1, 360));
    }

    // ---------- Widget ----------

    static void DimensioneWidget(Scena s)
    {
        var w = s.R(16, 8, 16, 16, Lime, 2);
        var sc = s.Scala(w, .5, .5);
        s.K(sc, ScaleTransform.ScaleXProperty, 2.8, (0, .6), (.5, 1.25), (1, .6));
        s.K(sc, ScaleTransform.ScaleYProperty, 2.8, (0, .6), (.5, 1.25), (1, .6));
    }

    static void OpacitaWidget(Scena s)
    {
        s.R(0, 0, 24, H, Tema.Pennello(96, 165, 250, .25));
        var w = s.R(14, 7, 20, 18, Lime, 2);
        s.K(w, UIElement.OpacityProperty, 2.8, (0, 1), (.5, .25), (1, 1));
    }

    static void ColoreWidget(Scena s)
    {
        var a = s.R(16, 8, 16, 16, Lime, 2);
        var b = s.R(16, 8, 16, 16, Blu, 2);
        var c = s.R(16, 8, 16, 16, Rosa, 2);
        s.K(b, UIElement.OpacityProperty, 3.6, (0, 0), (.25, 0), (.33, 1), (.58, 1), (.66, 0), (1, 0));
        s.K(c, UIElement.OpacityProperty, 3.6, (0, 0), (.58, 0), (.66, 1), (.92, 1), (1, 0));
    }

    static void Secondi(Scena s)
    {
        s.Cerchio(12, 4, 24, 24, Tenue);
        var m = s.R(23.5, 9, 1.5, 7, Pieno);
        var sec = s.R(23.8, 6, 1, 10, Lime);
        var r = new RotateTransform(0, .5, 10);
        sec.RenderTransform = r;
        s.K(r, RotateTransform.AngleProperty, 6, (0, 0), (1, 360), lineare: true);
        s.R(23, 15, 3, 3, Pieno, 1.5);
    }

    static void Firma(Scena s)
    {
        s.Testo("12:30", 11, Pieno, 24, 12);
        var f = s.R(12, 22, 24, 1.5, Lime);
        s.K(s.Scala(f, 0, .5), ScaleTransform.ScaleXProperty, 2.8, (0, 0), (.45, 1), (.85, 1), (1, 0));
    }

    // ---------- Windows ----------

    static Rectangle Finestra(Scena s, double x, double y, double w, double h, Brush fondo = null)
    {
        var f = s.R(x, y, w, h, fondo ?? Tenue, 1.5);
        s.R(x, y, w, 4, Tema.Pennello(255, 255, 255, .18), 1.5);
        return f;
    }

    static void TemaApp(Scena s)
    {
        var chiara = s.R(8, 5, 32, 22, Tema.Pennello(240, 243, 245, 1), 1.5);
        var scura = s.R(8, 5, 32, 22, Tema.Pennello(32, 40, 48, 1), 1.5);
        s.K(scura, UIElement.OpacityProperty, 3, (0, 0), (.4, 0), (.5, 1), (.9, 1), (1, 0));
        s.R(12, 12, 16, 2, Lime);
        s.R(12, 17, 22, 2, Tenue);
    }

    static void TemaWindows(Scena s)
    {
        Finestra(s, 10, 4, 28, 16);
        s.R(0, 25, L, 7, Tema.Pennello(240, 243, 245, 1));
        var scura = s.R(0, 25, L, 7, Tema.Pennello(32, 40, 48, 1));
        s.K(scura, UIElement.OpacityProperty, 3, (0, 0), (.4, 0), (.5, 1), (.9, 1), (1, 0));
        s.R(21, 27, 6, 3, Lime);
    }

    static void Trasparenza(Scena s)
    {
        for (int i = 0; i < 4; i++) s.R(i * 12, 0, 6, H, Tema.Pennello(96, 165, 250, .35));
        var f = s.R(8, 6, 32, 20, Tema.Pennello(40, 52, 64, 1), 1.5);
        s.K(f, UIElement.OpacityProperty, 3, (0, 1), (.4, 1), (.55, .55), (.9, .55), (1, 1));
    }

    static void AnimazioniFinestre(Scena s)
    {
        var f = s.R(8, 5, 32, 22, Tenue, 1.5);
        var sc = s.Scala(f, .5, 1);
        s.K(sc, ScaleTransform.ScaleXProperty, 2.8, (0, .3), (.3, 1), (.8, 1), (1, .3));
        s.K(sc, ScaleTransform.ScaleYProperty, 2.8, (0, .3), (.3, 1), (.8, 1), (1, .3));
        s.K(f, UIElement.OpacityProperty, 2.8, (0, 0), (.3, 1), (.8, 1), (1, 0));
    }

    static void Accento(Scena s)
    {
        Finestra(s, 8, 5, 32, 22);
        var a = s.R(12, 14, 24, 6, Lime, 1);
        var b = s.R(12, 14, 24, 6, Blu, 1);
        var c = s.R(12, 14, 24, 6, Rosa, 1);
        s.K(b, UIElement.OpacityProperty, 3.6, (0, 0), (.25, 0), (.33, 1), (.58, 1), (.66, 0), (1, 0));
        s.K(c, UIElement.OpacityProperty, 3.6, (0, 0), (.58, 0), (.66, 1), (.92, 1), (1, 0));
    }

    static void AccentoBordi(Scena s)
    {
        Finestra(s, 8, 5, 32, 22);
        var bordo = s.Bordo(8, 5, 32, 22, Lime);
        var titolo = s.R(8, 5, 32, 4, Lime, 1.5);
        s.K(bordo, UIElement.OpacityProperty, 3, (0, 0), (.35, 0), (.5, 1), (.85, 1), (1, 0));
        s.K(titolo, UIElement.OpacityProperty, 3, (0, 0), (.35, 0), (.5, 1), (.85, 1), (1, 0));
    }

    // ---------- Codice ----------

    static void TemaCodice(Scena s)
    {
        s.R(4, 4, 40, 24, Tema.Pennello(20, 28, 36, .9), 1.5);
        double[] larghe = { 18, 28, 22, 14 };
        Brush[] colori = { Lime, Blu, Rosa, Lime };
        for (int i = 0; i < 4; i++)
        {
            s.R(8, 8 + i * 5, larghe[i], 2, Tenue);
            var c = s.R(8, 8 + i * 5, larghe[i], 2, colori[i]);
            s.K(c, UIElement.OpacityProperty, 3, (0, 0), (.3, 0), (.45 + i * .05, 1), (.88, 1), (1, 0));
        }
    }

    static void Dimensione(Scena s)
    {
        var t = s.Testo("Aa", 14, Pieno, 24, 16);
        var sc = s.Scala(t, .5, .5);
        s.K(sc, ScaleTransform.ScaleXProperty, 2.8, (0, .7), (.5, 1.3), (1, .7));
        s.K(sc, ScaleTransform.ScaleYProperty, 2.8, (0, .7), (.5, 1.3), (1, .7));
    }

    static void AgentiIsola(Scena s)
    {
        var p = Pillola(s, 16, 2, 16, 5);
        Finestra(s, 6, 13, 26, 16);
        var punto = s.R(17, 21, 3, 3, Lime, 1.5);
        var t = s.Sposta(punto);
        s.K(t, TranslateTransform.XProperty, 3, (0, 0), (.25, 0), (.55, 6), (1, 6));
        s.K(t, TranslateTransform.YProperty, 3, (0, 0), (.25, 0), (.55, -17), (1, -17));
        s.K(punto, UIElement.OpacityProperty, 3, (0, 1), (.55, 1), (.6, 0), (.95, 0), (1, 1));
        var sc = s.Scala(p, .5, .5);
        s.K(sc, ScaleTransform.ScaleXProperty, 3, (0, 1), (.55, 1), (.65, 1.35), (.8, 1.35), (.9, 1), (1, 1));
        s.K(p, UIElement.OpacityProperty, 3, (0, .45), (.55, .45), (.62, 1), (.9, 1), (1, .45));
    }

    static void Prestazioni(Scena s)
    {
        var area = new Canvas { Width = 40, Height = 24, ClipToBounds = true };
        Canvas.SetLeft(area, 4); Canvas.SetTop(area, 4);
        s.Tela.Children.Add(area);
        area.Children.Add(new Rectangle { Width = 40, Height = 24, Fill = Tema.Pennello(20, 28, 36, .9), RadiusX = 1.5, RadiusY = 1.5 });
        var macchia = new Ellipse { Width = 26, Height = 26, Fill = new RadialGradientBrush(Color.FromArgb(150, Tema.Lime.R, Tema.Lime.G, Tema.Lime.B), Colors.Transparent) };
        Canvas.SetLeft(macchia, -6); Canvas.SetTop(macchia, 2);
        area.Children.Add(macchia);
        var t = new TranslateTransform();
        macchia.RenderTransform = t;
        s.K(t, TranslateTransform.XProperty, 4, (0, 0), (.5, 26), (1, 0));
        s.K(t, TranslateTransform.YProperty, 3, (0, 0), (.5, -6), (1, 0));
    }

    // ---------- Terminali ----------

    static void OpacitaTerminale(Scena s)
    {
        for (int i = 0; i < 4; i++) s.R(i * 12, 0, 6, H, Tema.Pennello(214, 230, 93, .3));
        var term = s.R(6, 5, 36, 22, Tema.Pennello(20, 28, 36, 1), 1.5);
        s.K(term, UIElement.OpacityProperty, 2.8, (0, 1), (.5, .45), (1, 1));
        s.Testo(">_", 9, Lime, 15, 13);
    }

    static void Cursore(Scena s)
    {
        s.R(4, 5, 40, 22, Tema.Pennello(20, 28, 36, .9), 1.5);
        s.Testo(">", 10, Lime, 12, 16);
        s.R(17, 15, 12, 2, Pieno);
        var c = s.R(31, 11, 5, 10, Pieno);
        s.K(c, UIElement.OpacityProperty, 1.1, (0, 1), (.5, 1), (.51, 0), (.99, 0), (1, 1), lineare: true, scatti: true);
    }

    // Tela della scenetta: le animazioni partono quando compare e si fermano quando sparisce.
    sealed class Scena
    {
        public readonly Canvas Tela = new() { Width = L, Height = H, ClipToBounds = true };
        readonly List<(IAnimatable o, DependencyProperty p, AnimationTimeline a)> animazioni = new();

        public Rectangle R(double x, double y, double w, double h, Brush b, double raggio = 0)
        {
            var r = new Rectangle { Width = w, Height = h, Fill = b, RadiusX = raggio, RadiusY = raggio };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
            Tela.Children.Add(r);
            return r;
        }

        public Rectangle Bordo(double x, double y, double w, double h, Brush b)
        {
            var r = new Rectangle { Width = w, Height = h, Stroke = b, StrokeThickness = 1.2, RadiusX = 1.5, RadiusY = 1.5 };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, y);
            Tela.Children.Add(r);
            return r;
        }

        public Ellipse Cerchio(double x, double y, double w, double h, Brush b)
        {
            var e = new Ellipse { Width = w, Height = h, Stroke = b, StrokeThickness = 1.5 };
            Canvas.SetLeft(e, x); Canvas.SetTop(e, y);
            Tela.Children.Add(e);
            return e;
        }

        /// <summary>Testo o icona centrati in (cx, cy).</summary>
        public FrameworkElement Testo(string t, double dim, Brush b, double cx, double cy, bool icona = false)
        {
            var tb = icona ? Tema.Glifo(t, dim, b) : Tema.Scritta(t, dim, b, FontWeights.SemiBold);
            var box = new Grid { Width = 30, Height = 20 };
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            box.Children.Add(tb);
            Canvas.SetLeft(box, cx - 15); Canvas.SetTop(box, cy - 10);
            Tela.Children.Add(box);
            return box;
        }

        TransformGroup Gruppo(UIElement e, double ox, double oy)
        {
            if (e.RenderTransform is TransformGroup g) return g;
            g = new TransformGroup();
            g.Children.Add(new ScaleTransform());
            g.Children.Add(new RotateTransform());
            g.Children.Add(new TranslateTransform());
            e.RenderTransform = g;
            e.RenderTransformOrigin = new Point(ox, oy);
            return g;
        }

        public ScaleTransform Scala(UIElement e, double ox, double oy) => (ScaleTransform)Gruppo(e, ox, oy).Children[0];
        public RotateTransform Ruota(UIElement e, double ox, double oy) => (RotateTransform)Gruppo(e, ox, oy).Children[1];
        public TranslateTransform Sposta(UIElement e) => (TranslateTransform)Gruppo(e, .5, .5).Children[2];

        /// <summary>Animazione in loop: coppie (frazione del giro, valore).</summary>
        public void K(IAnimatable o, DependencyProperty p, double secondi, params (double t, double v)[] passi) => K(o, p, secondi, false, false, passi);

        public void K(IAnimatable o, DependencyProperty p, double secondi, (double t, double v) a, (double t, double v) b, bool lineare) => K(o, p, secondi, lineare, false, new[] { a, b });

        public void K(IAnimatable o, DependencyProperty p, double secondi, (double t, double v) a, (double t, double v) b, (double t, double v) c, (double t, double v) d, (double t, double v) e, bool lineare, bool scatti) => K(o, p, secondi, lineare, scatti, new[] { a, b, c, d, e });

        void K(IAnimatable o, DependencyProperty p, double secondi, bool lineare, bool scatti, (double t, double v)[] passi)
        {
            var a = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(secondi), RepeatBehavior = RepeatBehavior.Forever };
            var morbido = new SineEase { EasingMode = EasingMode.EaseInOut };
            foreach (var (t, v) in passi)
            {
                var kt = KeyTime.FromPercent(t);
                a.KeyFrames.Add(scatti ? new DiscreteDoubleKeyFrame(v, kt) : lineare ? new LinearDoubleKeyFrame(v, kt) : new EasingDoubleKeyFrame(v, kt, morbido));
            }
            a.Freeze();
            animazioni.Add((o, p, a));
        }

        public FrameworkElement Incornicia()
        {
            var c = new Cornice
            {
                Profilo = Gradini.Minimo, Rilievo = false, Fondo = Tema.Pennello(0, 0, 0, .28), Bordo = Tema.Pennello(255, 255, 255, .12),
                Width = L + 4, Height = H + 4, Child = Tela, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center,
                LayoutTransform = new ScaleTransform(.82, .82),
            };
            Tela.Margin = new Thickness(2);
            c.Loaded += (_, _) => { foreach (var (o, p, a) in animazioni) o.BeginAnimation(p, a); };
            c.Unloaded += (_, _) => { foreach (var (o, p, _) in animazioni) o.BeginAnimation(p, null); };
            return c;
        }
    }
}
