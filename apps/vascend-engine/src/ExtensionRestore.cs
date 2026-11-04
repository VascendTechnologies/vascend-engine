using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BarraVascend;

namespace VascendEngine;

public interface IEngineExtensionRestoration
{
    string RestorationDescription {get;}
    Task RestoreSettingsAsync(CancellationToken cancellationToken);
}
sealed class RestoreChoices
{
    public bool Taskbar {get;set;}
    public bool Startup {get;set;}
    public bool Applications {get;set;}
}
sealed class DesktopRestoreState
{
    public bool HideTaskbar {get;set;}
    public bool BarAtStartup {get;set;}
    public bool EngineAtStartup {get;set;}
    public bool TaskbarRestored {get;set;}
    public bool StartupRestored {get;set;}
}
sealed class SettingsBackup
{
    public string Name {get;set;}
    public string Path {get;set;}
    public bool Terminal {get;set;}
}
static class ExtensionRestore
{
    const string BackupSuffix=".prima-vascend-engine.bak";
    static string StatePath=>Path.Combine(Stile.Cartella,"extension-restore","desktop.json");
    static string TaskbarScript=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".config\yasb\barra-windows.ps1");
    public static bool CanRestoreTaskbar=>File.Exists(TaskbarScript);
    public static List<SettingsBackup> AvailableBackups()
    {
        var candidates=new List<SettingsBackup>();
        string roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        candidates.Add(new(){Name="VS Code",Path=Path.Combine(roaming,@"Code\User\settings.json")});
        foreach(var ide in Generatori.Ide)candidates.Add(new(){Name="Vascend IDE "+ide.nome,Path=ide.impostazioni});
        foreach(string package in new[]{"Microsoft.WindowsTerminal_8wekyb3d8bbwe","Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe"})
            candidates.Add(new(){Name=package.Contains("Preview") ? "Terminal Preview" : "Windows Terminal",Path=Path.Combine(local,"Packages",package,@"LocalState\settings.json"),Terminal=true});
        candidates.Add(new(){Name="Windows Terminal",Path=Path.Combine(local,@"Microsoft\Windows Terminal\settings.json"),Terminal=true});
        return candidates.Where(b=>File.Exists(b.Path) && File.Exists(b.Path+BackupSuffix)).ToList();
    }
    static JsonObject Read(string path)=>JsonNode.Parse(File.ReadAllText(path),null,new JsonDocumentOptions{CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true}) as JsonObject ?? throw new Exception("Impostazioni non valide: "+path);
    static void RestoreKey(JsonObject target,JsonObject before,string key)
    {
        if(before != null && before.TryGetPropertyValue(key,out var value))target[key]=value?.DeepClone();else target.Remove(key);
    }
    static void RestoreThemeSection(JsonObject current,JsonObject original,string key)
    {
        string scope="["+Generatori.NomeTema+"]";
        if(current[key] is not JsonObject target)return;
        RestoreKey(target,original[key] as JsonObject ?? new JsonObject(),scope);
        if(target.Count == 0)current.Remove(key);
    }
    static void RestoreNamed(JsonObject current,JsonObject original,string key)
    {
        if(current[key] is not JsonArray values)return;
        var kept=new JsonArray();
        foreach(var value in values)
            if(value is not JsonObject item || item["name"]?.GetValue<string>() != Generatori.NomeTema)kept.Add(value?.DeepClone());
        if(original[key] is JsonArray old)
            foreach(var value in old)
                if(value is JsonObject item && item["name"]?.GetValue<string>() == Generatori.NomeTema)kept.Add(value.DeepClone());
        if(kept.Count == 0 && !original.ContainsKey(key))current.Remove(key);else current[key]=kept;
    }
    public static void RestoreApplication(SettingsBackup backup)
    {
        var current=Read(backup.Path);var before=Read(backup.Path+BackupSuffix);
        if(!backup.Terminal)
        {
            foreach(string key in new[]{"editor.fontFamily","editor.fontSize","editor.fontLigatures","terminal.integrated.fontFamily","terminal.integrated.fontSize","chat.editor.fontFamily","workbench.colorTheme"})RestoreKey(current,before,key);
            if(backup.Name.StartsWith("Vascend IDE",StringComparison.Ordinal))foreach(string key in new[]{"horizon.performanceMode","horizon.island.vascend"})RestoreKey(current,before,key);
            foreach(string key in new[]{"workbench.colorCustomizations","editor.tokenColorCustomizations","editor.semanticTokenColorCustomizations"})RestoreThemeSection(current,before,key);
        }
        else
        {
            RestoreKey(current,before,"theme");RestoreNamed(current,before,"themes");RestoreNamed(current,before,"schemes");
            if(current["profiles"] is JsonObject profiles && profiles["defaults"] is JsonObject defaults)
            {
                var old=(before["profiles"] as JsonObject)?["defaults"] as JsonObject;
                foreach(string key in new[]{"colorScheme","cursorShape","padding","opacity","useAcrylic","intenseTextStyle","scrollbarState","font"})RestoreKey(defaults,old,key);
                if(defaults.Count == 0 && old == null)profiles.Remove("defaults");
            }
        }
        // Copia del file attuale prima del ripristino, senza sovrascrivere lo storico.
        File.Copy(backup.Path,backup.Path+".prima-ripristino-"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+".bak");
        string temporary=backup.Path+".restore-"+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,current.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,backup.Path,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    static async Task RunTaskbar(string action)
    {
        if(!CanRestoreTaskbar)throw new Exception("Script di ripristino della taskbar non trovato.");
        var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true};
        foreach(string arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",TaskbarScript,action})start.ArgumentList.Add(arg);
        using var process=Process.Start(start) ?? throw new Exception("Impossibile avviare il ripristino della taskbar.");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try{await process.WaitForExitAsync(timeout.Token);}
        catch(OperationCanceledException){try{process.Kill();}catch{}throw new Exception("Ripristino taskbar scaduto. L'estensione resta disattivata.");}
        if(process.ExitCode != 0)throw new Exception("Ripristino taskbar non riuscito.");
    }
    static void Save(DesktopRestoreState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
        string temp=StatePath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(state));File.Move(temp,StatePath,true);
    }
    public static async Task<string> RestoreAsync(RestoreChoices choices)
    {
        var state=new DesktopRestoreState {HideTaskbar=Sistema.NascondiBarraWindows,BarAtStartup=Sistema.BarraAllAvvio,EngineAtStartup=Sistema.EngineAllAvvio};
        var messages=new List<string>();var errors=new List<string>();
        // L'host desktop sta terminando attraverso il watcher dello stato: lascia che liberi l'area di lavoro.
        foreach(var process in Process.GetProcessesByName("BarraVascend"))
        {
            try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));await process.WaitForExitAsync(timeout.Token);}catch{}finally{process.Dispose();}
        }
        if(choices.Taskbar)
        {
            try{await RunTaskbar("mostra");Sistema.NascondiBarraWindows=false;state.TaskbarRestored=true;messages.Add("Taskbar Windows ripristinata");}
            catch(Exception ex){errors.Add(ex.Message);}
        }
        if(choices.Startup)
        {
            try{Sistema.BarraAllAvvio=false;Sistema.EngineAllAvvio=false;state.StartupRestored=true;messages.Add("Avvio automatico Vascend rimosso");}
            catch(Exception ex){errors.Add(ex.Message);}
        }
        if(choices.Applications)
        {
            foreach(var backup in AvailableBackups())
            {
                try{RestoreApplication(backup);messages.Add(backup.Name+" ripristinato");}catch(Exception ex){errors.Add(backup.Name+": "+ex.Message);}
            }
        }
        Save(state);
        if(errors.Count > 0)throw new Exception(string.Join(" · ",messages)+"\nRipristino parziale: "+string.Join("; ",errors));
        return string.Join(" · ",messages);
    }
    public static async Task ReapplyAsync()
    {
        if(!File.Exists(StatePath))return;
        var state=JsonSerializer.Deserialize<DesktopRestoreState>(File.ReadAllText(StatePath));if(state == null)return;
        if(state.TaskbarRestored && state.HideTaskbar)
        {
            // Il guardiano è persistente: non aspettare l'uscita del processo.
            if(!CanRestoreTaskbar)throw new Exception("Script della taskbar non trovato.");
            var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"-NoProfile","-NonInteractive","-ExecutionPolicy","Bypass","-File",TaskbarScript})start.ArgumentList.Add(arg);
            Process.Start(start)?.Dispose();Sistema.NascondiBarraWindows=true;
        }
        if(state.StartupRestored){Sistema.BarraAllAvvio=state.BarAtStartup;Sistema.EngineAllAvvio=state.EngineAtStartup;}
        state.TaskbarRestored=false;state.StartupRestored=false;Save(state);
        await Task.CompletedTask;
    }
}

