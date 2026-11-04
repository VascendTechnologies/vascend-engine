using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace BarraVascend;

// Barra delle applicazioni personale: una per schermo, a comparsa, al posto di quella di YASB.
public class App : Application
{
    public static string Cartella => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".config\barra-vascend");
    internal static App Io => (App)Current;

    internal Tracciatore Finestre;
    internal Tray Tray;
    internal Fissate Fissate;
    internal Pannello Pannello;
    readonly List<Barra> barre = new();
    readonly List<Isola> isole = new();
    readonly List<Scrivania> scrivanie = new();
    FileSystemWatcher guardia;
    FileSystemWatcher guardiaEstensioni;
    DispatcherTimer ricarica;
    DispatcherTimer tic, ricrea;
    DateTime ultimo = DateTime.Now, ultimoGiro;
    HwndSource shell;
    uint msgShell;
    static Mutex unico;
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);

    public void ApriCerca()
    {
        var p = N.Cursore();
        var s = schermi.Find(x => x.Area.Contiene(p)) ?? (schermi.Count > 0 ? schermi[0] : null);
        if (s != null) Cerca.Apri(s);
    }
    List<Schermo> schermi = new();

    // Il tasto della finestra sulla barra dello schermo dove si trova (o su una qualsiasi, se lì non c'è).
    N.RECT? RettangoloTasto(IntPtr h)
    {
        var mon = N.MonitorFromWindow(h, 2);
        foreach (var b in barre.OrderBy(b => b.Schermo.H == mon ? 0 : 1))
            if (b.RettangoloApp(h) is N.RECT r) return r;
        return null;
    }

    public void ApriNotifiche()
    {
        var p = N.Cursore();
        var b = barre.Find(x => x.Schermo.Area.Contiene(p)) ?? (barre.Count > 0 ? barre[0] : null);
        b?.ApriNotifiche();
    }

    public void ApriStart()
    {
        var p = N.Cursore();
        var b = barre.Find(x => x.Schermo.Area.Contiene(p)) ?? (barre.Count > 0 ? barre[0] : null);
        b?.ApriStart();
    }

    [STAThread]
    public static void Main()
    {
        if(!ExtensionState.Enabled(ExtensionState.BuiltinId)) return;
        unico = new Mutex(true, @"Local\BarraVascend", out bool nuovo);
        if (!nuovo) return;
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += (_, _) => app.Avvia();
        app.Run();
    }

    public static void Log(string s)
    {
        try { File.AppendAllText(Path.Combine(Cartella, "barra.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {s}\n"); } catch { }
    }

    void Avvia()
    {
        DispatcherUnhandledException += (_, e) => { Log(e.Exception.ToString()); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log(e.ExceptionObject.ToString());
        Directory.CreateDirectory(Cartella);
        GuardaStatoEstensione();
        Fissate = Fissate.Carica();
        Finestre = new Tracciatore();
        Tray = new Tray();
        Tray.Avvia();
        Finestre.Aggiorna();
        Rete.Aggiorna();
        System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += (_, _) => Rete.Aggiorna();
        Musica.Avvia();
        Claude.Avvia();
        Ide.Avvia();
        Indice.Carica();
        AscoltaShell();
        CreaBarre();

        tic = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
        tic.Tick += (_, _) => Giro();
        tic.Start();
        new Thread(() =>
        {
            string detto = null;
            while (true)
            {
                Thread.Sleep(1000);
                var fermo = (DateTime.Now.Ticks - System.Threading.Interlocked.Read(ref battito)) / TimeSpan.TicksPerSecond;
                if (fermo >= 3 && Fase != detto) { detto = Fase; Log($"fermo da {fermo} s in: {Fase}"); }
                if (fermo < 3) detto = null;
            }
        }) { IsBackground = true }.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => Dispatcher.BeginInvoke(RicreaPresto);
        Exit += (_, _) => { foreach (var b in barre) b.Close(); };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { };
    }

    void GuardaStatoEstensione()
    {
        Directory.CreateDirectory(Stile.Cartella);
        guardiaEstensioni = new FileSystemWatcher(Stile.Cartella,"extension-state.json") { NotifyFilter=NotifyFilters.LastWrite|NotifyFilters.FileName|NotifyFilters.Size };
        void Controlla() => Dispatcher.BeginInvoke(new Action(() => {
            try {if(!ExtensionState.Enabled(ExtensionState.BuiltinId)){TogliDecorazioni();Shutdown();}}
            catch(Exception ex){Log("stato estensione: "+ex.Message);}
        }));
        guardiaEstensioni.Changed += (_,_)=>Controlla();
        guardiaEstensioni.Created += (_,_)=>Controlla();
        guardiaEstensioni.Renamed += (_,_)=>Controlla();
        guardiaEstensioni.EnableRaisingEvents=true;
        Exit += (_,_)=>guardiaEstensioni.Dispose();
    }

    void RicreaPresto()
    {
        ricrea?.Stop();
        ricrea = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        ricrea.Tick += (_, _) => { ricrea.Stop(); CreaBarre(); };
        ricrea.Start();
    }

    // Finestra nascosta che riceve dalla shell gli avvisi su finestre create, chiuse, attivate o che lampeggiano.
    void AscoltaShell()
    {
        var p = new HwndSourceParameters("BarraVascendShell") { Width = 0, Height = 0, WindowStyle = 0 };
        shell = new HwndSource(p);
        msgShell = N.RegisterWindowMessage("SHELLHOOK");
        N.RegisterShellHookWindow(shell.Handle);
        // Alt+Spazio apre Cerca sullo schermo del mouse
        Start.AscoltaWin(ApriStart, ApriNotifiche);
        Notifiche.Avvia();
        if (!RegisterHotKey(shell.Handle, 1, 0x1 | 0x4000, 0x20)) Log("Alt+Spazio non registrato (già usato da un'altra app)");
        shell.AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool gestito) =>
        {
            if (m == 0x312 && (int)w == 1) { ApriCerca(); return IntPtr.Zero; }
            if (m == msgShell)
            {
                int cod = (int)w & 0x7FFF;
                // la finestra si riduce o si ingrandisce: l'animazione va verso il suo tasto sulla barra a sinistra
                if (cod == 5 && l != IntPtr.Zero)
                {
                    var f = System.Runtime.InteropServices.Marshal.ReadIntPtr(l);
                    var r = RettangoloTasto(f);
                    if (r is N.RECT rr) { System.Runtime.InteropServices.Marshal.StructureToPtr(rr, l + IntPtr.Size, false); gestito = true; return (IntPtr)1; }
                }
                if ((int)w == 0x8006) Finestre.Attenzione.Add(l);
                if (cod is 1 or 2 or 4 or 6) Dispatcher.BeginInvoke(() => Finestre.Aggiorna(), DispatcherPriority.Background);
            }
            return IntPtr.Zero;
        });
    }

    void CreaBarre()
    {
        Pannello?.Chiudi();
        foreach (var b in barre) { b.Scollega(); b.Close(); }
        barre.Clear();
        TogliDecorazioni();
        schermi = new List<Schermo>();
        N.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hm, IntPtr dc, ref N.RECT r, IntPtr d) =>
        {
            var mi = new N.MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<N.MONITORINFOEX>() };
            N.GetMonitorInfo(hm, ref mi);
            schermi.Add(new Schermo { H = hm, Area = mi.rcMonitor, Nome = mi.szDevice, Principale = (mi.dwFlags & 1) != 0 });
            return true;
        }, IntPtr.Zero);
        Barra.TogliVecchie();
        foreach (var s in schermi)
        {
            var b = new Barra(s);
            barre.Add(b);
            b.Avvia();
        }
        CreaDecorazioni();
        GuardaStile();
        Log("barre: " + string.Join(", ", schermi.ConvertAll(s => $"{s.Nome} {s.Area.L},{s.Area.T} {s.Area.W}x{s.Area.H}")));
    }

    void TogliDecorazioni()
    {
        foreach (var i in isole) { i.Scollega(); i.Close(); }
        foreach (var o in scrivanie) o.Close();
        isole.Clear();
        scrivanie.Clear();
    }

    // Isola e widget di ogni schermo, come li vuole lo stile.
    void CreaDecorazioni()
    {
        var st = Stile.Attuale;
        foreach (var s in schermi)
        {
            if (st.IsolaAttiva && st.IsolaModuli?.Count > 0 && (st.IsolaSchermi != "principale" || s.Principale))
            {
                var i = new Isola(s);
                isole.Add(i);
                i.Avvia();
            }
            var qui = (st.Scrivania ?? new()).Where(w => w.Schermi != "principale" || s.Principale);
            foreach (var gruppo in qui.GroupBy(w => w.Posizione ?? "centro"))
            {
                var o = new Scrivania(s, gruppo.Key, gruppo);
                if (o.Vuota) { o.Close(); continue; }
                scrivanie.Add(o);
                o.Avvia();
            }
        }
    }

    // Quando l'Engine salva lo stile, isola e widget si rifanno subito, senza riavviare la barra.
    void GuardaStile()
    {
        if (guardia != null) return;
        try
        {
            System.IO.Directory.CreateDirectory(Stile.Cartella);
            guardia = new FileSystemWatcher(Stile.Cartella, "stile.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            ricarica = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            ricarica.Tick += (_, _) =>
            {
                ricarica.Stop();
                try
                {
                    Stile.RicaricaDecorazioni();
                    Pannello?.Chiudi();
                    TogliDecorazioni();
                    CreaDecorazioni();
                }
                catch (Exception e) { Log("decorazioni: " + e.Message); }
            };
            FileSystemEventHandler h = (_, _) => Dispatcher.BeginInvoke(() => { ricarica.Stop(); ricarica.Start(); });
            guardia.Changed += h;
            guardia.Created += h;
            guardia.Renamed += (_, _) => Dispatcher.BeginInvoke(() => { ricarica.Stop(); ricarica.Start(); });
            guardia.EnableRaisingEvents = true;
        }
        catch (Exception e) { Log("stile: " + e.Message); }
    }

    // Il giro va veloce (15 ms) solo quando serve: mouse sulla barra, pannello aperto, barra che scivola.
    // Il resto del tempo basta un controllo ogni 250 ms; i passaggi del mouse sui tasti li gestisce WPF da sé.
    DateTime ultimeFinestre, ultimaRete, ultimeNotifiche;
    public static volatile string Fase = "";
    static long battito;
    void Giro()
    {
        System.Threading.Interlocked.Exchange(ref battito, DateTime.Now.Ticks);
        Fase = "giro";
        var adesso = DateTime.Now;
        double dt = Math.Min(50, (adesso - ultimo).TotalMilliseconds);
        ultimo = adesso;
        var p = N.Cursore();
        bool attivo = Pannello?.Aperto == true;
        foreach (var b in barre) { Fase = "tick " + b.Schermo.Nome; b.Tick(p, dt); attivo |= b.Occupata(p); }
        var voluto = TimeSpan.FromMilliseconds(attivo ? 15 : 250);
        if (tic.Interval != voluto) tic.Interval = voluto;
        if ((adesso - ultimoGiro).TotalMilliseconds > 800)
        {
            ultimoGiro = adesso;
            // le finestre arrivano già dagli avvisi della shell: questo è solo un controllo di sicurezza
            if ((adesso - ultimeFinestre).TotalSeconds >= 3) { ultimeFinestre = adesso; Fase = "finestre"; Finestre.Aggiorna(); }
            if ((adesso - ultimaRete).TotalSeconds >= 15) { ultimaRete = adesso; Fase = "rete"; Rete.Aggiorna(); }
            if ((adesso - ultimeNotifiche).TotalSeconds >= 3) { ultimeNotifiche = adesso; Fase = "notifiche"; Notifiche.Aggiorna(); }
            Fase = "claude"; Claude.Aggiorna();
        }
        Fase = "fuori dal giro";
    }

    // Da chiamare quando si apre qualcosa che vuole il giro veloce subito, senza aspettare il prossimo controllo.
    public void Sveglia() { if (tic != null && tic.Interval.TotalMilliseconds > 15) { tic.Interval = TimeSpan.FromMilliseconds(15); } }

    public static void Riavvia()
    {
        var exe = Environment.ProcessPath;
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c timeout /t 1 /nobreak >nul & start \"\" \"{exe}\"") { CreateNoWindow = true, UseShellExecute = false });
        Current.Shutdown();
    }
}
