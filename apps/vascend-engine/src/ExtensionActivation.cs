using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using BarraVascend;

namespace VascendEngine;

public interface IEngineExtensionLifecycle
{
    void OnActivated(ExtensionContext context);
    void OnDeactivated();
}

sealed class ExtensionActivation
{
    readonly string root;
    public ExtensionActivation(string directory=null) {root=directory ?? Stile.Cartella;}
    public static ExtensionManifest Builtin() => new() {
        Id=ExtensionState.BuiltinId,Name="Vascend Technologies",Author="Vascend",Version="1.0.0",
        Description="Stile, barra, isola, widget e strumenti. Il tuo ambiente, in un unico posto.",
        ExclusiveResources=new() {"desktop.bar","desktop.widgets","desktop.island","system.appearance","editor.vscode.theme","editor.vascend.theme","terminal.theme"}
    };
    public List<ExtensionManifest> Installed()
    {
        var list=new List<ExtensionManifest>{Builtin()};
        string path=Path.Combine(root,"extensions");
        if(Directory.Exists(path)) foreach(string directory in Directory.GetDirectories(path))
        {
            try {list.Add(Finestra.LeggiManifest(directory));} catch { }
        }
        return list;
    }
    public bool Enabled(string id) => ExtensionState.Enabled(id,root);
    static bool Overlap(string a,string b) => a == b || a.StartsWith(b+".",StringComparison.Ordinal) || b.StartsWith(a+".",StringComparison.Ordinal);
    public List<string> Issues(ExtensionManifest plugin,List<ExtensionManifest> installed=null)
    {
        installed ??= Installed();var state=ExtensionState.Load(root);
        bool Enabled(ExtensionManifest p)=>state.TryGetValue(p.Id,out bool enabled) ? enabled : p.Id != ExtensionState.BuiltinId;
        var issues=new List<string>();
        foreach(var other in installed.Where(p=>p.Id != plugin.Id && Enabled(p)))
        {
            if(plugin.ConflictsWith.Contains(other.Id) || other.ConflictsWith.Contains(plugin.Id))
                issues.Add("Incompatibile con "+other.Name+" (incompatibilità dichiarata).");
            var resources=plugin.ExclusiveResources.SelectMany(a=>other.ExclusiveResources.Where(b=>Overlap(a,b)).Select(b=>a.Length <= b.Length ? a : b)).Distinct().ToList();
            if(resources.Count > 0) issues.Add("Conflitto con "+other.Name+": "+string.Join(", ",resources)+".");
        }
        if(installed.Count(p=>p.Id == plugin.Id)>1) issues.Add("Più estensioni hanno lo stesso ID: "+plugin.Id+".");
        return issues;
    }
    public void SetEnabled(string id,bool enabled)
    {
        var installed=Installed();
        var matches=installed.Where(p=>p.Id == id).ToList();
        if(matches.Count != 1) throw new Exception("Estensione non trovata o ID duplicato.");
        if(enabled)
        {
            var issues=Issues(matches[0],installed);
            if(issues.Count > 0) throw new Exception(string.Join("\n",issues)+"\nDisattiva l'altra estensione prima di attivare questa.");
        }
        ExtensionState.Set(id,enabled,root);
    }
}

partial class Finestra
{
    readonly ExtensionActivation activation=new();
    readonly Dictionary<string,CancellationTokenSource> pluginTokens=new();
    readonly HashSet<string> restartRequired=new();

