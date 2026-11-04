using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace BarraVascend;

// Lo stile di tutto il sistema (barra, VS Code, terminali), salvato da Vascend Engine in stile.json.
class Stile
{
    public string Nome { get; set; } = "Ghiaccio";

    // interfaccia
    public string Accento { get; set; } = "#D3E65D";
    public string Testo { get; set; } = "#F4F4EF";
    public string Ghiaccio { get; set; } = "#A8C6DA";
    public string Vetro { get; set; } = "#DEF0FC";
    public double Trasparenza { get; set; } = 1;

    // fondi delle app scure (editor, pannelli, barre, campi)
    public string Fondo { get; set; } = "#121A22";
    public string Lato { get; set; } = "#151F29";
    public string Barra { get; set; } = "#18232E";
    public string Alto { get; set; } = "#1D2A36";

    // colori del codice e del terminale
    public string Azzurro { get; set; } = "#78C8FF";
    public string Arancio { get; set; } = "#FFAA5A";
    public string Viola { get; set; } = "#C896FF";
    public string Rosa { get; set; } = "#FF7896";
    public string VerdeAcqua { get; set; } = "#8FD9C8";
    public string Rosso { get; set; } = "#FF6B5E";
    public string Stringhe { get; set; } = "#C9DC7A";
    public string Commenti { get; set; } = "#6E8294";

    // dove sta la barra: sinistra, destra, alto, basso
    public string PosizioneBarra { get; set; } = "basso";

    // caratteri
    public string FontCodice { get; set; } = "JetBrainsMono Nerd Font";
    public string FontTerminale { get; set; } = "";
    public string FontDelTerminale() => string.IsNullOrEmpty(FontTerminale) ? FontCodice : FontTerminale;
    public int DimEditor { get; set; } = 14;
    public int DimTerminale { get; set; } = 11;
    public int Acrilico { get; set; } = 90;
    public string Cursore { get; set; } = "filledBox";

    // Vascend IDE: dove applicare, tema, caratteri (vuoto = come VS Code) e modalità prestazioni (null = non toccare)
    public List<string> IdeDestinazioni { get; set; } = new() { "Installato", "Sviluppo" };
    public bool IdeTema { get; set; } = true;
    public string IdeFont { get; set; } = "";
    public int IdeDim { get; set; } = 14;
    public bool? IdePrestazioni { get; set; }
    public bool IdeIsola { get; set; } = true;

    // isola in alto: dove sta e i moduli, nell'ordine in cui compaiono
    public bool IsolaAttiva { get; set; } = true;
    public string IsolaPosizione { get; set; } = "centro";
    public List<string> IsolaModuli { get; set; } = new() { "musica", "claude" };
    public string IsolaSchermi { get; set; } = "tutti";
    public int IsolaScala { get; set; } = 100;
    public int IsolaOpacita { get; set; } = 100;
    public int IsolaDistanza { get; set; } = 10;
    // cosa mostra ogni modulo
    public bool IsolaCopertina { get; set; } = true;
    public bool IsolaAvanzamento { get; set; } = true;
    public bool IsolaComandi { get; set; } = true;
    public bool IsolaSoloSuona { get; set; } = false;
    public int IsolaTitolo { get; set; } = 170;
    public bool IsolaCinqueOre { get; set; } = true;
    public bool IsolaSetteGiorni { get; set; } = true;
    public bool IsolaCpu { get; set; } = true;
    public bool IsolaRam { get; set; } = true;

    // widget sul desktop
    public List<Widget> Scrivania { get; set; } = new() { new Widget() };

    public static readonly (string id, string nome)[] ModuliIsola = { ("musica", "Musica"), ("claude", "Claude"), ("sistema", "CPU e memoria"), ("ide", "Vascend IDE") };

