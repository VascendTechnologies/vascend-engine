using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media;

namespace BarraVascend;

class Finestra
{
    public IntPtr H;
    public string Titolo;
    public uint Pid;
}

// Un'app sulla barra: fissata, aperta o entrambe.
class Gruppo
{
    public string Chiave;
    public string Exe;
    public string Aumid;
    public string Nome;
    public ImageSource Icona;
    public Fissata Fissata;
    public List<Finestra> Finestre = new();
    public bool Attiva;
    public bool Attenzione;

    public string Firma => Chiave + ":" + Finestre.Count;
}

class Fissata
{
    public string Percorso;
    public string Exe;
    public string Aumid;
    public string Nome;
    public ImageSource Icona;

    public static Fissata Da(string percorso)
    {
        var f = new Fissata { Percorso = percorso };
        if (percorso.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
        {
            f.Aumid = percorso.Substring(17);
            f.Nome = Shell.NomeVisualizzato(percorso) ?? f.Aumid;
            f.Icona = Shell.IconaApp(f.Aumid);
            return f;
        }
        if (percorso.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            f.Exe = Shell.DestinazioneLnk(percorso);
            f.Aumid = Shell.AumidFile(percorso);
            f.Nome = Path.GetFileNameWithoutExtension(percorso);
            // Esplora file è un collegamento a una posizione della shell, senza eseguibile
            if (f.Exe == null && (f.Aumid == "Microsoft.Windows.Explorer" || f.Nome is "File Explorer" or "Esplora file"))
            {
                f.Exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                f.Nome = "Esplora file";
            }
            // l'icona del collegamento: le web app (Chat, Olympus) hanno la loro, non quella di chrome_proxy
            f.Icona = Shell.Icona(percorso);
            f.Icona ??= f.Exe != null && File.Exists(f.Exe) ? Shell.Icona(f.Exe) : null;
            f.Icona ??= f.Aumid != null ? Shell.IconaApp(f.Aumid) : null;
            return f;
        }
        f.Exe = percorso;
        f.Nome = Shell.NomeExe(percorso);
        f.Icona = Shell.Icona(percorso);
        return f;
    }

    public void Avvia()
    {
        // Esplora file fissato: si apre il nostro
        if (Nome == "Esplora file") { Esplora.Apri(); return; }
        try
        {
            if (Aumid != null && Percorso.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                Process.Start("explorer.exe", Percorso);
            else
                Process.Start(new ProcessStartInfo(Percorso) { UseShellExecute = true });
        }
        catch (Exception e) { App.Log("avvio " + Percorso + ": " + e.Message); }
    }
}

// App fissate: un percorso per riga in fissate.txt, la prima volta copiate da quelle di Windows.
class Fissate
{
    public static string File => Path.Combine(App.Cartella, "fissate.txt");
    public List<Fissata> Elenco = new();

    public static Fissate Carica()
    {
        var f = new Fissate();
        List<string> righe;
        if (System.IO.File.Exists(File)) righe = System.IO.File.ReadAllLines(File).Where(r => r.Trim().Length > 0).ToList();
        else
        {
            var cartella = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
            righe = Directory.Exists(cartella) ? Directory.GetFiles(cartella, "*.lnk").OrderBy(x => x).ToList() : new List<string>();
            System.IO.File.WriteAllLines(File, righe);
        }
        foreach (var r in righe)
        {
            try { f.Elenco.Add(Fissata.Da(r.Trim())); } catch (Exception e) { App.Log("fissata " + r + ": " + e.Message); }
        }
        return f;
    }

    public void Salva() => System.IO.File.WriteAllLines(File, Elenco.Select(x => x.Percorso));

    public void Fissa(Gruppo g)
    {
        string p = g.Aumid != null && (g.Exe == null || g.Exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase) || g.Exe.Contains(@"\WindowsApps\"))
            ? @"shell:AppsFolder\" + g.Aumid
            : g.Exe;
        if (p == null) return;
        var f = Fissata.Da(p);
        if (g.Aumid != null) f.Aumid ??= g.Aumid;
        Elenco.Add(f);
        Salva();
    }

    public void Togli(Fissata f)
    {
        Elenco.Remove(f);
        Salva();
    }
}

// Elenco delle finestre aperte, raggruppate per app come la barra di Windows.
class Tracciatore
{
    static readonly HashSet<string> classiEscluse = new()
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW", "Windows.UI.Core.CoreWindow",
        "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland", "Windows.UI.Composition.DesktopWindowContentBridge",
    };
    static readonly HashSet<string> exeEsclusi = new(StringComparer.OrdinalIgnoreCase) { "yasb.exe", "BarraVascend.exe", "TextInputHost.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe", "SearchHost.exe" };

    readonly Dictionary<uint, string> exePid = new();
    readonly Dictionary<IntPtr, string> aumidFinestra = new();
    readonly Dictionary<string, (string nome, ImageSource icona)> info = new();
    public readonly HashSet<IntPtr> Attenzione = new();
    public List<Gruppo> Gruppi = new();
    public event Action Cambiati;
    string firma = "";

    public void Aggiorna()
    {
        var fg = N.GetForegroundWindow();
        if (Attenzione.Contains(fg)) Attenzione.Remove(fg);
        var aperte = new List<(Finestra f, string exe, string aumid)>();
        uint mio = (uint)Environment.ProcessId;
        N.EnumWindows((h, _) =>
        {
            try
            {
                if (!Valida(h, mio, out var exe, out var pid)) return true;
                if (!aumidFinestra.TryGetValue(h, out var aumid))
                {
                    aumid = Shell.AumidFinestra(h);
                    aumidFinestra[h] = aumid;
                }
                aperte.Add((new Finestra { H = h, Titolo = N.Titolo(h), Pid = pid }, exe, aumid));
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        var gruppi = new List<Gruppo>();
        foreach (var f in App.Io.Fissate.Elenco)
            gruppi.Add(new Gruppo { Chiave = "fissata:" + f.Percorso.ToLowerInvariant(), Exe = f.Exe, Aumid = f.Aumid, Nome = f.Nome, Icona = f.Icona, Fissata = f });

        foreach (var (f, exe, aumid) in aperte)
        {
            var g = gruppi.FirstOrDefault(x => aumid != null && x.Aumid != null && string.Equals(x.Aumid, aumid, StringComparison.OrdinalIgnoreCase))
                 ?? gruppi.FirstOrDefault(x => aumid != null && x.Aumid != null && x.Fissata != null && aumid.StartsWith(x.Aumid + ".UserData.Profile1", StringComparison.OrdinalIgnoreCase))
                 ?? gruppi.FirstOrDefault(x => exe != null && x.Exe != null && (x.Aumid == null || aumid == null) && string.Equals(x.Exe, exe, StringComparison.OrdinalIgnoreCase));
            if (g == null)
            {
                string chiave = aumid != null ? "aumid:" + aumid.ToLowerInvariant() : "exe:" + (exe ?? f.H.ToString()).ToLowerInvariant();
                var (nome, icona) = Info(chiave, exe, aumid, f.H);
                g = new Gruppo { Chiave = chiave, Exe = exe, Aumid = aumid, Nome = nome, Icona = icona };
                gruppi.Add(g);
            }
            g.Finestre.Add(f);
            if (f.H == fg) g.Attiva = true;
            if (Attenzione.Contains(f.H)) g.Attenzione = true;
        }

        // ordine stabile: le app non fissate restano dove sono apparse la prima volta
        var vecchio = Gruppi.Select(x => x.Chiave).ToList();
        var fissate = gruppi.Where(x => x.Fissata != null).ToList();
        var altre = gruppi.Where(x => x.Fissata == null).OrderBy(x => { int i = vecchio.IndexOf(x.Chiave); return i < 0 ? int.MaxValue : i; }).ToList();
        Gruppi = fissate.Concat(altre).ToList();

        var vive = new HashSet<IntPtr>(aperte.Select(x => x.f.H));
        foreach (var h in aumidFinestra.Keys.Where(k => !vive.Contains(k)).ToList()) aumidFinestra.Remove(h);
        Attenzione.IntersectWith(vive);

        string nuova = string.Join("|", Gruppi.Select(g => g.Firma + (g.Attiva ? "*" : "") + (g.Attenzione ? "!" : "") + string.Join(",", g.Finestre.Select(f => f.Titolo))));
        if (nuova != firma)
        {
            if (Environment.GetEnvironmentVariable("BARRA_DEBUG") == "1")
                App.Log("gruppi: " + string.Join(" | ", Gruppi.Select(g => $"{g.Chiave} exe={g.Exe} aumid={g.Aumid} n={g.Finestre.Count}")));
            firma = nuova;
            Cambiati?.Invoke();
        }
    }

    (string, ImageSource) Info(string chiave, string exe, string aumid, IntPtr h)
    {
        if (info.TryGetValue(chiave, out var i)) return i;
        string nome = null;
        ImageSource icona = null;
        bool store = exe == null || exe.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) || exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);
        if (aumid != null && store)
        {
            icona = Shell.IconaApp(aumid);
            nome = Shell.NomeVisualizzato(@"shell:AppsFolder\" + aumid);
        }
        if (exe != null)
        {
            icona ??= Shell.Icona(exe);
            nome ??= Shell.NomeExe(exe);
        }
        icona ??= Shell.IconaFinestra(h);
        nome ??= N.Titolo(h);
        info[chiave] = (nome, icona);
        return (nome, icona);
    }

    bool Valida(IntPtr h, uint mio, out string exe, out uint pid)
    {
        exe = null;
        pid = 0;
        if (!N.IsWindowVisible(h)) return false;
        long ex = (long)N.GetWindowLongPtr(h, N.GWL_EXSTYLE);
        bool appWin = (ex & N.WS_EX_APPWINDOW) != 0;
        if (!appWin && (ex & N.WS_EX_TOOLWINDOW) != 0) return false;
        if (!appWin && (ex & N.WS_EX_NOACTIVATE) != 0) return false;
        if (!appWin && N.GetWindow(h, 4) != IntPtr.Zero) return false;
        if (N.GetWindowTextLength(h) == 0) return false;
        if (N.Nascosta(h)) return false;
        if (classiEscluse.Contains(N.Classe(h))) return false;
        pid = N.Pid(h);
        if (pid == mio)
        {
            if (N.GetProp(h, "BarraVascendEsplora") == IntPtr.Zero) return false;
            exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            return true;
        }
        if (!exePid.TryGetValue(pid, out exe))
        {
            exe = N.ExeDiPid(pid);
            exePid[pid] = exe;
        }
        if (exe != null && exe.EndsWith("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            // le app dello Store: l'exe vero è quello della finestra figlia
            uint p0 = pid;
            string vero = null;
            N.EnumChildWindows(h, (c, _) =>
            {
                uint pc = N.Pid(c);
                if (pc != p0) { vero = N.ExeDiPid(pc); return false; }
                return true;
            }, IntPtr.Zero);
            if (vero != null) exe = vero;
        }
        if (exe != null && exeEsclusi.Contains(Path.GetFileName(exe))) return false;
        return true;
    }
}
