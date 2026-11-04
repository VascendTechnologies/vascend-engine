using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BarraVascend;

// Volume con le API Core Audio di Windows.
static class Audio
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }
    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")] class PolicyConfigClient { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint n);
        [PreserveSig] int Item(uint i, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY { public Guid fmtid; public uint pid; }
    [StructLayout(LayoutKind.Sequential)]
    struct PROPVARIANT { public ushort vt; ushort r1, r2, r3; public IntPtr p; public IntPtr p2; }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out uint c);
        void GetAt(uint i, out PROPERTYKEY k);
        [PreserveSig] int GetValue(ref PROPERTYKEY k, out PROPVARIANT v);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr p);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr p);
        [PreserveSig] int GetChannelCount(out uint n);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid ctx);
        [PreserveSig] int SetMasterVolumeLevelScalar(float v, ref Guid ctx);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float v);
        [PreserveSig] int SetChannelVolumeLevel(uint c, float db, ref Guid ctx);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint c, float v, ref Guid ctx);
        [PreserveSig] int GetChannelVolumeLevel(uint c, out float db);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint c, out float v);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }

    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string id, IntPtr f);
        [PreserveSig] int GetDeviceFormat(string id, int def, IntPtr f);
        [PreserveSig] int ResetDeviceFormat(string id);
        [PreserveSig] int SetDeviceFormat(string id, IntPtr a, IntPtr b);
        [PreserveSig] int GetProcessingPeriod(string id, int def, IntPtr a, IntPtr b);
        [PreserveSig] int SetProcessingPeriod(string id, IntPtr a);
        [PreserveSig] int GetShareMode(string id, IntPtr m);
        [PreserveSig] int SetShareMode(string id, IntPtr m);
        [PreserveSig] int GetPropertyValue(string id, IntPtr k, IntPtr v);
        [PreserveSig] int SetPropertyValue(string id, IntPtr k, IntPtr v);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr g, int f, out IntPtr c);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr g, int f, out IntPtr v);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator e);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int n);
        [PreserveSig] int GetSession(int i, out IAudioSessionControl2 s);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int s);
        [PreserveSig] int GetDisplayName(out IntPtr n);
        [PreserveSig] int SetDisplayName(IntPtr n, ref Guid g);
        [PreserveSig] int GetIconPath(out IntPtr p);
        [PreserveSig] int SetIconPath(IntPtr p, ref Guid g);
        [PreserveSig] int GetGroupingParam(out Guid g);
        [PreserveSig] int SetGroupingParam(ref Guid g, ref Guid c);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint pid);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float v, ref Guid g);
        [PreserveSig] int GetMasterVolume(out float v);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool m, ref Guid g);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool m);
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float p);
    }

    public class Fonte { public string Exe, Nome; public float Picco; }

    // Le app che in questo momento mandano suono (dal misuratore di ogni sessione del mixer).
    public static List<Fonte> Suonano()
    {
        var r = new Dictionary<string, Fonte>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // gira in background: un enumeratore suo, non quello del thread della barra
            var enu = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enu.GetDefaultAudioEndpoint(0, 1, out var d) != 0 || d == null) return new();
            var iid = typeof(IAudioSessionManager2).GUID;
            d.Activate(ref iid, 1, IntPtr.Zero, out var o);
            ((IAudioSessionManager2)o).GetSessionEnumerator(out var e);
            e.GetCount(out int n);
            uint mio = (uint)Environment.ProcessId;
            for (int i = 0; i < n; i++)
            {
                e.GetSession(i, out var s);
                if (s == null) continue;
                s.GetProcessId(out uint pid);
                if (pid == 0 || pid == mio) continue;
                if (s.GetState(out int stato) != 0 || stato != 1) continue;
                if (s is not IAudioMeterInformation m || m.GetPeakValue(out float picco) != 0) continue;
                var percorso = Percorso(pid);
                if (percorso.Length == 0) continue;
                var nome = System.IO.Path.GetFileName(percorso);
                if (!r.TryGetValue(nome, out var f)) r[nome] = f = new Fonte { Exe = percorso, Nome = nome };
                f.Picco = Math.Max(f.Picco, picco);
            }
        }
        catch { }
        return r.Values.Where(f => f.Picco > .002f).OrderByDescending(f => f.Picco).ToList();
    }

    public static bool MutoApp(string app)
    {
        foreach (var s in SessioniDi(app, out _)) { s.GetMute(out bool m); if (!m) return false; }
        return true;
    }

    public static void MutaApp(string app, bool m)
    {
        foreach (var s in SessioniDi(app, out _)) s.SetMute(m, ref vuoto);
    }

    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT v);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(int a, bool i, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int f, System.Text.StringBuilder s, ref int n);

    static readonly Dictionary<uint, string> percorsi = new();
    static string Percorso(uint pid)
    {
        if (percorsi.TryGetValue(pid, out var p)) return p;
        p = "";
        var h = OpenProcess(0x1000, false, pid);
        if (h != IntPtr.Zero)
        {
            var sb = new System.Text.StringBuilder(1024); int n = sb.Capacity;
            if (QueryFullProcessImageName(h, 0, sb, ref n)) p = sb.ToString();
            CloseHandle(h);
        }
        if (percorsi.Count > 200) percorsi.Clear();
        return percorsi[pid] = p;
    }

    // Le sessioni audio dell'app che manda la musica (id del lettore multimediale: "chrome.exe", "Spotify.exe" o un AUMID).
    static List<ISimpleAudioVolume> SessioniDi(string app, out string exe)
    {
        exe = null;
        var r = new List<ISimpleAudioVolume>();
        if (string.IsNullOrEmpty(app)) return r;
        try
        {
            en ??= (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (en.GetDefaultAudioEndpoint(0, 1, out var d) != 0 || d == null) return r;
            var iid = typeof(IAudioSessionManager2).GUID;
            d.Activate(ref iid, 1, IntPtr.Zero, out var o);
            ((IAudioSessionManager2)o).GetSessionEnumerator(out var e);
            e.GetCount(out int n);
            var a = app.ToLowerInvariant();
            for (int i = 0; i < n; i++)
            {
                e.GetSession(i, out var s);
                if (s == null) continue;
                s.GetProcessId(out uint pid);
                if (pid == 0) continue;
                s.GetSessionInstanceIdentifier(out var ist);
                var percorso = Percorso(pid);
                var nome = System.IO.Path.GetFileNameWithoutExtension(percorso).ToLowerInvariant();
                bool ok = (nome.Length > 0 && (a == nome + ".exe" || a == nome || a.EndsWith("!" + nome) || a.StartsWith(nome + ".")))
                    || (ist != null && ist.ToLowerInvariant().Contains(a.Split('!')[0]));
                if (!ok) continue;
                r.Add((ISimpleAudioVolume)s);
                exe ??= percorso;
            }
        }
        catch { }
        return r;
    }

    // Volume della sola app (come nel mixer di Windows); null se l'app non ha una sessione audio.
    public static float? LivelloApp(string app, out string exe)
    {
        var s = SessioniDi(app, out exe);
        if (s.Count == 0) return null;
        float m = 0;
        foreach (var v in s) { v.GetMasterVolume(out var x); m = Math.Max(m, x); }
        return m;
    }

    public static void ImpostaApp(string app, float v)
    {
        foreach (var s in SessioniDi(app, out _)) { s.SetMasterVolume(Math.Clamp(v, 0f, 1f), ref vuoto); s.SetMute(false, ref vuoto); }
    }

    static IMMDeviceEnumerator en;
    static IAudioEndpointVolume vol;
    static string idCorrente;
    static Guid vuoto = Guid.Empty;
    static PROPERTYKEY nomeKey = new() { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };

    public class Uscita { public string Id, Nome; public bool Predefinita; }

    static IAudioEndpointVolume Vol()
    {
        try
        {
            en ??= (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (en.GetDefaultAudioEndpoint(0, 1, out var d) != 0 || d == null) { vol = null; idCorrente = null; return null; }
            d.GetId(out var id);
            if (id != idCorrente || vol == null)
            {
                var iid = typeof(IAudioEndpointVolume).GUID;
                d.Activate(ref iid, 1, IntPtr.Zero, out var o);
                vol = (IAudioEndpointVolume)o;
                idCorrente = id;
            }
            return vol;
        }
        catch { vol = null; return null; }
    }

    public static bool Presente => Vol() != null;

    public static float Livello
    {
        get { var v = Vol(); if (v == null) return 0; v.GetMasterVolumeLevelScalar(out var x); return x; }
        set { var v = Vol(); if (v == null) return; v.SetMasterVolumeLevelScalar(Math.Clamp(value, 0f, 1f), ref vuoto); if (Muto && value > 0) Muto = false; }
    }

    public static bool Muto
    {
        get { var v = Vol(); if (v == null) return false; v.GetMute(out var m); return m; }
        set { Vol()?.SetMute(value, ref vuoto); }
    }

    public static List<Uscita> Uscite()
    {
        var r = new List<Uscita>();
        try
        {
            Vol();
            if (en.EnumAudioEndpoints(0, 1, out var c) != 0) return r;
            c.GetCount(out var n);
            for (uint i = 0; i < n; i++)
            {
                c.Item(i, out var d);
                d.GetId(out var id);
                string nome = id;
                if (d.OpenPropertyStore(0, out var s) == 0 && s.GetValue(ref nomeKey, out var v) == 0)
                {
                    if (v.vt == 31) nome = Marshal.PtrToStringUni(v.p);
                    PropVariantClear(ref v);
                }
                r.Add(new Uscita { Id = id, Nome = nome, Predefinita = id == idCorrente });
            }
        }
        catch (Exception e) { App.Log("uscite audio: " + e.Message); }
        return r;
    }

    public static void Predefinita(string id)
    {
        try
        {
            var p = (IPolicyConfig)new PolicyConfigClient();
            for (int ruolo = 0; ruolo < 3; ruolo++) p.SetDefaultEndpoint(id, ruolo);
            vol = null;
        }
        catch (Exception e) { App.Log("uscita audio: " + e.Message); }
    }

    public static string Glifo()
    {
        if (!Presente) return "";
        if (Muto) return "";
        float l = Livello;
        return l <= 0.001 ? "" : l < .34 ? "" : l < .67 ? "" : "";
    }
}

// Stato rete e segnale Wi-Fi dalla API nativa, senza dipendere dai permessi di posizione.
static class Rete
{
    public static string Tipo = "nessuna";
    public static string Nome = "";
    public static int Segnale;
    public static bool SegnaleDisponibile;
    public static string Ip = "";
    public static event Action Cambiata;
    static int inCorso;

    public class Wifi { public string Ssid; public int Segnale; public bool Sicura; }

    public static void Aggiorna()
    {
        if (System.Threading.Interlocked.CompareExchange(ref inCorso,1,0) != 0) return;
        Task.Run(() =>
        {
            try
            {
                string tipo = "nessuna", nome = "", ip = "";
                int segnale = 0;
                bool disponibile=false;
                string wifiId=null;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                    var p = ni.GetIPProperties();
                    if (!p.GatewayAddresses.Any(g => g.Address != null && !g.Address.Equals(System.Net.IPAddress.Any) && g.Address.ToString() != "::")) continue;
                    var d = (ni.Description + " " + ni.Name).ToLowerInvariant();
                    if (d.Contains("virtual") || d.Contains("hyper-v") || d.Contains("vethernet") || d.Contains("wsl")) continue;
                    var ip4 = p.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                    {
                        if (tipo == "ethernet") continue;
                        tipo = "wifi";
                        nome = ni.Name;
                        wifiId=ni.Id;
                        ip = ip4?.Address.ToString() ?? "";
                    }
                    else if (tipo != "ethernet")
                    {
                        tipo = "ethernet";
                        nome = ni.Name;
                        ip = ip4?.Address.ToString() ?? "";
                    }
                }
                if (tipo == "wifi")
                {
                    var reading=WifiSignal.Read(wifiId);
                    disponibile=reading.Available;
                    if(disponibile)segnale=reading.Quality;
                }
                bool cambio = tipo != Tipo || nome != Nome || segnale != Segnale || disponibile != SegnaleDisponibile || ip != Ip;
                Tipo = tipo; Nome = nome; Segnale = segnale; SegnaleDisponibile=disponibile; Ip = ip;
                if (cambio) App.Io.Dispatcher.BeginInvoke(() => Cambiata?.Invoke());
            }
            catch (Exception e) { App.Log("rete: " + e.Message); }
            finally { System.Threading.Interlocked.Exchange(ref inCorso,0); }
        });
    }

    public static string Glifo() => Tipo switch
    {
        "ethernet" => "",
        "wifi" when !SegnaleDisponibile => "\uE701",
        "wifi" => Segnale >= 75 ? "" : Segnale >= 50 ? "" : Segnale >= 25 ? "" : "",
        _ => "",
    };

    public static Task<List<Wifi>> Reti() => Task.Run(() =>
    {
        var r = new List<Wifi>();
        var o = Netsh("wlan show networks mode=bssid");
        Wifi c = null;
        foreach (var riga in o.Split('\n'))
        {
            var m = Regex.Match(riga, @"^SSID\s+\d+\s*:\s*(.*)$");
            if (m.Success)
            {
                c = new Wifi { Ssid = m.Groups[1].Value.Trim() };
                if (c.Ssid.Length > 0) r.Add(c);
                continue;
            }
            if (c == null) continue;
            var s = Regex.Match(riga, @"^\s+(Segnale|Signal)\s*:\s*(\d+)%");
            if (s.Success) c.Segnale = Math.Max(c.Segnale, int.Parse(s.Groups[2].Value));
            if (Regex.IsMatch(riga, @"^\s+(Autenticazione|Authentication)\s*:\s*(?!Aperta|Open)")) c.Sicura = true;
        }
        return r.GroupBy(x => x.Ssid).Select(g => g.OrderByDescending(x => x.Segnale).First()).OrderByDescending(x => x.Segnale).ToList();
    });

    public static void Connetti(string ssid) => Task.Run(() => { Netsh($"wlan connect name=\"{ssid.Replace("\"", "")}\""); Aggiorna(); });

    static string Netsh(string arg)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("netsh.exe", arg)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            });
            var read=p.StandardOutput.ReadToEndAsync();
            if(!p.WaitForExit(4000)){try{p.Kill();}catch{}return "";}
            return p.ExitCode == 0 ? read.GetAwaiter().GetResult().Replace("\r", "") : "";
        }
        catch { return ""; }
    }
}

