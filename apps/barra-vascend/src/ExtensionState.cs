using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace BarraVascend;

static class ExtensionState
{
    public const string BuiltinId = "vascend-technologies";
    public static string DefaultRoot => Stile.Cartella;
    public static Dictionary<string,bool> Load(string root=null)
    {
        string file=Path.Combine(root ?? DefaultRoot,"extension-state.json");
        if(!File.Exists(file)) return new();
        return JsonSerializer.Deserialize<Dictionary<string,bool>>(File.ReadAllText(file)) ?? throw new Exception("Stato estensioni non valido.");
    }
    public static bool Enabled(string id,string root=null)
    {
        var state=Load(root);
        return state.TryGetValue(id,out bool enabled) ? enabled : id != BuiltinId;
    }
    public static void Set(string id,bool enabled,string root=null)
    {
        root ??= DefaultRoot;Directory.CreateDirectory(root);
        // Serializza anche le modifiche provenienti da altre istanze di Engine.
        using var guard=new FileStream(Path.Combine(root,"extension-state.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        var state=Load(root);state[id]=enabled;
        string file=Path.Combine(root,"extension-state.json"),temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.WriteAllText(temp,JsonSerializer.Serialize(state,new JsonSerializerOptions{WriteIndented=true}));File.Move(temp,file,true);}
        finally {if(File.Exists(temp))File.Delete(temp);}
    }
}
