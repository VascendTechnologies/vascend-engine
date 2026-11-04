using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace BarraVascend;

class IconaTray
{
    public IntPtr HWnd;
    public uint Id;
    public Guid? Guid;
    public uint Callback;
    public uint Versione;
    public string Suggerimento = "";
    public ImageSource Immagine;
    public bool Nascosta;

    public bool Stessa(IntPtr h, uint id, Guid? g) => g.HasValue && Guid.HasValue ? g == Guid : HWnd == h && Id == id;

    // Clic inoltrato all'app come farebbe la barra di Windows.
    public void Clic(uint mouse)
    {
        if (!N.IsWindow(HWnd)) return;
        N.AllowSetForegroundWindow(N.Pid(HWnd));
        Notifica(mouse);
        if (Versione >= 3)
        {
            if (mouse == 0x202) Notifica(0x400);       // NIN_SELECT
            else if (mouse == 0x205) Notifica(0x7B);   // WM_CONTEXTMENU
        }
    }

    void Notifica(uint msg)
    {
        if (Callback == 0) return;
        IntPtr w, l;
        if (Versione > 3)
        {
            var c = N.Cursore();
            w = (IntPtr)((c.X & 0xFFFF) | (c.Y << 16));
            l = (IntPtr)(int)((msg & 0xFFFF) | (Id << 16));
        }
        else
        {
            w = (IntPtr)Id;
            l = (IntPtr)msg;
        }
        N.SendNotifyMessage(HWnd, Callback, w, l);
    }
}

// Area di notifica: una finestra Shell_TrayWnd sempre in cima riceve le icone dalle app e le passa a Explorer.
class Tray
{
    public readonly List<IconaTray> Icone = new();
    public event Action Cambiate;
    N.WndProc proc;
    IntPtr hwnd, vera;
    uint msgCreata;

    public void Avvia()
    {
        proc = Finestra;
        msgCreata = N.RegisterWindowMessage("TaskbarCreated");
        var wc = new N.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<N.WNDCLASSEX>(),
            lpfnWndProc = proc,
            hInstance = N.GetModuleHandle(null),
            lpszClassName = "Shell_TrayWnd",
        };
        N.RegisterClassEx(ref wc);
        hwnd = N.CreateWindowEx(N.WS_EX_TOOLWINDOW | N.WS_EX_TOPMOST, "Shell_TrayWnd", "Shell_TrayWnd", 0x80000000 | 0x02000000 | 0x04000000, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        if (hwnd == IntPtr.Zero) { App.Log("tray: finestra non creata"); return; }
        Davanti();
        N.SetTimer(hwnd, (IntPtr)1, 400, IntPtr.Zero);
        // le app già aperte rimandano le loro icone, che ora arrivano a noi
        N.SendNotifyMessage((IntPtr)0xFFFF, msgCreata, IntPtr.Zero, IntPtr.Zero);
    }

    void Davanti() => N.SetWindowPos(hwnd, N.HWND_TOPMOST, 0, 0, 0, 0, N.SWP_NOMOVE | N.SWP_NOSIZE | N.SWP_NOACTIVATE);

    IntPtr Vera()
    {
        if (vera != IntPtr.Zero && N.IsWindow(vera)) return vera;
        vera = IntPtr.Zero;
        IntPtr h = IntPtr.Zero;
        while ((h = N.FindWindowEx(IntPtr.Zero, h, "Shell_TrayWnd", null)) != IntPtr.Zero)
        {
            if (h == hwnd) continue;
            var exe = N.ExeDiPid(N.Pid(h));
            if (exe != null && exe.EndsWith(@"\explorer.exe", StringComparison.OrdinalIgnoreCase)) { vera = h; break; }
        }
        return vera;
    }

    IntPtr Inoltra(uint m, IntPtr w, IntPtr l)
    {
        var v = Vera();
        if (v == IntPtr.Zero) return m == N.WM_COPYDATA ? (IntPtr)1 : N.DefWindowProc(hwnd, m, w, l);
        if (m == N.WM_USER + 372) { N.PostMessage(v, m, w, l); return N.DefWindowProc(hwnd, m, w, l); }
        return N.SendMessage(v, m, w, l);
    }

    IntPtr Finestra(IntPtr h, uint m, IntPtr w, IntPtr l)
    {
        try
        {
            if (m == msgCreata && msgCreata != 0) { N.SetProp(h, "TaskbandHWND", h); return IntPtr.Zero; }
            if (m == N.WM_TIMER) { Davanti(); return IntPtr.Zero; }
            if (m == N.WM_COPYDATA) return CopiaDati(m, w, l);
            if (m == 0x1C || m == 0x111 || m >= N.WM_USER) return Inoltra(m, w, l);
        }
        catch (Exception e) { App.Log("tray: " + e); }
        return N.DefWindowProc(h, m, w, l);
    }

