using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BarraVascend;

// Chiamate Win32 usate dalla barra: finestre, schermi, regioni, vetro DWM, tasti.
static class N
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int L, T, R, B;
        public int W => R - L;
        public int H => B - T;
        public bool Contiene(POINT p) => p.X >= L && p.X < R && p.Y >= T && p.Y < B;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    public delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr data);
    public delegate bool EnumWindowsProc(IntPtr h, IntPtr p);
    public delegate IntPtr WndProc(IntPtr h, uint m, IntPtr w, IntPtr l);

    [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr hMon, ref MONITORINFOEX mi);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hMon, int type, out uint x, out uint y);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT p, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetProp(IntPtr h, string name);
    [DllImport("user32.dll")] public static extern uint GetDpiForSystem();

    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr h, bool alt);
    [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(uint pid);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] public static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] public static extern IntPtr GetClassLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc f, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc f, IntPtr p);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool SendNotifyMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessage(string s);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h, IntPtr rgn, bool redraw);
    [DllImport("user32.dll")] public static extern bool RegisterShellHookWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern ushort RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(uint ex, string cls, string title, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
    [DllImport("user32.dll")] public static extern IntPtr SetTimer(IntPtr h, IntPtr id, uint ms, IntPtr proc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SetProp(IntPtr h, string name, IntPtr data);
    [DllImport("user32.dll")] public static extern IntPtr CopyIcon(IntPtr h);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    [DllImport("gdi32.dll")] public static extern IntPtr CreatePolygonRgn(POINT[] pts, int n, int mode);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("gdi32.dll")] public static extern int CombineRgn(IntPtr dst, IntPtr a, IntPtr b, int mode);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("gdi32.dll")] public static extern int OffsetRgn(IntPtr r, int x, int y);

    [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder s, ref int size);

    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] public static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] public static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES p);
    [DllImport("dwmapi.dll")] public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SIZE size);

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_THUMBNAIL_PROPERTIES
    {
        public int dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEX
    {
        public uint cbSize, style;
        public WndProc lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string lpszMenuName, lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT { public IntPtr dwData; public int cbData; public IntPtr lpData; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")] public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);

    [StructLayout(LayoutKind.Sequential)]
    struct ACCENT_POLICY { public int State, Flags, Gradient, Anim; }
    [StructLayout(LayoutKind.Sequential)]
    struct WINCOMPATTRDATA { public int Attr; public IntPtr Data; public int Size; }
    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h, ref WINCOMPATTRDATA d);

    public const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x80, WS_EX_APPWINDOW = 0x40000, WS_EX_NOACTIVATE = 0x08000000, WS_EX_TOPMOST = 0x8;
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_SHOWWINDOW = 0x40, SWP_HIDEWINDOW = 0x80;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const int SW_HIDE = 0, SW_SHOWNOACTIVATE = 4, SW_RESTORE = 9, SW_MINIMIZE = 6;
    // Barre di sistema (AppBar): riservano un bordo dello schermo.
    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint uCallbackMessage; public uint uEdge; public RECT rc; public IntPtr lParam; }
    [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint msg, ref APPBARDATA d);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")] public static extern bool SystemParametersInfo(uint azione, uint p, ref RECT r, uint ini);
    public const uint ABM_NEW = 0, ABM_REMOVE = 1, ABM_QUERYPOS = 2, ABM_SETPOS = 3, ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    public const uint WM_CLOSE = 0x10, WM_COPYDATA = 0x4A, WM_TIMER = 0x113, WM_USER = 0x400, WM_DPICHANGED = 0x02E0, WM_MOUSEACTIVATE = 0x21;

    // Vetro: sfocatura dietro la finestra (la tinta la disegna WPF sopra), angoli di Windows 11 spenti, niente bordo di sistema.
    public static void Vetro(IntPtr h)
    {
        var a = new ACCENT_POLICY { State = 3 };
        int size = Marshal.SizeOf(a);
        IntPtr p = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(a, p, false);
            var d = new WINCOMPATTRDATA { Attr = 19, Data = p, Size = size };
            SetWindowCompositionAttribute(h, ref d);
        }
        finally { Marshal.FreeHGlobal(p); }
        int angoli = 1; DwmSetWindowAttribute(h, 33, ref angoli, 4);
        int bordo = unchecked((int)0xFFFFFFFE); DwmSetWindowAttribute(h, 34, ref bordo, 4);
    }

    public static void StileStrumento(IntPtr h, bool nonAttivare)
    {
        long ex = (long)GetWindowLongPtr(h, GWL_EXSTYLE);
        ex |= WS_EX_TOOLWINDOW;
        ex &= ~WS_EX_APPWINDOW;
        if (nonAttivare) ex |= WS_EX_NOACTIVATE;
        SetWindowLongPtr(h, GWL_EXSTYLE, (IntPtr)ex);
    }

    public static string Classe(IntPtr h)
    {
        var s = new StringBuilder(256);
        GetClassName(h, s, 256);
        return s.ToString();
    }

    public static string Titolo(IntPtr h)
    {
        int n = GetWindowTextLength(h);
        if (n <= 0) return "";
        var s = new StringBuilder(n + 1);
        GetWindowText(h, s, n + 1);
        return s.ToString();
    }

    public static string ExeDiPid(uint pid)
    {
        IntPtr p = OpenProcess(0x1000, false, pid);
        if (p == IntPtr.Zero) return null;
        try
        {
            var s = new StringBuilder(1024);
            int n = s.Capacity;
            return QueryFullProcessImageName(p, 0, s, ref n) ? s.ToString() : null;
        }
        finally { CloseHandle(p); }
    }

    public static uint Pid(IntPtr h)
    {
        GetWindowThreadProcessId(h, out uint pid);
        return pid;
    }

    public static bool Nascosta(IntPtr h) => DwmGetWindowAttribute(h, 14, out int c, 4) == 0 && c != 0;

    public static POINT Cursore()
    {
        GetCursorPos(out var p);
        return p;
    }

    // Preme e rilascia una combinazione di tasti (es. Win, Win+N, Alt+Spazio).
    public static void Tasti(params byte[] vk)
    {
        foreach (var k in vk) keybd_event(k, 0, IsEsteso(k) ? 1u : 0u, UIntPtr.Zero);
        for (int i = vk.Length - 1; i >= 0; i--) keybd_event(vk[i], 0, (IsEsteso(vk[i]) ? 1u : 0u) | 2u, UIntPtr.Zero);
    }
    static bool IsEsteso(byte k) => k == 0x5B || k == 0x5C;

    public static IntPtr Regione(List<POINT> punti)
    {
        var a = punti.ToArray();
        return CreatePolygonRgn(a, a.Length, 2);
    }

    // Porta davanti una finestra: il processo della barra ha appena ricevuto il clic, quindi Windows lo permette.
    public static void Attiva(IntPtr h)
    {
        if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
        if (!SetForegroundWindow(h) || GetForegroundWindow() != h) SwitchToThisWindow(h, true);
    }
}
