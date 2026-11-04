using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace VascendEngine;

partial class Finestra
{
    readonly GitHubExtensions github = new();
    static TextBox CampoRepository(string value="") => new() {
        Text=value, FontFamily=Tema.Testo,FontSize=14,Foreground=Tema.Bianco,CaretBrush=Tema.LimePennello,
        Background=Tema.Pennello(27,29,24,1),BorderBrush=Contorno,BorderThickness=new Thickness(1),
        Padding=new Thickness(12,10,12,10),HorizontalContentAlignment=HorizontalAlignment.Left,
        VerticalContentAlignment=VerticalAlignment.Center, MinWidth=180
    };
    void ApriAggiungiEstensione()
    {
        ResetPagina("Estensioni / GitHub");
        fasciaAzioni.Visibility=Visibility.Collapsed;
        var page=new DockPanel();
        var back=Pulsante("Estensioni",Catalogo,icon:"arrow-left");
        back.HorizontalAlignment=HorizontalAlignment.Left; back.Margin=new Thickness(0,0,0,20);
        DockPanel.SetDock(back,Dock.Top); page.Children.Add(back);
        var body=new StackPanel { Margin=new Thickness(4,8,12,0) };
        body.Children.Add(Tema.Scritta("Aggiungi estensioni",28,Tema.Bianco,FontWeights.SemiBold));
        var intro=Tema.Scritta("Collega un repository GitHub e scegli i plugin da installare.",14,Tema.BiancoTenue);
        intro.Margin=new Thickness(0,10,0,24); body.Children.Add(intro);
        var form=new StackPanel { Margin=new Thickness(20) };
        form.Children.Add(EtichettaAzione("Da GitHub","github",Tema.Bianco,16,bold:true));
        var help=Tema.Scritta("Repository pubblico · link GitHub oppure autore/repository",12,Tema.BiancoTenue);
        help.Margin=new Thickness(0,16,0,8);form.Children.Add(help);
        var repo=CampoRepository();
        System.Windows.Automation.AutomationProperties.SetName(repo,"Repository GitHub");
        repo.ToolTip=Suggerimento("https://github.com/autore/repository oppure autore/repository");
        form.Children.Add(repo);
        var branchLabel=Tema.Scritta("Branch, tag o commit (facoltativo)",12,Tema.BiancoTenue);
        branchLabel.Margin=new Thickness(0,14,0,8);form.Children.Add(branchLabel);
        var branch=CampoRepository();
        System.Windows.Automation.AutomationProperties.SetName(branch,"Branch, tag o commit");
        branch.ToolTip=Suggerimento("Lascia vuoto per seguire il branch predefinito del repository.");
        form.Children.Add(branch);
        var status=Tema.Scritta("Il repository deve contenere plugin Engine con extension.json.",12,Tema.BiancoTenue);
        status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(0,14,0,0);
        var results=new StackPanel { Margin=new Thickness(0,22,0,0) };
        var linked=new StackPanel { Margin=new Thickness(0,24,0,0) };
        GitHubDownload download=null;
        var lifetime=new CancellationTokenSource();
        bool busy=false, departed=false;
        Button search=null,local=null;
        void SetBusy(bool value)
        {
            busy=value;
            repo.IsEnabled=branch.IsEnabled=results.IsEnabled=linked.IsEnabled=!value;
            if(search != null) search.IsEnabled=!value;
            if(local != null) local.IsEnabled=!value;
        }
        void Status(string message,bool error=false)
        {
            status.Text=message;
            status.Foreground=error ? Tema.Pennello(239,151,122,1) : Tema.BiancoTenue;
        }
        void Finished()
        {
            SetBusy(false);
            if(departed) { download?.Dispose(); download=null; lifetime.Dispose(); }
        }
        bool Trust(ExtensionManifest plugin) => string.IsNullOrWhiteSpace(plugin.EntryAssembly) ||
            I18n.ShowMessage(this,$"{plugin.Name} contiene codice che verrà eseguito sul tuo computer all’apertura della pagina.\n\nInstalla solo se ti fidi dell’autore del repository.","Installa estensione",MessageBoxButton.OKCancel) == MessageBoxResult.OK;
        async Task Install(ExtensionManifest plugin,Button button)
        {
            if(busy || !Trust(plugin)) return;
            SetBusy(true);Status("Installazione di "+plugin.Name+"…");
            try
            {
                await Task.Run(()=>github.Install(download,plugin,lifetime.Token));
                if(departed) return;
                button.Content=EtichettaAzione("Installata","check",Tema.Bianco,12);
                button.IsEnabled=false;
                Status(plugin.Name+" installata e disattivata. Puoi attivarla dalla home dopo il controllo dei conflitti.");
                RefreshLinked();
            }
            catch(OperationCanceledException) { if(!departed) Status("Installazione annullata."); }
            catch(Exception ex) { if(!departed) Status(ex.GetBaseException().Message,true); }
            finally { Finished(); }
        }
        async Task Search()
        {
            if(busy) return;
            SetBusy(true); results.Children.Clear();download?.Dispose();download=null;
            try
            {
                download=await github.DownloadAsync(repo.Text,branch.Text,lifetime.Token,new Progress<string>(message=>{ if(!departed) Status(message); }));
                lifetime.Token.ThrowIfCancellationRequested();
                Status($"{download.Plugins.Count} plugin trovati · {download.Source.Repository} · commit {download.Source.Commit[..7]}");
                foreach(var plugin in download.Plugins.OrderBy(p=>p.Name))
                {
                    var row=new Grid { Margin=new Thickness(0,0,0,10) };
                    row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
                    var text=new StackPanel { Margin=new Thickness(0,0,18,0) };
                    text.Children.Add(Tema.Scritta(plugin.Name+"  ·  "+plugin.Version,15,Tema.Bianco,FontWeights.SemiBold));
                    var description=Tema.Scritta(plugin.Description ?? plugin.Id,12,Tema.BiancoTenue);
                    description.TextWrapping=TextWrapping.Wrap;description.Margin=new Thickness(0,5,0,0);text.Children.Add(description);
                    string relative=Path.GetRelativePath(download.RepositoryDirectory,plugin.Directory).Replace('\\','/');
                    var location=Tema.Scritta(relative == "." ? "Radice del repository" : relative,11,Tema.BiancoTenue);
                    location.Margin=new Thickness(0,5,0,0);text.Children.Add(location);row.Children.Add(text);
                    Button install=null;
                    bool exists=Directory.Exists(Path.Combine(ExtensionsDirectory,plugin.Id));
                    install=Pulsante(exists ? "Installata" : "Installa",()=>{ _=Install(plugin,install); },primary:!exists,icon:exists ? "check" : "plus");
                    install.IsEnabled=!exists;install.VerticalAlignment=VerticalAlignment.Center;
                    Grid.SetColumn(install,1);row.Children.Add(install);
                    results.Children.Add(new Border { BorderBrush=Contorno,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(16),Child=row,Margin=new Thickness(0,0,0,10) });
                }
                if(download.Errors.Count > 0)
                {
                    var warning=Tema.Scritta("Manifest non caricati:\n"+string.Join("\n",download.Errors),12,Tema.BiancoTenue);
                    warning.TextWrapping=TextWrapping.Wrap;results.Children.Add(warning);
                }
            }
            catch(OperationCanceledException) { if(!departed) Status("Ricerca annullata."); }
            catch(Exception ex) { if(!departed) Status(ex is TaskCanceledException ? "Download scaduto. Riprova." : ex.GetBaseException().Message,true); }
            finally { Finished(); }
        }
        async Task Update(string id,GitHubExtensionSource source)
        {
            if(busy) return;
            SetBusy(true);
            try
            {
                using var latest=await github.DownloadAsync(source.Repository,source.Reference,lifetime.Token,new Progress<string>(message=>{if(!departed)Status(message);}));
                var plugin=latest.Plugins.SingleOrDefault(p=>p.Id == id && Path.GetRelativePath(latest.RepositoryDirectory,p.Directory).Replace('\\','/') == source.Subdirectory);
                if(plugin == null) throw new Exception("Il plugin non è più presente nello stesso percorso del repository.");
                if(source.Commit != latest.Source.Commit && !Trust(plugin)) { Status("Aggiornamento annullato.");return; }
                string message=await Task.Run(()=>github.Update(latest,id,lifetime.Token));
                if(!departed) {Status(message);RefreshLinked();}
            }
            catch(OperationCanceledException) { if(!departed) Status("Aggiornamento annullato o scaduto."); }
            catch(Exception ex) { if(!departed) Status(ex.GetBaseException().Message,true); }
            finally {Finished();}
        }
        void RefreshLinked()
        {
            linked.Children.Clear();
            var ids=Directory.Exists(ExtensionsDirectory) ? Directory.GetDirectories(ExtensionsDirectory).Select(Path.GetFileName).OrderBy(x=>x).ToList() : new System.Collections.Generic.List<string>();
            bool heading=false;
            foreach(string id in ids)
            {
                try
                {
                    var source=github.ReadSource(id);
                    if(source == null) continue;
                    var manifest=LeggiManifest(Path.Combine(ExtensionsDirectory,id));
                    if(!heading) {linked.Children.Add(Tema.Scritta("Repository collegati",18,Tema.Bianco,FontWeights.SemiBold));heading=true;}
                    var row=new Grid { Margin=new Thickness(0,14,0,0) };
                    row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                    var details=new StackPanel { Margin=new Thickness(0,0,16,0) };
                    details.Children.Add(Tema.Scritta(manifest.Name+"  ·  "+manifest.Version,14,Tema.Bianco,FontWeights.SemiBold));
                    var origin=Tema.Scritta(source.Repository+" · "+(source.Reference == "HEAD" ? "branch predefinito" : source.Reference)+" · "+(source.Commit?.Length >= 7 ? source.Commit[..7] : ""),12,Tema.BiancoTenue);
                    origin.TextWrapping=TextWrapping.Wrap;origin.Margin=new Thickness(0,5,0,0);details.Children.Add(origin);row.Children.Add(details);
                    bool pending=github.HasPending(id);
                    var update=Pulsante(pending ? "Pronto al riavvio" : "Aggiorna",()=>{_=Update(id,source);},icon:pending ? "check" : "refresh");
                    update.IsEnabled=!pending;Grid.SetColumn(update,1);row.Children.Add(update);linked.Children.Add(row);
                }
                catch(Exception ex) { var warning=Tema.Scritta(id+": "+ex.Message,12,Tema.BiancoTenue);warning.TextWrapping=TextWrapping.Wrap;linked.Children.Add(warning); }
            }
        }
        search=Pulsante("Trova plugin",()=>{_=Search();},primary:true,icon:"github");
        search.HorizontalAlignment=HorizontalAlignment.Left;search.Margin=new Thickness(0,16,0,0);form.Children.Add(search);
        form.Children.Add(status);
        body.Children.Add(new Border { Background=Superficie,BorderBrush=Contorno,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Child=form });
        body.Children.Add(results);
        var localRow=new DockPanel { Margin=new Thickness(0,22,0,0) };
        local=Pulsante("Carica da cartella",ImportaEstensione,icon:"folder");
        DockPanel.SetDock(local,Dock.Right);localRow.Children.Add(local);
        localRow.Children.Add(Tema.Scritta("Hai già il plugin sul computer?",13,Tema.BiancoTenue));
        body.Children.Add(localRow);body.Children.Add(linked);
        RefreshLinked();
        page.Children.Add(Scorri(body));
        page.Unloaded += (_,_)=>{ departed=true;lifetime.Cancel();if(!busy){download?.Dispose();download=null;lifetime.Dispose();} };
        lavoro.Content=page;
    }
}