    IntPtr CopiaDati(uint m, IntPtr w, IntPtr l)
    {
        var cd = Marshal.PtrToStructure<N.COPYDATASTRUCT>(l);
        if (cd.cbData == 0) return IntPtr.Zero;
        if ((long)cd.dwData == 1 && cd.cbData >= 8 + 280)
        {
            Leggi(cd.lpData, cd.cbData);
            return Inoltra(m, w, l);
        }
        if ((long)cd.dwData == 3 && cd.lpData != IntPtr.Zero)
        {
            // un'app chiede dove sta la sua icona: rispondiamo col cursore, come YASB
            int quale = Marshal.ReadInt32(cd.lpData, 4);
            var c = N.Cursore();
            if (quale == 1) return (IntPtr)(int)((c.X & 0xFFFF) | ((c.Y + 1) << 16));
            if (quale == 2) return (IntPtr)(int)(((c.X + 1) & 0xFFFF) | ((c.Y - 1) << 16));
            return IntPtr.Zero;
        }
        var esito = Inoltra(m, w, l);
        if (Environment.GetEnvironmentVariable("BARRA_DEBUG") == "1") App.Log($"copydata {(long)cd.dwData} {cd.cbData} vera={Vera()} -> {esito} err={Marshal.GetLastWin32Error()}");
        return esito;
    }

    // SHELLTRAYDATA: magic, messaggio, poi NOTIFYICONDATA con handle a 32 bit.
    void Leggi(IntPtr p, int n)
    {
        uint msg = (uint)Marshal.ReadInt32(p, 4);
        int b = 8;
        IntPtr hw = (IntPtr)Marshal.ReadInt32(p, b + 4);
        uint id = (uint)Marshal.ReadInt32(p, b + 8);
        uint flags = (uint)Marshal.ReadInt32(p, b + 12);
        uint cb = (uint)Marshal.ReadInt32(p, b + 16);
        IntPtr hicon = (IntPtr)Marshal.ReadInt32(p, b + 20);
        Guid? guid = null;
        if ((flags & 0x20) != 0 && n >= b + 952)
        {
            var g = new byte[16];
            Marshal.Copy(p + b + 936, g, 0, 16);
            guid = new Guid(g);
        }
        var esistente = Icone.FirstOrDefault(x => x.Stessa(hw, id, guid));

        if (msg == 2)
        {
            if (esistente != null) { Icone.Remove(esistente); Cambiate?.Invoke(); }
            return;
        }
        if (msg != 0 && msg != 1 && msg != 4) return;
        var i = esistente;
        if (i == null)
        {
            i = new IconaTray { HWnd = hw, Id = id, Guid = guid };
            Icone.Add(i);
        }
        if (hw != IntPtr.Zero) i.HWnd = hw;
        if (msg == 4 && n >= b + 804) { uint v = (uint)Marshal.ReadInt32(p, b + 800); if (v <= 4) i.Versione = v; }
        if ((flags & 0x1) != 0) i.Callback = cb;
        if ((flags & 0x2) != 0) i.Immagine = Shell.DaHIcon(hicon) ?? i.Immagine;
        if ((flags & 0x4) != 0) i.Suggerimento = (Marshal.PtrToStringUni(p + b + 24, 128) ?? "").Split('\0')[0].Replace("\r", "");
        if ((flags & 0x8) != 0 && n >= b + 288)
        {
            uint stato = (uint)Marshal.ReadInt32(p, b + 280), maschera = (uint)Marshal.ReadInt32(p, b + 284);
            if ((maschera & 1) != 0) i.Nascosta = (stato & 1) != 0;
        }
        Cambiate?.Invoke();
    }

    // le icone di sistema di Explorer (volume, rete, batteria) la barra le ha già sue
    public IEnumerable<IconaTray> Visibili() => Icone.Where(x => !x.Nascosta && x.Immagine != null && N.IsWindow(x.HWnd) && !DiExplorer(x.HWnd));

    readonly Dictionary<uint, bool> explorer = new();
    bool DiExplorer(IntPtr h)
    {
        uint pid = N.Pid(h);
        if (!explorer.TryGetValue(pid, out var e))
        {
            var exe = N.ExeDiPid(pid);
            e = exe != null && exe.EndsWith(@"\explorer.exe", StringComparison.OrdinalIgnoreCase);
            explorer[pid] = e;
        }
        return e;
    }
}
