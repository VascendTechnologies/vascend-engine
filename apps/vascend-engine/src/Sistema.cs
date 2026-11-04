using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using BarraVascend;

namespace VascendEngine;

// Le impostazioni di Windows che l'Engine sa leggere e cambiare: tutte dell'utente, niente che chieda l'amministratore.
static class Sistema
{
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern int AddFontResource(string f);

    /// <summary>Installa il font per l'utente corrente e ne restituisce la famiglia.</summary>
    public static string InstallaFont(string file)
    {
        var tipo = new System.Windows.Media.GlyphTypeface(new Uri(file));
        var it = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        string Nome(System.Collections.Generic.IDictionary<System.Globalization.CultureInfo, string> n) => n.TryGetValue(it, out var v) ? v : System.Linq.Enumerable.First(n.Values);
        var famiglia = Nome(tipo.Win32FamilyNames);
        var cartella = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
        Directory.CreateDirectory(cartella);
        var meta = Path.Combine(cartella, Path.GetFileName(file));
        if (!string.Equals(Path.GetFullPath(file), meta, StringComparison.OrdinalIgnoreCase))
        {
            bool uguale=File.Exists(meta) && System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)).SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(meta)));
            if(!uguale)File.Copy(file,meta,true);
        }
        var voce = $"{famiglia} {Nome(tipo.Win32FaceNames)} ({(file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ? "OpenType" : "TrueType")})";
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts")) k.SetValue(voce, meta);
        AddFontResource(meta);
        System.Threading.Tasks.Task.Run(() => SendMessageTimeout((IntPtr)0xFFFF, 0x1D, IntPtr.Zero, null!, 2, 1000, out _));
        return famiglia;
    }

    const string Personalizza = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string Dwm = @"Software\Microsoft\Windows\DWM";
    const string Accento = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    const string Avvio = @"Software\Microsoft\Windows\CurrentVersion\Run";

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint a, uint u, string p, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint a, uint u, StringBuilder p, uint f);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint u, ref bool p, uint f);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint u, IntPtr p, uint f);
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint a, uint u, ref ANIMATIONINFO p, uint f);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, string l, uint f, uint t, out IntPtr r);
    [StructLayout(LayoutKind.Sequential)] struct ANIMATIONINFO { public uint cbSize; public int iMinAnimate; }

    static int Leggi(string chiave, string nome, int predefinito)
    {
        using var k = Registry.CurrentUser.OpenSubKey(chiave);
        return k?.GetValue(nome) is int v ? v : predefinito;
    }

    static void Scrivi(string chiave, string nome, object v, RegistryValueKind tipo = RegistryValueKind.DWord)
    {
        using var k = Registry.CurrentUser.CreateSubKey(chiave);
        k.SetValue(nome, v, tipo);
    }

    // Le app (Esplora, Impostazioni, le barre dei titoli) rileggono colori e tema quando ricevono questo avviso.
    // L'avviso passa da tutte le finestre aperte e può durare secondi: va fuori dal thread della finestra.
    static void Avvisa(string cosa = "ImmersiveColorSet")
    {
        System.Threading.Tasks.Task.Run(() => SendMessageTimeout((IntPtr)0xFFFF, 0x1A, IntPtr.Zero, cosa, 2, 1000, out _));
    }

    // ---------- tema ----------

    public static bool AppScure
    {
        get => Leggi(Personalizza, "AppsUseLightTheme", 1) == 0;
        set { Scrivi(Personalizza, "AppsUseLightTheme", value ? 0 : 1); Avvisa(); }
    }

    public static bool SistemaScuro
    {
        get => Leggi(Personalizza, "SystemUsesLightTheme", 1) == 0;
        set { Scrivi(Personalizza, "SystemUsesLightTheme", value ? 0 : 1); Avvisa(); }
    }

    public static bool Trasparenza
    {
        get => Leggi(Personalizza, "EnableTransparency", 1) == 1;
        set { Scrivi(Personalizza, "EnableTransparency", value ? 1 : 0); Avvisa(); }
    }

    public static bool AccentoSuTitoli
    {
        get => Leggi(Dwm, "ColorPrevalence", 0) == 1;
        set { Scrivi(Dwm, "ColorPrevalence", value ? 1 : 0); Avvisa(); }
    }

    public static string ColoreAccento
    {
        get
        {
            int abgr = Leggi(Accento, "AccentColorMenu", unchecked((int)0xFFD77800));
            return $"#{abgr & 0xFF:X2}{(abgr >> 8) & 0xFF:X2}{(abgr >> 16) & 0xFF:X2}";
        }
    }

    // Mentre si trascina nel selettore arrivano decine di colori al secondo: si applica solo l'ultimo, a mano ferma.
    static System.Threading.Timer attesa;
    static string inAttesa;
    static readonly object chiave = new();
    public static void ImpostaAccentoPiano(string hex)
    {
        lock (chiave)
        {
            inAttesa = hex;
            attesa ??= new System.Threading.Timer(_ => { string h; lock (chiave) h = inAttesa; ImpostaAccento(h); });
            attesa.Change(250, System.Threading.Timeout.Infinite);
        }
    }

    // Accento di Windows: la tavolozza di otto toni (dal più chiaro al più scuro) e i valori del DWM.
    public static void ImpostaAccento(string hex)
    {
        var c = Stile.Colore(hex);
        double[] f = { 1.55, 1.35, 1.15, 1, .82, .64, .46, .3 };
        var tav = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            var t = Stile.Scala(c, f[i]);
            tav[i * 4] = t.R; tav[i * 4 + 1] = t.G; tav[i * 4 + 2] = t.B; tav[i * 4 + 3] = 0;
        }
        int Abgr(System.Windows.Media.Color x) => unchecked((int)(0xFF000000u | (uint)x.B << 16 | (uint)x.G << 8 | x.R));
        Scrivi(Accento, "AccentPalette", tav, RegistryValueKind.Binary);
        Scrivi(Accento, "AccentColorMenu", Abgr(c));
        Scrivi(Accento, "StartColorMenu", Abgr(Stile.Scala(c, .64)));
        Scrivi(Dwm, "AccentColor", Abgr(c));
        Scrivi(Dwm, "ColorizationColor", unchecked((int)(0xC4000000u | (uint)c.R << 16 | (uint)c.G << 8 | c.B)));
        Scrivi(Dwm, "ColorizationAfterglow", unchecked((int)(0xC4000000u | (uint)c.R << 16 | (uint)c.G << 8 | c.B)));
        using (var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop")) k.SetValue("AutoColorization", "0");
        Avvisa();
    }

    // ---------- sfondo ----------

    public static string Sfondo
    {
        get
        {
            var sb = new StringBuilder(520);
            SystemParametersInfo(0x73, (uint)sb.Capacity, sb, 0);
            var p = sb.ToString();
            if (File.Exists(p)) return p;
            var t = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            return File.Exists(t) ? t : null;
        }
    }

    public static void ImpostaSfondo(string percorso) => SystemParametersInfo(0x14, 0, percorso, 3);

    // ---------- animazioni ----------

    public static bool Animazioni
    {
        get
        {
            var a = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>() };
            SystemParametersInfo(0x48, a.cbSize, ref a, 0);
            return a.iMinAnimate != 0;
        }
        set
        {
            var a = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>(), iMinAnimate = value ? 1 : 0 };
            SystemParametersInfo(0x49, a.cbSize, ref a, 3);
            SystemParametersInfo(0x1043, 0, (IntPtr)(value ? 1 : 0), 3);
            using var k = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop\WindowMetrics");
            k.SetValue("MinAnimate", value ? "1" : "0");
        }
    }

    // ---------- barra ----------

    static string EseguibileBarra => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".config\barra-vascend\bin\BarraVascend.exe");

    public static bool BarraAllAvvio
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(Avvio); return k?.GetValue("BarraVascend") != null; }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(Avvio);
            if (value) k.SetValue("BarraVascend", $"\"{EseguibileBarra}\"");
            else k.DeleteValue("BarraVascend", false);
        }
    }

    // lo script che all'accesso nasconde la barra di Windows (resta nel registro, si toglie solo il richiamo)
    public static bool NascondiBarraWindows
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(Avvio); return k?.GetValue("VascendNascondiBarra") != null; }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(Avvio);
            var script = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".config\yasb\barra-windows.ps1");
            if (value) k.SetValue("VascendNascondiBarra", $"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File {script}");
            else k.DeleteValue("VascendNascondiBarra", false);
        }
    }

    public static bool EngineAllAvvio
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(Avvio); return k?.GetValue("VascendEngine") != null; }
        set
        {
            using var k = Registry.CurrentUser.CreateSubKey(Avvio);
            // all'avvio l'Engine riapplica lo stile salvato senza aprire la finestra
            if (value) k.SetValue("VascendEngine", $"\"{Environment.ProcessPath}\" --applica vscode terminali");
            else k.DeleteValue("VascendEngine", false);
        }
    }
}
