using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BarraVascend;

// Icone e nomi delle app dalla shell di Windows (exe, collegamenti, app dello Store).
static class Shell
{
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
        void SetValue(ref PROPERTYKEY k, ref PROPVARIANT v);
        void Commit();
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(N.SIZE size, int flags, out IntPtr hbm);
    }

    [ComImport, Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        [PreserveSig] int GetDisplayName(uint sigdn, out IntPtr name);
        void GetAttributes(uint mask, out uint attr);
        void Compare(IShellItem psi, uint hint, out int order);
    }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW
    {
        [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int cch, IntPtr fd, uint flags);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPersistFile
    {
        void GetClassID(out Guid id);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string file, uint mode);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out object item);
    [DllImport("shell32.dll")]
    static extern int SHGetPropertyStoreForWindow(IntPtr h, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr pbc, int flags, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT v);
    [DllImport("ole32.dll")] static extern void CoTaskMemFree(IntPtr p);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, out DIBSECTION ds);

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER { public int biSize, biWidth, biHeight; public short biPlanes, biBitCount; public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant; }
    [StructLayout(LayoutKind.Sequential)]
    struct DIBSECTION { public BITMAP dsBm; public BITMAPINFOHEADER dsBmih; public uint f0, f1, f2; public IntPtr dshSection; public uint dsOffset; }

    static PROPERTYKEY AumidKey = new() { fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), pid = 5 };
    static Guid IID_IPropertyStore = typeof(IPropertyStore).GUID;
    static Guid IID_ImageFactory = typeof(IShellItemImageFactory).GUID;
    static Guid IID_ShellItem = typeof(IShellItem).GUID;
    static readonly Dictionary<string, ImageSource> icone = new(StringComparer.OrdinalIgnoreCase);

    static string Leggi(IPropertyStore s)
    {
        if (s == null) return null;
        try
        {
            if (s.GetValue(ref AumidKey, out var v) != 0) return null;
            string r = v.vt == 31 ? Marshal.PtrToStringUni(v.p) : null;
            PropVariantClear(ref v);
            return string.IsNullOrWhiteSpace(r) ? null : r;
        }
        catch { return null; }
        finally { Marshal.ReleaseComObject(s); }
    }

    public static string AumidFinestra(IntPtr h)
    {
        try { return SHGetPropertyStoreForWindow(h, ref IID_IPropertyStore, out var s) == 0 ? Leggi(s) : null; }
        catch { return null; }
    }

    public static string AumidFile(string percorso)
    {
        try { return SHGetPropertyStoreFromParsingName(percorso, IntPtr.Zero, 0, ref IID_IPropertyStore, out var s) == 0 ? Leggi(s) : null; }
        catch { return null; }
    }

    public static string DestinazioneLnk(string lnk)
    {
        try
        {
            var t = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
            var o = Activator.CreateInstance(t);
            try
            {
                ((IPersistFile)o).Load(lnk, 0);
                var s = new StringBuilder(1024);
                ((IShellLinkW)o).GetPath(s, s.Capacity, IntPtr.Zero, 0);
                var r = s.ToString();
                return string.IsNullOrWhiteSpace(r) ? null : r;
            }
            finally { Marshal.ReleaseComObject(o); }
        }
        catch { return null; }
    }

    public static string NomeVisualizzato(string percorso)
    {
        try
        {
            if (SHCreateItemFromParsingName(percorso, IntPtr.Zero, ref IID_ShellItem, out var o) != 0) return null;
            try
            {
                if (((IShellItem)o).GetDisplayName(0, out var p) != 0) return null;
                var r = Marshal.PtrToStringUni(p);
                CoTaskMemFree(p);
                return r;
            }
            finally { Marshal.ReleaseComObject(o); }
        }
        catch { return null; }
    }

    // Icona grande (64 px) senza la freccia dei collegamenti, con la trasparenza giusta.
    public static ImageSource Icona(string percorso, int lato = 64, bool ricorda = true)
    {
        if (string.IsNullOrEmpty(percorso)) return null;
        string chiave = percorso + "|" + lato;
        if (icone.TryGetValue(chiave, out var c)) return c;
        ImageSource img = null;
        try
        {
            if (SHCreateItemFromParsingName(percorso, IntPtr.Zero, ref IID_ImageFactory, out var o) == 0)
            {
                try
                {
                    if (((IShellItemImageFactory)o).GetImage(new N.SIZE { cx = lato, cy = lato }, 0x1 | 0x4, out var hbm) == 0 && hbm != IntPtr.Zero)
                    {
                        img = DaHBitmap(hbm);
                        N.DeleteObject(hbm);
                    }
                }
                finally { Marshal.ReleaseComObject(o); }
            }
        }
        catch { }
        if (ricorda) icone[chiave] = img;
        return img;
    }

    public static ImageSource IconaApp(string aumid) => aumid == null ? null : Icona(@"shell:AppsFolder\" + aumid);

    static ImageSource DaHBitmap(IntPtr hbm)
    {
        if (GetObject(hbm, Marshal.SizeOf<DIBSECTION>(), out var ds) == 0 || ds.dsBm.bmBits == IntPtr.Zero || ds.dsBm.bmBitsPixel != 32) return null;
        int w = ds.dsBm.bmWidth, h = ds.dsBm.bmHeight, stride = ds.dsBm.bmWidthBytes;
        var bytes = new byte[stride * h];
        Marshal.Copy(ds.dsBm.bmBits, bytes, 0, bytes.Length);
        if (ds.dsBmih.biHeight > 0)
        {
            var rigata = new byte[bytes.Length];
            for (int y = 0; y < h; y++) Buffer.BlockCopy(bytes, y * stride, rigata, (h - 1 - y) * stride, stride);
            bytes = rigata;
        }
        // la shell restituisce a volte alpha premoltiplicato, a volte no (icone PNG delle app dello Store e delle PWA):
        // se un colore supera la sua alpha non può essere premoltiplicato, e trattarlo così schiarisce i bordi
        bool dritto = false, alpha = false;
        for (int i = 0; i + 3 < bytes.Length; i += 4)
        {
            byte al = bytes[i + 3];
            if (al != 0) alpha = true;
            if (bytes[i] > al || bytes[i + 1] > al || bytes[i + 2] > al) { dritto = true; if (alpha) break; }
        }
        if (!alpha) for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
        var b = BitmapSource.Create(w, h, 96, 96, alpha && dritto ? PixelFormats.Bgra32 : PixelFormats.Pbgra32, null, bytes, stride);
        b.Freeze();
        return b;
    }

    public static ImageSource DaHIcon(IntPtr hicon)
    {
        if (hicon == IntPtr.Zero) return null;
        try
        {
            var b = Imaging.CreateBitmapSourceFromHIcon(hicon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            b.Freeze();
            return b;
        }
        catch { return null; }
    }

    // Icona impostata dalla finestra stessa: serve per le app che non ne hanno una nell'exe.
    public static ImageSource IconaFinestra(IntPtr h)
    {
        N.SendMessageTimeout(h, 0x7F, (IntPtr)1, IntPtr.Zero, 0x2, 100, out var hi);
        if (hi == IntPtr.Zero) N.SendMessageTimeout(h, 0x7F, (IntPtr)2, IntPtr.Zero, 0x2, 100, out hi);
        if (hi == IntPtr.Zero) hi = N.GetClassLongPtr(h, -14);
        return DaHIcon(hi);
    }

    public static string NomeExe(string exe)
    {
        try
        {
            var v = System.Diagnostics.FileVersionInfo.GetVersionInfo(exe);
            if (!string.IsNullOrWhiteSpace(v.FileDescription)) return v.FileDescription.Trim();
        }
        catch { }
        return Path.GetFileNameWithoutExtension(exe);
    }
}