static class Batteria
{
    public static bool Presente
    {
        get { N.GetSystemPowerStatus(out var s); return (s.BatteryFlag & 128) == 0 && s.BatteryFlag != 255; }
    }

    public static int Percento { get { N.GetSystemPowerStatus(out var s); return s.BatteryLifePercent > 100 ? 100 : s.BatteryLifePercent; } }
    public static bool InCarica { get { N.GetSystemPowerStatus(out var s); return s.ACLineStatus == 1; } }
    public static int SecondiRimasti { get { N.GetSystemPowerStatus(out var s); return s.BatteryLifeTime; } }

    public static string Glifo()
    {
        int l = (int)Math.Round(Percento / 10.0);
        return ((char)((InCarica ? 0xEBAB : 0xEBA0) + Math.Clamp(l, 0, 10))).ToString();
    }
}

// Uso della CPU (dalla differenza dei tempi di sistema fra due letture) e della memoria.
static class Prestazioni
{
    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong a, b, c, d, e, f, g; }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);

    static long i0, k0, u0;
    static DateTime ultima;
    static double cpu;

    public static double Cpu
    {
        get
        {
            if ((DateTime.Now - ultima).TotalMilliseconds < 900) return cpu;
            ultima = DateTime.Now;
            if (!GetSystemTimes(out var i, out var k, out var u)) return cpu;
            long libero = i - i0, totale = (k - k0) + (u - u0);
            if (i0 != 0 && totale > 0) cpu = Math.Clamp(100.0 * (totale - libero) / totale, 0, 100);
            i0 = i; k0 = k; u0 = u;
            return cpu;
        }
    }

    public static double Ram
    {
        get
        {
            var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            return GlobalMemoryStatusEx(ref m) ? m.dwMemoryLoad : 0;
        }
    }
}
