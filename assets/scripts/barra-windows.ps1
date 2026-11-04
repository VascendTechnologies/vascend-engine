# Rende invisibile la barra di Windows, che YASB sostituisce, lasciandola attiva.
#   powershell -File barra-windows.ps1          -> invisibile e resta in ascolto
#   powershell -File barra-windows.ps1 mostra   -> ferma il guardiano e la rimostra
# La barra di Windows resta attiva in nascondimento automatico (le app usano
# tutto lo schermo) ma diventa trasparente (alpha 0) e lascia passare i clic
# (WS_EX_TRANSPARENT). Tocca solo le finestre di explorer.exe: YASB ha una sua
# finestra Shell_TrayWnd per la tray, che non va toccata.
# Il guardiano riapplica tutto se Explorer ricrea la barra (WinEvent
# EVENT_OBJECT_CREATE/SHOW) e ricontrolla ogni 3 s. Un solo guardiano (mutex).
param([string]$azione = 'nascondi')
Add-Type -ReferencedAssemblies System.Windows.Forms @'
using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Runtime.InteropServices;
public static class Barra {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int l, t, r, b; }
  [StructLayout(LayoutKind.Sequential)] public struct APPBARDATA { public int cbSize; public IntPtr hWnd; public uint msg; public uint edge; public RECT rc; public IntPtr lParam; }
  [DllImport("shell32.dll")] static extern IntPtr SHAppBarMessage(uint m, ref APPBARDATA d);
  [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string t);
  [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr a, string c, string t);
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int n);
  [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
  [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h, uint k, byte a, uint f);
  [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  delegate bool EnumProc(IntPtr h, IntPtr p);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr p);
  static bool DiExplorer(IntPtr h) {
    uint pid; GetWindowThreadProcessId(h, out pid);
    try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName.ToLower() == "explorer"; } catch { return false; }
  }
  // tutte le barre di Windows vere (principale e altri schermi), mai le finestre omonime di YASB
  static System.Collections.Generic.List<IntPtr> Barre() {
    var l = new System.Collections.Generic.List<IntPtr>();
    EnumWindows((h, p) => { if (EBarra(h) && DiExplorer(h)) l.Add(h); return true; }, IntPtr.Zero);
    return l;
  }
  static IntPtr Principale() {
    foreach (var h in Barre()) { var s = new StringBuilder(64); GetClassName(h, s, 64); if (s.ToString() == "Shell_TrayWnd") return h; }
    return IntPtr.Zero;
  }
  delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
  [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
  static WinEventProc proc;
  const int EX = -20, LAYERED = 0x80000, TRASP = 0x20;

  static void AutoHide(bool on) {
    var d = new APPBARDATA(); d.cbSize = Marshal.SizeOf(d); d.hWnd = Principale();
    d.lParam = (IntPtr)(on ? 1 : 2); SHAppBarMessage(10, ref d);
  }
  static bool EBarra(IntPtr h) {
    var s = new StringBuilder(64); GetClassName(h, s, 64); var c = s.ToString();
    return c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd";
  }
  static void Una(IntPtr h, bool visibile) {
    if (h == IntPtr.Zero) return;
    int ex = GetWindowLong(h, EX);
    if (visibile) { SetLayeredWindowAttributes(h, 0, 255, 2); SetWindowLong(h, EX, ex & ~TRASP); }
    else if ((ex & (LAYERED | TRASP)) != (LAYERED | TRASP)) { SetWindowLong(h, EX, ex | LAYERED | TRASP); SetLayeredWindowAttributes(h, 0, 0, 2); }
  }
  public static void Tutte(bool visibile) { foreach (var h in Barre()) Una(h, visibile); }
  static bool GiaInvisibile() {
    foreach (var h in Barre()) if ((GetWindowLong(h, EX) & (LAYERED | TRASP)) != (LAYERED | TRASP)) return false;
    return true;
  }
  public static void Mostra() { AutoHide(false); Tutte(true); }
  public static void Guarda() {
    bool nuovo; var m = new Mutex(true, "Local\\VascendBarraWindows", out nuovo);
    if (!nuovo) return;
    AutoHide(true); Tutte(false);
    proc = (hook, ev, hwnd, idObject, idChild, thread, time) => {
      if (idObject == 0 && hwnd != IntPtr.Zero && EBarra(hwnd) && DiExplorer(hwnd)) Una(hwnd, false);
    };
    // EVENT_OBJECT_CREATE .. EVENT_OBJECT_SHOW, fuori contesto, tutti i processi
    SetWinEventHook(0x8000, 0x8002, IntPtr.Zero, proc, 0, 0, 0);
    var t = new System.Windows.Forms.Timer(); t.Interval = 3000;
    t.Tick += (s, e) => { if (!GiaInvisibile()) Tutte(false); };
    t.Start();
    Application.Run();
    GC.KeepAlive(m);
  }
}
'@
if ($azione -eq 'mostra') {
  Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -like '*-File*barra-windows.ps1*' -and $_.ProcessId -ne $PID -and $_.CommandLine -notlike '*mostra*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
  [Barra]::Mostra()
} else {
  [Barra]::Guarda()
}
