using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BarraVascend;

namespace VascendEngine;

// Anteprima dal vivo: la barra a sinistra, VS Code con del codice e Windows Terminal, tutti con lo stile in modifica.
static class Anteprima
{
    static SolidColorBrush P(string hex, double a = 1) => new(Stile.ConAlfa(hex, a));

    public static UIElement Costruisci(Stile s)
    {
        var mono = new FontFamily(s.FontCodice + ", Cascadia Mono, Consolas");
        var radice = new Grid { ClipToBounds = true };
        radice.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        radice.ColumnDefinitions.Add(new ColumnDefinition());
        // un desktop scuro qualsiasi dietro, per vedere il vetro
        radice.Background = new LinearGradientBrush(Color.FromRgb(38, 52, 70), Color.FromRgb(14, 18, 26), 60);

        radice.Children.Add(BarraFinta(s));

        var destra = new Grid { Margin = new Thickness(14, 14, 14, 14) };
        destra.RowDefinitions.Add(new RowDefinition { Height = new GridLength(62, GridUnitType.Star) });
        destra.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
        destra.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38, GridUnitType.Star) });
        Grid.SetColumn(destra, 1);
        radice.Children.Add(destra);

        destra.Children.Add(VsCode(s, mono));
        var t = Terminale(s, mono);
        Grid.SetRow(t, 2);
        destra.Children.Add(t);
        return radice;
    }

    public static UIElement BarraFinta(Stile s)
    {
        Color V(double r, double g, double b, double a)
        {
            var v = Stile.Colore(s.Vetro);
            return Color.FromArgb((byte)Math.Clamp(Math.Round(a * s.Trasparenza), 0, 255), (byte)Math.Round(v.R * r), (byte)Math.Round(v.G * g), (byte)Math.Round(v.B * b));
        }
        var vetro = new LinearGradientBrush(new GradientStopCollection
        {
            new(V(1, 1, 1, 87), 0), new(V(176 / 222.0, 206 / 240.0, 228 / 252.0, 61), .55), new(V(130 / 222.0, 162 / 240.0, 188 / 252.0, 66), 1),
        }, 90);
        var col = new StackPanel { Margin = new Thickness(0, 10, 0, 10) };
        var logo = new System.Windows.Shapes.Rectangle
        {
            Width = 16, Height = 16, Margin = new Thickness(0, 4, 0, 12),
            Fill = new LinearGradientBrush(Stile.Scala(Stile.Colore(s.Ghiaccio), 1.85), Stile.Colore(s.Ghiaccio), 90),
        };
        var volpe = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".config\barra-vascend\volpe.png");
        if (System.IO.File.Exists(volpe)) logo.OpacityMask = new ImageBrush(new System.Windows.Media.Imaging.BitmapImage(new Uri(volpe)));
        col.Children.Add(logo);
        string[] icone = { "", "", "", "", "" };
        for (int i = 0; i < icone.Length; i++)
        {
            var g = new Grid { Height = 32 };
            g.Children.Add(Tema.Glifo(icone[i], 13, i == 2 ? P(s.Testo) : P(s.Testo, .7)));
            if (i == 2) g.Children.Add(new Border { Width = 3, Height = 14, HorizontalAlignment = HorizontalAlignment.Left, Background = P(s.Accento) });
            else if (i > 0) g.Children.Add(new Border { Width = 3, Height = 5, HorizontalAlignment = HorizontalAlignment.Left, Background = P(s.Testo, .55) });
            col.Children.Add(g);
        }
        var orario = Tema.Scritta(DateTime.Now.ToString("HH:mm"), 11, P(s.Testo), FontWeights.SemiBold);
        orario.HorizontalAlignment = HorizontalAlignment.Center;
        orario.Margin = new Thickness(0, 18, 0, 0);
        col.Children.Add(orario);
        return new Cornice { Profilo = Gradini.Barra, Fondo = vetro, Bordo = Tema.Bordo, Width = 40, Margin = new Thickness(8, 8, 0, 8), Child = col };
    }

    static TextBlock Riga(FontFamily mono, double dim, params (string testo, Brush colore, bool corsivo)[] pezzi)
    {
        var t = new TextBlock { FontFamily = mono, FontSize = dim, Margin = new Thickness(0, 1, 0, 1) };
        foreach (var (testo, colore, corsivo) in pezzi)
            t.Inlines.Add(new System.Windows.Documents.Run(testo) { Foreground = colore, FontStyle = corsivo ? FontStyles.Italic : FontStyles.Normal });
        return t;
    }

    public static UIElement VsCode(Stile s, FontFamily mono = null)
    {
        var bordo = P(s.Vetro, .10);
        var fin = new Grid();
        fin.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
        fin.RowDefinitions.Add(new RowDefinition());
        fin.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

        var titolo = new Border { Background = P(s.Barra), BorderBrush = bordo, BorderThickness = new Thickness(0, 0, 0, 1) };
        var cerca = new Border { Width = 220, Height = 16, Background = P("#FFFFFF", .05), BorderBrush = bordo, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center };
        cerca.Child = Tema.Scritta("progetti", 9.5, P(s.Testo, .5));
        ((TextBlock)cerca.Child).HorizontalAlignment = HorizontalAlignment.Center;
        titolo.Child = cerca;
        fin.Children.Add(titolo);

        var corpo = new Grid { Background = P(s.Fondo) };
        corpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        corpo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        corpo.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetRow(corpo, 1);
        fin.Children.Add(corpo);

        var attivita = new StackPanel { Background = P(s.Barra) };
        string[] ico = { "", "", "", "", "" };
        for (int i = 0; i < ico.Length; i++)
        {
            var g = new Grid { Height = 34 };
            g.Children.Add(Tema.Glifo(ico[i], 14, i == 0 ? P(s.Testo) : P(s.Testo, .36)));
            if (i == 0) g.Children.Add(new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Left, Background = P(s.Accento) });
            if (i == 2)
            {
                var b = new Border { Width = 12, Height = 12, Background = P(s.Accento), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 5, 5) };
                b.Child = new TextBlock { Text = "3", FontSize = 8, FontWeight = FontWeights.Bold, Foreground = P(s.Fondo), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(b);
            }
            attivita.Children.Add(g);
        }
        corpo.Children.Add(new Border { Background = P(s.Barra), BorderBrush = bordo, BorderThickness = new Thickness(0, 0, 1, 0), Child = attivita });

        var lato = new StackPanel { Background = P(s.Lato) };
        var tl = Tema.Scritta("ESPLORA", 9, P(s.Testo), FontWeights.SemiBold);
        tl.Margin = new Thickness(10, 8, 0, 6);
        lato.Children.Add(tl);
        (string nome, string colore, bool sel)[] file = { ("src", null, false), ("Stile.cs", s.Accento, true), ("Barra.cs", s.Azzurro, false), ("Tema.json", null, false), ("note.md", s.Rosso, false) };
        foreach (var (nome, colore, scelto) in file)
        {
            var r = new Border { Background = scelto ? P(s.Ghiaccio, .2) : Brushes.Transparent, Padding = new Thickness(scelto || nome != "src" ? 22 : 10, 3, 6, 3) };
            r.Child = Tema.Scritta(nome, 10.5, colore != null ? P(colore) : P(s.Testo, .62));
            lato.Children.Add(r);
        }
        Grid.SetColumn(lato, 1);
        corpo.Children.Add(new Border { Background = P(s.Lato), BorderBrush = bordo, BorderThickness = new Thickness(0, 0, 1, 0), Child = lato });
        Grid.SetColumn(corpo.Children[^1], 1);

        var editor = new Grid();
        editor.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
        editor.RowDefinitions.Add(new RowDefinition());
        Grid.SetColumn(editor, 2);
        corpo.Children.Add(editor);
        var schede = new StackPanel { Orientation = Orientation.Horizontal, Background = P(s.Barra) };
        foreach (var (nome, attiva) in new[] { ("Stile.cs", true), ("Barra.cs", false) })
        {
            var sc = new Border
            {
                Background = attiva ? P(s.Fondo) : P(s.Barra), BorderBrush = attiva ? P(s.Accento) : bordo,
                BorderThickness = attiva ? new Thickness(0, 2, 0, 0) : new Thickness(0, 0, 1, 1), Padding = new Thickness(12, 0, 12, 0),
            };
            sc.Child = Tema.Scritta(nome, 10.5, attiva ? P(s.Testo) : P(s.Testo, .36));
            schede.Children.Add(sc);
        }
        editor.Children.Add(schede);

        var kw = P(s.Ghiaccio); var str = P(s.Stringhe); var fn = P(s.Azzurro); var ty = P(s.VerdeAcqua); var num = P(s.Arancio);
        var com = P(s.Commenti); var tx = P(s.Testo); var punt = new SolidColorBrush(Generatori.Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Ghiaccio), .45));
        var att = P(s.Viola); var qs = P(s.Rosa);
        var param = new SolidColorBrush(Generatori.Mescola(Stile.Colore(s.Testo), Stile.Colore(s.Arancio), .18));
        double d = Math.Max(9, s.DimEditor * .78);
        var righe = new List<TextBlock>
        {
            Riga(new FontFamily(s.FontCodice), d, ("// lo stile di tutto il sistema", com, true)),
            Riga(new FontFamily(s.FontCodice), d, ("[", punt, false), ("Serializable", att, false), ("]", punt, false)),
            Riga(new FontFamily(s.FontCodice), d, ("public class ", kw, false), ("Stile", ty, false), (" {", punt, false)),
            Riga(new FontFamily(s.FontCodice), d, ("    public string ", kw, false), ("Accento", tx, false), (" = ", punt, false), ("\"" + Stile.Hex(Stile.Colore(s.Accento)) + "\"", str, false), (";", punt, false)),
            Riga(new FontFamily(s.FontCodice), d, ("    int ", kw, false), ("dimensione", tx, false), (" = ", punt, false), (s.DimEditor.ToString(), num, false), (";", punt, false)),
            Riga(new FontFamily(s.FontCodice), d, ("    void ", kw, false), ("Applica", fn, false), ("(", punt, false), ("Barra ", ty, false), ("b", param, true), (") => ", punt, false), ("b", tx, false), (".", punt, false), ("Riavvia", fn, false), ("(", punt, false), ("this", qs, true), (");", punt, false)),
            Riga(new FontFamily(s.FontCodice), d, ("}", punt, false)),
        };
        var codice = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        codice.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
        codice.ColumnDefinitions.Add(new ColumnDefinition());
        var numeri = new StackPanel();
        var linee = new StackPanel();
        for (int i = 0; i < righe.Count; i++)
        {
            var n = Riga(new FontFamily(s.FontCodice), d, ((i + 1).ToString(), i == 3 ? P(s.Ghiaccio) : P(s.Testo, .2), false));
            n.HorizontalAlignment = HorizontalAlignment.Right;
            n.Margin = new Thickness(0, 1, 10, 1);
            numeri.Children.Add(n);
            if (i == 3)
            {
                var ev = new Grid { Background = P("#FFFFFF", .03) };
                ev.Children.Add(righe[i]);
                ev.Children.Add(new Border { Width = 2, Background = P(s.Accento), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(Larghezza(righe[i]) + 1, 1, 0, 1) });
                linee.Children.Add(ev);
            }
            else linee.Children.Add(righe[i]);
        }
        codice.Children.Add(numeri);
        Grid.SetColumn(linee, 1);
        codice.Children.Add(linee);
        Grid.SetRow(codice, 1);
        editor.Children.Add(codice);

        var stato = new Grid { Background = P(s.Barra) };
        var remoto = new Border { Background = P(s.Accento), Padding = new Thickness(8, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Left };
        remoto.Child = Tema.Scritta("WSL: Ubuntu", 9.5, P(s.Fondo), FontWeights.SemiBold);
        stato.Children.Add(remoto);
        var dx = Tema.Scritta("UTF-8   C#", 9.5, P(s.Testo, .62));
        dx.HorizontalAlignment = HorizontalAlignment.Right;
        dx.Margin = new Thickness(0, 0, 10, 0);
        stato.Children.Add(dx);
        Grid.SetRow(stato, 2);
        fin.Children.Add(stato);

        return new Border { BorderBrush = bordo, BorderThickness = new Thickness(1), Child = fin, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = .45 } };
    }

    static double Larghezza(TextBlock t)
    {
        t.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return t.DesiredSize.Width;
    }

    public static UIElement Terminale(Stile s, FontFamily mono = null)
    {
        var ansi = new Dictionary<string, Brush>();
        foreach (var (k, v) in Generatori.Ansi(s)) ansi[k] = P(v);
        var fin = new Grid();
        fin.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        fin.RowDefinitions.Add(new RowDefinition());
        var schede = new StackPanel { Orientation = Orientation.Horizontal };
        var sc = new Border { Background = P(s.Fondo), Padding = new Thickness(12, 0, 14, 0), Margin = new Thickness(6, 4, 0, 0) };
        var st = new StackPanel { Orientation = Orientation.Horizontal };
        st.Children.Add(Tema.Glifo("", 10, P(s.Testo, .7)));
        var nome = Tema.Scritta("Ubuntu", 10.5, P(s.Testo));
        nome.Margin = new Thickness(8, 0, 0, 0);
        st.Children.Add(nome);
        sc.Child = st;
        schede.Children.Add(sc);
        schede.Children.Add(Tema.Glifo("", 10, P(s.Testo, .5)));
        ((FrameworkElement)schede.Children[^1]).Margin = new Thickness(10, 4, 0, 0);
        fin.Children.Add(new Border { Background = P(s.Barra), Child = schede });

        double d = Math.Max(9, s.DimTerminale * 1.0);
        var f = new FontFamily(s.FontDelTerminale() + ", Cascadia Mono, Consolas");
        var testo = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };
        testo.Children.Add(Riga(f, d, ("utente@pc", ansi["green"], false), (":", ansi["white"], false), ("~/progetti/vascend", ansi["blue"], false), ("$ ", ansi["white"], false), ("git status -s", ansi["brightWhite"], false)));
        testo.Children.Add(Riga(f, d, (" M ", ansi["red"], false), ("src/Stile.cs", ansi["white"], false)));
        testo.Children.Add(Riga(f, d, ("?? ", ansi["green"], false), ("src/Anteprima.cs", ansi["white"], false)));
        testo.Children.Add(Riga(f, d, ("utente@pc", ansi["green"], false), (":", ansi["white"], false), ("~/progetti/vascend", ansi["blue"], false), ("$ ", ansi["white"], false), ("dotnet build", ansi["brightWhite"], false)));
        testo.Children.Add(Riga(f, d, ("  avvisi ", ansi["yellow"], false), ("1", ansi["magenta"], false), ("  errori ", ansi["cyan"], false), ("0", ansi["brightBlack"], false)));
        var ultima = Riga(f, d, ("utente@pc", ansi["green"], false), (":", ansi["white"], false), ("~/progetti/vascend", ansi["blue"], false), ("$ ", ansi["white"], false));
        // il cursore con la forma scelta per Windows Terminal
        var forma = s.Cursore switch
        {
            "bar" => new Border { Width = 2, Height = d * 1.2, Background = P(s.Accento) },
            "underscore" => new Border { Width = d * .62, Height = d * 1.2, BorderBrush = P(s.Accento), BorderThickness = new Thickness(0, 0, 0, 2) },
            "emptyBox" => new Border { Width = d * .62, Height = d * 1.2, BorderBrush = P(s.Accento), BorderThickness = new Thickness(1.5) },
            _ => new Border { Width = d * .62, Height = d * 1.2, Background = P(s.Accento) },
        };
        var cur = new System.Windows.Documents.InlineUIContainer(forma) { BaselineAlignment = BaselineAlignment.Center };
        ultima.Inlines.Add(cur);
        testo.Children.Add(ultima);
        var corpo = new Border { Background = P(s.Fondo, s.Acrilico / 100.0), Child = testo };
        Grid.SetRow(corpo, 1);
        fin.Children.Add(corpo);
        return new Border { BorderBrush = P(s.Vetro, .10), BorderThickness = new Thickness(1), Child = fin, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = .45 } };
    }
}