    bool activationBusy;
    async void ToggleExtension(string id)
    {
        if(activationBusy)return;
        bool previous=activation.Enabled(id);
        RestoreChoices restore=null;
        IEngineExtensionRestoration pluginRestore=null;
        if(previous && id == ExtensionState.BuiltinId)
        {
            restore=ConfermaDisattivazione();if(restore == null)return;
        }
        if(previous && plugins.TryGetValue(id,out var restoreCandidate) && restoreCandidate is IEngineExtensionRestoration restorable)
        {
            var result=MessageBox.Show(this,restorable.RestorationDescription+"\n\nVuoi ripristinare queste impostazioni dopo la disattivazione?", "Disattiva estensione",MessageBoxButton.YesNoCancel);
            if(result == MessageBoxResult.Cancel)return;
            if(result == MessageBoxResult.Yes)pluginRestore=restorable;
        }
        activationBusy=true;
        lavoro.IsEnabled=false;
        try
        {
            if(!previous && restartRequired.Contains(id)) throw new Exception("Riavvia Engine prima di riattivare questa estensione: il suo codice non supporta la disattivazione a caldo.");
            if(previous && plugins.TryGetValue(id,out var plugin))
            {
                // Il nuovo contratto richiede di rilasciare eventi, risorse e attività.
                if(plugin is IEngineExtensionLifecycle lifecycle) lifecycle.OnDeactivated();
                else {if(plugin is IDisposable disposable)disposable.Dispose();restartRequired.Add(id);}
                if(pluginTokens.Remove(id,out var token)){try{token.Cancel();}catch{}token.Dispose();}
                plugins.Remove(id);
            }
            activation.SetEnabled(id,!previous);
            if(id == ExtensionState.BuiltinId && !previous)
            {
                try {await ExtensionRestore.ReapplyAsync();AvviaTecnologie();}
                catch {ExtensionState.Set(id,false);throw;}
            }
            string restored="";
            if(restore != null && (restore.Taskbar || restore.Startup || restore.Applications))
            {
                Esito("Estensione disattivata. Ripristino delle impostazioni…");
                restored=await System.Threading.Tasks.Task.Run(()=>ExtensionRestore.RestoreAsync(restore));
            }
            if(pluginRestore != null)await pluginRestore.RestoreSettingsAsync(CancellationToken.None);
            Catalogo();
            if(restored.Length > 0){Esito(restored);return;}
            Esito(!previous ? "Estensione attivata." : restartRequired.Contains(id) ? "Disattivata. Riavvia Engine per terminare il codice già caricato." : "Estensione disattivata.");
        }
        catch(Exception ex){Catalogo();Esito(ex.Message,true);}
        finally{activationBusy=false;lavoro.IsEnabled=true;}
    }
    static void AvviaTecnologie()
    {
        string exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".config\barra-vascend\bin\BarraVascend.exe");
        if(File.Exists(exe)) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)});
    }
    bool VerificaAttivazione(ExtensionManifest manifest)
    {
        var issues=activation.Issues(manifest);
        if(activation.Enabled(manifest.Id) && issues.Count == 0) return true;
        ResetPagina(manifest.Name);
        var page=new StackPanel{Margin=new Thickness(4,12,0,0)};
        var back=Pulsante("Estensioni",Catalogo,icon:"arrow-left");back.HorizontalAlignment=HorizontalAlignment.Left;page.Children.Add(back);
        var title=Tema.Scritta(issues.Count > 0 ? "Conflitto tra estensioni" : "Estensione disattivata",24,Tema.Bianco,FontWeights.SemiBold);
        title.Margin=new Thickness(0,24,0,12);page.Children.Add(title);
        var description=Tema.Scritta(issues.Count > 0 ? string.Join("\n",issues) : "Attiva l'estensione dalla home per aprire le sue impostazioni.",14,Tema.BiancoTenue);
        description.TextWrapping=TextWrapping.Wrap;page.Children.Add(description);
        lavoro.Content=Scorri(page);return false;
    }
    void ChiudiEstensioni()
    {
        foreach(var pair in plugins.ToList())
        {
            try{if(pair.Value is IEngineExtensionLifecycle lifecycle)lifecycle.OnDeactivated();else if(pair.Value is IDisposable disposable)disposable.Dispose();}catch{}
        }
        foreach(var token in pluginTokens.Values){try{token.Cancel();}catch{}token.Dispose();}
        plugins.Clear();pluginTokens.Clear();
    }
}
