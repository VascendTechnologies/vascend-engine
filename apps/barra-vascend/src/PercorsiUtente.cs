using System;
using System.IO;
namespace BarraVascend;
static class PercorsiUtente
{
    static string Distro => Environment.GetEnvironmentVariable("VASCEND_WSL_DISTRO") is string value && value.Length>0 ? value : "Ubuntu";
    public static string WslRoot => @"\\wsl.localhost\"+Distro;
    public static string WslLegacyRoot => @"\\wsl$\"+Distro;
    public static string WslHome => Environment.GetEnvironmentVariable("VASCEND_WSL_HOME") is string value && value.Length>0
        ? value : Path.Combine(WslRoot,"home",Environment.UserName);
}
