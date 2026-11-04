using System;
using System.Runtime.InteropServices;

namespace BarraVascend;

sealed class WifiSignalResult
{
    public bool Available {get;set;}
    public int Quality {get;set;}
    public uint Error {get;set;}
    public string Source {get;set;}="unavailable";
}
static class WifiSignal
{
    [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version,IntPtr reserved,out uint negotiated,out IntPtr handle);
    [DllImport("wlanapi.dll")] static extern uint WlanQueryInterface(IntPtr handle,ref Guid guid,uint opcode,IntPtr reserved,out uint size,out IntPtr data,IntPtr valueType);
    [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr handle,IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);
    // La qualità realtime omette SSID/BSSID e non richiede l'accesso alla posizione.
    const uint RealtimeQuality=19,Rssi=0x10000102;
    public static bool ReadRealtimeBuffer(IntPtr data,uint size,out int quality)
    {
        quality=0;
        if(data == IntPtr.Zero || size < 24)return false;
        int value=Marshal.ReadInt32(data,4); // WLAN_REALTIME_CONNECTION_QUALITY.ulLinkQuality
        if(value < 0 || value > 100)return false;
        quality=value;return true;
    }
    public static int QualityFromRssi(int value)=>(int)Math.Clamp(2L*(value+100L),0L,100L);
    public static WifiSignalResult Read(string interfaceId)
    {
        var result=new WifiSignalResult();
        if(!Guid.TryParse(interfaceId,out var id)){result.Error=87;return result;}
        IntPtr handle=IntPtr.Zero,data=IntPtr.Zero;
        try
        {
            result.Error=WlanOpenHandle(2,IntPtr.Zero,out _,out handle);
            if(result.Error != 0)return result;
            result.Error=WlanQueryInterface(handle,ref id,RealtimeQuality,IntPtr.Zero,out uint size,out data,IntPtr.Zero);
            if(result.Error == 0 && ReadRealtimeBuffer(data,size,out int quality))
            {
                result.Available=true;result.Quality=quality;result.Source="realtime";return result;
            }
            if(data != IntPtr.Zero){WlanFreeMemory(data);data=IntPtr.Zero;}
            // Compatibilità con i sistemi che non supportano il nuovo opcode.
            result.Error=WlanQueryInterface(handle,ref id,Rssi,IntPtr.Zero,out size,out data,IntPtr.Zero);
            if(result.Error == 0 && size >= 4 && data != IntPtr.Zero)
            {
                int rssi=Marshal.ReadInt32(data);
                if(rssi >= -127 && rssi < 0){result.Available=true;result.Quality=QualityFromRssi(rssi);result.Source="rssi";}
            }
        }
        catch(DllNotFoundException){result.Error=126;}
        catch(EntryPointNotFoundException){result.Error=127;}
        finally{if(data != IntPtr.Zero)WlanFreeMemory(data);if(handle != IntPtr.Zero)WlanCloseHandle(handle,IntPtr.Zero);}
        return result;
    }
}