    public static string Cartella => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".config\vascend-engine");
    static string File_ => Path.Combine(Cartella, "stile.json");
    static readonly JsonSerializerOptions opz = new() { WriteIndented = true };

    static Stile attuale;
    public static Stile Attuale => attuale ??= Carica();

    public static Stile Carica()
    {
        try { if (File.Exists(File_)) return JsonSerializer.Deserialize<Stile>(File.ReadAllText(File_)) ?? new Stile(); }
        catch { }
        return new Stile();
    }

    public void Salva()
    {
        Directory.CreateDirectory(Cartella);
        File.WriteAllText(File_, JsonSerializer.Serialize(this, opz));
        attuale = Clona();
    }

    // Isola e widget si possono cambiare a barra accesa; il resto vale dal prossimo avvio.
    public static void RicaricaDecorazioni()
    {
        var n = Carica();
        var a = Attuale;
        foreach (var p in typeof(Stile).GetProperties())
            if (p.Name.StartsWith("Isola") && p.CanWrite) p.SetValue(a, p.GetValue(n));
        a.Scrivania = n.Scrivania;
    }

    public Stile Clona() => JsonSerializer.Deserialize<Stile>(JsonSerializer.Serialize(this, opz));

    public static Color Colore(string hex)
    {
        try
        {
            var h = hex.Trim().TrimStart('#');
            if (h.Length == 3) h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);
            return Color.FromRgb(Convert.ToByte(h[..2], 16), Convert.ToByte(h[2..4], 16), Convert.ToByte(h[4..6], 16));
        }
        catch { return Colors.Magenta; }
    }

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // Stesso colore con il canale alfa dato (0-1): il formato che usano VS Code e il vetro.
    public static Color ConAlfa(string hex, double a) { var c = Colore(hex); c.A = (byte)Math.Round(Math.Clamp(a, 0, 1) * 255); return c; }

    // Il colore scalato verso il nero (f < 1) o verso il bianco (f > 1).
    public static Color Scala(Color c, double f)
    {
        byte S(byte v) => (byte)Math.Clamp(Math.Round(f <= 1 ? v * f : v + (255 - v) * (f - 1)), 0, 255);
        return Color.FromArgb(c.A, S(c.R), S(c.G), S(c.B));
    }

    public static Stile[] Preset => new[]
    {
        new Stile(),
        new Stile
        {
            Nome = "Notte", Accento = "#7FD1FF", Ghiaccio = "#9DB4E8", Vetro = "#C8D4F5", Trasparenza = 1.1,
            Fondo = "#0E1220", Lato = "#121729", Barra = "#151B30", Alto = "#1B2340",
            Stringhe = "#A6E3A1", Commenti = "#6C7493",
        },
        new Stile
        {
            Nome = "Ambra", Accento = "#FFB454", Ghiaccio = "#E6C9A8", Vetro = "#F6E3CC", Testo = "#F7F1E8",
            Fondo = "#1A1512", Lato = "#1F1915", Barra = "#241D18", Alto = "#2C241E",
            Azzurro = "#8CC8E8", Stringhe = "#D8C47A", Commenti = "#8A7B6C", VerdeAcqua = "#9CD3B8",
        },
        new Stile
        {
            Nome = "Menta", Accento = "#7CF2C2", Ghiaccio = "#A8DAC9", Vetro = "#D6F5EA",
            Fondo = "#0F1A18", Lato = "#13201D", Barra = "#162522", Alto = "#1C2E2A",
            Stringhe = "#B8E07A", Commenti = "#6A8A82",
        },
    };
}

// Un widget del desktop. Quelli con la stessa posizione si mettono in colonna, nell'ordine della lista.
class Widget
{
    public string Tipo { get; set; } = "orologio";
    // alto-sinistra, alto, alto-destra, sinistra, centro, destra, basso-sinistra, basso, basso-destra
    public string Posizione { get; set; } = "centro";
    public string Schermi { get; set; } = "tutti";
    public int Dimensione { get; set; } = 96;
    public double Opacita { get; set; } = .94;
    public string Colore { get; set; } = "#FFFFFF";
    public bool Secondi { get; set; } = true;
    public bool Firma { get; set; } = true;
    public string NomeFirma { get; set; } = "Vascend";
    public string Aspetto { get; set; } = "classico";
    public string Testo { get; set; } = "";
    public string Immagine { get; set; } = "";

    public static readonly (string id, string nome, string glifo, int dimensione)[] Tipi =
    {
        ("orologio", "Orologio", "\uE823", 96), ("data", "Data", "\uE787", 28), ("testo", "Testo", "\uE8D2", 32),
        ("immagine", "Immagine", "\uE91B", 160), ("sistema", "CPU e memoria", "\uE9D9", 16), ("musica", "Musica", "\uE8D6", 16),
    };

    public static Widget Nuovo(string tipo)
    {
        var w = new Widget { Tipo = tipo, Firma = false };
        foreach (var t in Tipi) if (t.id == tipo) w.Dimensione = t.dimensione;
        if (tipo == "testo") w.Testo = "Vascend";
        return w;
    }
}