partial class Finestra
{
    RestoreChoices ConfermaDisattivazione()
    {
        RestoreChoices choice=null;
        var dialog=new Window {Owner=this,Title=I18n.Translate("Disattiva Vascend Technologies"),Width=590,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=System.Windows.Media.Brushes.Transparent,ShowInTaskbar=false};
        var body=new StackPanel {Margin=new Thickness(24)};
        var title=Tema.Scritta("Disattivare Vascend Technologies?",23,Tema.Bianco,FontWeights.SemiBold);title.TextWrapping=TextWrapping.Wrap;body.Children.Add(title);
        var intro=Tema.Scritta("Barra Vascend, isola e widget verranno sospesi. Puoi anche ripristinare le seguenti impostazioni.",13,Tema.BiancoTenue);intro.TextWrapping=TextWrapping.Wrap;intro.Margin=new Thickness(0,12,0,20);body.Children.Add(intro);
        CheckBox Option(string label,bool check,bool available=true)
        {
            var control=new CheckBox {Content=Tema.Scritta(label,13),IsChecked=check && available,IsEnabled=available,Margin=new Thickness(0,0,0,14),VerticalContentAlignment=VerticalAlignment.Center};body.Children.Add(control);return control;
        }
        var taskbar=Option("Mostra la taskbar di Windows e rimuovi il suo nascondimento all'avvio",true,ExtensionRestore.CanRestoreTaskbar);
        var startup=Option("Rimuovi l'avvio automatico di barra e stile Vascend",true);
        var backups=ExtensionRestore.AvailableBackups();
        var apps=Option("Ripristina tema e font delle app dalle copie precedenti",false,backups.Count > 0);
        var note=Tema.Scritta(backups.Count > 0 ? "Copie disponibili: "+string.Join(", ",backups.Select(b=>b.Name))+". Vengono ripristinate solo le impostazioni modificate da Engine." : "Non ci sono copie delle impostazioni delle app da ripristinare.",12,Tema.BiancoTenue);
        note.TextWrapping=TextWrapping.Wrap;note.Margin=new Thickness(0,0,0,12);body.Children.Add(note);
        var keep=Tema.Scritta("I colori di Windows e le configurazioni dei widget restano salvati. Alla riattivazione ripristiniamo le preferenze di taskbar e avvio di Vascend.",12,Tema.BiancoTenue);
        keep.TextWrapping=TextWrapping.Wrap;keep.Margin=new Thickness(0,0,0,22);body.Children.Add(keep);
        var actions=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right};
        actions.Children.Add(Pulsante("Annulla",()=>dialog.Close()));
        actions.Children.Add(Pulsante("Solo disattiva",()=>{choice=new RestoreChoices();dialog.DialogResult=true;}));
        actions.Children.Add(Pulsante("Disattiva e ripristina",()=>{choice=new RestoreChoices {Taskbar=taskbar.IsChecked==true,Startup=startup.IsChecked==true,Applications=apps.IsChecked==true};dialog.DialogResult=true;},primary:true,icon:"refresh"));
        body.Children.Add(actions);
        dialog.Content=new Border {Background=Tema.GhiaccioPannello,BorderBrush=Contorno,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=body};
        dialog.KeyDown += (_,e)=>{if(e.Key == System.Windows.Input.Key.Escape)dialog.Close();};
        dialog.ShowDialog();return choice;
    }
}
