using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using BarraVascend;

namespace VascendEngine;

// API pubblica: una community extension può fornire qualsiasi pagina WPF.
public interface IEngineExtension
{
    FrameworkElement CreatePage(ExtensionContext context);
}

public sealed class ExtensionContext
{
    public string ExtensionDirectory { get; }
    public string SettingsPath { get; }
    public string Language => I18n.Language;
    public System.Threading.CancellationToken ActivationToken { get; internal set; }
    public ExtensionContext(string directory, string settingsPath) { ExtensionDirectory = directory; SettingsPath = settingsPath; }
    public Dictionary<string, JsonElement> LoadSettings() => File.Exists(SettingsPath)
        ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(SettingsPath)) ?? new() : new();
    public void SaveSettings(Dictionary<string, JsonElement> settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        var tmp = SettingsPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, SettingsPath, true);
    }
}

sealed class ExtensionManifest
{
    public int ApiVersion { get; set; } = 1;
    public string Id { get; set; }
    public string Name { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; }
    public string Description { get; set; }
    public string EntryAssembly { get; set; }
    public string EntryType { get; set; }
    public List<ExtensionSetting> Settings { get; set; } = new();
    public List<string> ConflictsWith { get; set; } = new();
    public List<string> ExclusiveResources { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore] public string Directory { get; set; }
}
sealed class ExtensionSetting
{
    public string Key { get; set; }
    public string Label { get; set; }
    public string Type { get; set; } = "string";
    public JsonElement Default { get; set; }
}
sealed class ExtensionLoadContext : AssemblyLoadContext
{
    readonly AssemblyDependencyResolver resolver;
    public ExtensionLoadContext(string entry) : base(false) { resolver = new(entry); }
    protected override Assembly Load(AssemblyName name)
    {
        if (name.Name == typeof(IEngineExtension).Assembly.GetName().Name) return typeof(IEngineExtension).Assembly;
        var path = resolver.ResolveAssemblyToPath(name);
        return path == null ? null : LoadFromAssemblyPath(path);
    }
    protected override IntPtr LoadUnmanagedDll(string name)
    {
        var path = resolver.ResolveUnmanagedDllToPath(name);
        return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}

partial class Finestra
{
    static readonly JsonSerializerOptions ManifestJson = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    static string ExtensionsDirectory => Path.Combine(Stile.Cartella, "extensions");
    readonly Dictionary<string, IEngineExtension> plugins = new();

    static readonly System.Windows.Media.Brush Superficie = Tema.Pennello(27, 29, 24, 1);
    static readonly System.Windows.Media.Brush Contorno = Tema.Pennello(211, 218, 190, .14);

    static FrameworkElement EtichettaAzione(string text, string icon, System.Windows.Media.Brush color, double fontSize = 13, bool trailing = false, bool bold = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var label = Tema.Scritta(text, fontSize, color, bold ? FontWeights.SemiBold : FontWeights.Normal);
        if (icon == null) return label;
        var symbol = IconeEngine.Create(icon,16,color);
        symbol.Margin = trailing ? new Thickness(8,0,0,0) : new Thickness(0,0,8,0);
        if (trailing) { row.Children.Add(label); row.Children.Add(symbol); }
        else { row.Children.Add(symbol); row.Children.Add(label); }
        return row;
    }

    Button Pulsante(string text, Action action, bool active = false, bool primary = false, string icon = null)
    {
        if (primary && icon == null) icon = text.StartsWith("Salva") ? "save" : "check";
        var label = EtichettaAzione(text,icon,primary ? Tema.Pennello(19,20,18,1) : Tema.Bianco,bold: active || primary);
        var button = Bottone(label,action,active,primary);
        System.Windows.Automation.AutomationProperties.SetName(button,text);
        return button;
    }

    FrameworkElement StrumentiEstensioni()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        var refresh = Bottone(IconeEngine.Create("refresh"),Catalogo,radius:new CornerRadius(7,0,0,7));
        refresh.Padding = new Thickness(11);
        refresh.ToolTip = Suggerimento("Aggiorna le estensioni");
        System.Windows.Automation.AutomationProperties.SetName(refresh,"Aggiorna le estensioni");
        var folder = Bottone(EtichettaAzione("Cartella", "folder", Tema.Bianco),() => {
            Directory.CreateDirectory(ExtensionsDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", ExtensionsDirectory) { UseShellExecute = true });
        },radius:new CornerRadius(0,7,7,0));
        folder.Padding = new Thickness(12,10,14,10);
        folder.ToolTip = Suggerimento("Apri la cartella delle estensioni");
        System.Windows.Automation.AutomationProperties.SetName(folder,"Apri la cartella delle estensioni");
        foreach (var button in new[] {refresh,folder})
        {
            button.Margin = new Thickness(0);
            button.Background = System.Windows.Media.Brushes.Transparent;
            button.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }
        row.Children.Add(refresh);
        row.Children.Add(new Border { Width=1,Height=18,Background=Contorno,VerticalAlignment=VerticalAlignment.Center });
        row.Children.Add(folder);
        return new Border { CornerRadius=new CornerRadius(8),BorderBrush=Contorno,BorderThickness=new Thickness(1),
            Background=Tema.Pennello(211,218,190,.035),Child=row,Margin=new Thickness(0,0,8,0) };
    }

    Button Bottone(UIElement content, Action action, bool active = false, bool primary = false, CornerRadius? radius = null)
    {
        var button = new Button { Content = content, Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(16, 10, 16, 10), Margin = new Thickness(0, 0, 8, 0),
            Background = primary ? Tema.LimePennello : active ? Tema.Pennello(211, 218, 190, .14) : Tema.Pennello(211, 218, 190, .045),
            BorderBrush = active ? Tema.LimePennello : Contorno, BorderThickness = new Thickness(1),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            FocusVisualStyle = null };
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "Surface";
        border.SetValue(Border.CornerRadiusProperty, radius ?? new CornerRadius(8));
        border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        border.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new System.Windows.Data.Binding("HorizontalContentAlignment"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Border.BorderBrushProperty, primary ? Tema.Bianco : Tema.Pennello(211, 218, 190, .42), "Surface"));

        template.Triggers.Add(hover);
        var focus = new Trigger { Property = IsKeyboardFocusedProperty, Value = true };
        focus.Setters.Add(new Setter(Border.BorderBrushProperty, Tema.LimePennello, "Surface"));
        template.Triggers.Add(focus);
        var down = new Trigger { Property = Button.IsPressedProperty, Value = true };
        down.Setters.Add(new Setter(OpacityProperty, .7, "Surface"));
        template.Triggers.Add(down);
        button.Template = template;
        MorphEngine.Setup(button,radius ?? new CornerRadius(8),primary);
        button.Click += (_, e) => {if(ReferenceEquals(e.Source,button))action();};
        return button;
    }

    static Image Logo(double size) => new() { Width = size, Height = size,
        Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/VascendEngine;component/engine.png")) };

    FrameworkElement NavigazioneGhiaccio(string current)
    {
        var root = new StackPanel { Margin = new Thickness(0, 4, 0, 22) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 22) };
        var back = Pulsante("Estensioni", Catalogo, icon: "arrow-left");
        back.HorizontalAlignment = HorizontalAlignment.Left;
        DockPanel.SetDock(back, Dock.Left);
        header.Children.Add(back);
        var name = Tema.Scritta("Vascend Technologies", 20, Tema.Bianco, FontWeights.SemiBold);
        name.Margin = new Thickness(14, 0, 0, 0);
        header.Children.Add(name);
        root.Children.Add(header);
        var tabs = new StackPanel {Orientation=Orientation.Horizontal};
        var entries=Sezioni.ToArray();
        for(int index=0;index<entries.Length;index++)
        {
            var entry=entries[index];
            string glyph=entry.nome switch {
                "Stile"=>"palette","Font"=>"code","Barra"=>"layout","Isola"=>"island","Widget"=>"grid",
                "Windows"=>"window","Terminali"=>"terminal",_=>"code"
            };
            bool selected=entry.nome==current;
            var label=EtichettaAzione(entry.nome,glyph,selected ? Tema.Bianco : Tema.BiancoTenue,12.5,bold:selected);
            var tab=Bottone(label,()=>Apri(entry.nome),selected,radius:new CornerRadius(0));
            tab.Padding=new Thickness(12,10,12,10);tab.Margin=new Thickness(0);
            tab.BorderBrush=System.Windows.Media.Brushes.Transparent;
            tab.Background=selected ? Tema.Pennello(211,230,93,.11) : System.Windows.Media.Brushes.Transparent;
            var segment=new Grid();segment.Children.Add(tab);
            if(selected)segment.Children.Add(new Border {Height=2,Background=Tema.LimePennello,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(12,0,12,0),IsHitTestVisible=false});
            if(index>0)tabs.Children.Add(new Border {Width=1,Height=20,Background=Contorno,VerticalAlignment=VerticalAlignment.Center});
            tabs.Children.Add(segment);
        }
        var surface=new Border {CornerRadius=new CornerRadius(9),BorderBrush=Contorno,BorderThickness=new Thickness(1),Background=Tema.Pennello(211,218,190,.025),Child=tabs,HorizontalAlignment=HorizontalAlignment.Left};
        surface.SizeChanged+=(_,_)=>{var clip=new System.Windows.Media.RectangleGeometry(new Rect(surface.RenderSize),9,9);clip.Freeze();surface.Clip=clip;};
        root.Children.Add(new ScrollViewer {Content=surface,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false});
        return root;
    }

    void ResetPagina(string heading)
    {
        fasciaAzioni.Visibility = Visibility.Visible;
        titolo.Text = heading;
        esito.Text = "";
        piede.Content = null;
        azioneApplica = null;
    }

    string filtroCatalogo="Tutte",ricercaCatalogo="";
    string ordineCatalogo="Nome A–Z";
    void Catalogo()
    {
        ResetPagina("Vascend Engine");fasciaAzioni.Visibility=Visibility.Collapsed;
        var panel=new StackPanel {Margin=new Thickness(6,16,6,0)};
        panel.Children.Add(Tema.Scritta("IL TUO WORKSPACE",10.5,Tema.LimePennello,FontWeights.SemiBold));
        var heading=Tema.Scritta("Estensioni",32,Tema.Bianco,FontWeights.SemiBold);
        heading.Margin=new Thickness(0,8,0,22);panel.Children.Add(heading);
        var toolbar=new Grid {Margin=new Thickness(0,0,0,20)};
        toolbar.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        toolbar.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        toolbar.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
        var filters=Pulsante(filtroCatalogo,()=>{},icon:"filter");
        filters.MinWidth=100;
        var menu=MenuScuro();
        filters.Click += (_,_)=>{menu.PlacementTarget=filters;menu.IsOpen=true;};
        toolbar.Children.Add(filters);
        var input=CampoRepository(ricercaCatalogo);input.MinWidth=120;input.Padding=new Thickness(0,9,8,9);
        input.BorderThickness=new Thickness(0);input.Background=System.Windows.Media.Brushes.Transparent;
        System.Windows.Automation.AutomationProperties.SetName(input,"Cerca estensioni");
        var searchGrid=new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(36)});searchGrid.ColumnDefinitions.Add(new ColumnDefinition());
        searchGrid.Children.Add(IconeEngine.Create("search",16,Tema.BiancoTenue));Grid.SetColumn(input,1);searchGrid.Children.Add(input);
        var placeholder=Tema.Scritta("Cerca estensioni…",13,Tema.BiancoTenue);placeholder.IsHitTestVisible=false;
        placeholder.Visibility=input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        Grid.SetColumn(placeholder,1);searchGrid.Children.Add(placeholder);
        var searchBorder=new Border {CornerRadius=new CornerRadius(8),Background=Superficie,BorderBrush=Contorno,BorderThickness=new Thickness(1),Child=searchGrid,Margin=new Thickness(4,0,10,0)};
        Grid.SetColumn(searchBorder,1);toolbar.Children.Add(searchBorder);
        var tools=StrumentiEstensioni();Grid.SetColumn(tools,2);toolbar.Children.Add(tools);
        var sort=Bottone(IconeEngine.Create("sort"),()=>{});sort.Padding=new Thickness(11);sort.ToolTip=Suggerimento("Ordina: "+ordineCatalogo);
        var sortMenu=MenuScuro();sort.ContextMenu=sortMenu;
        System.Windows.Automation.AutomationProperties.SetName(sort,"Ordina estensioni");Grid.SetColumn(sort,3);toolbar.Children.Add(sort);
        var add=Pulsante("Aggiungi",()=>{},primary:true,icon:"plus");
        var addMenu=MenuScuro();
        var githubItem=new MenuItem {Header="Da repository GitHub",Icon=IconeEngine.Create("github")};githubItem.Click += (_,_)=>ApriAggiungiEstensione();
        var localItem=new MenuItem {Header="Da cartella locale",Icon=IconeEngine.Create("folder")};localItem.Click += (_,_)=>ImportaEstensione();
        addMenu.Items.Add(githubItem);addMenu.Items.Add(localItem);
        add.Click += (_,_)=>{addMenu.PlacementTarget=add;addMenu.IsOpen=true;};Grid.SetColumn(add,4);toolbar.Children.Add(add);
        panel.Children.Add(toolbar);
        var summary=Tema.Scritta("",13,Tema.BiancoTenue);summary.Margin=new Thickness(0,0,0,14);panel.Children.Add(summary);
        var cards=new WrapPanel();
        var errors=new System.Collections.Generic.List<string>();
        var installed=new System.Collections.Generic.List<ExtensionManifest>{ExtensionActivation.Builtin()};
        Directory.CreateDirectory(ExtensionsDirectory);
        foreach(string directory in Directory.GetDirectories(ExtensionsDirectory))
        {
            try{installed.Add(LeggiManifest(directory));}catch(Exception ex){errors.Add(Path.GetFileName(directory)+": "+ex.Message);}
        }
        void ResizeCards()
        {
            double width=Math.Max(320,cards.ActualWidth);
            int columns=Math.Max(1,(int)Math.Floor(width/390));
            double cellWidth=Math.Floor(width/columns)-12;
            foreach(FrameworkElement card in cards.Children) card.Width=cellWidth;
        }
        void RenderCards()
        {
            cards.Children.Clear();int count=0;
            var query=ricercaCatalogo.Trim();
            var sorted=ordineCatalogo switch {
                "Nome Z–A"=>installed.OrderByDescending(p=>p.Name,StringComparer.OrdinalIgnoreCase),
                "Attive prima"=>installed.OrderByDescending(p=>activation.Enabled(p.Id)).ThenBy(p=>p.Name,StringComparer.OrdinalIgnoreCase),
                "Più recenti"=>installed.OrderByDescending(p=>p.Directory == null ? DateTime.MinValue : Directory.GetCreationTimeUtc(p.Directory)).ThenBy(p=>p.Name,StringComparer.OrdinalIgnoreCase),
                _=>installed.OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase)
            };
            foreach(var plugin in sorted)
            {
                bool enabled=activation.Enabled(plugin.Id);
                var issues=activation.Issues(plugin,installed);
                if(query.Length > 0 && !(plugin.Name+" "+plugin.Description+" "+plugin.Author+" "+plugin.Id).Contains(query,StringComparison.OrdinalIgnoreCase))continue;
                if(filtroCatalogo == "Attive" && !enabled || filtroCatalogo == "Disattivate" && enabled || filtroCatalogo == "Conflitti" && issues.Count == 0)continue;
                bool builtin=plugin.Id == ExtensionState.BuiltinId;
                cards.Children.Add(SchedaOrizzontale(plugin,enabled,issues,builtin));count++;
            }
            summary.Text=count+" estension"+(count == 1 ? "e" : "i")+" · "+filtroCatalogo.ToLowerInvariant()+" · "+ordineCatalogo;
            if(count == 0)
            {
                var empty=Tema.Scritta("Nessuna estensione corrisponde alla ricerca.",14,Tema.BiancoTenue);empty.Margin=new Thickness(0,20,0,20);cards.Children.Add(empty);
            }
            ResizeCards();
        }
        foreach(string label in new[]{"Tutte","Attive","Disattivate","Conflitti"})
        {
            var item=new MenuItem {Header=label,IsCheckable=true,IsChecked=label == filtroCatalogo};
            item.Click += (_,_)=>{filtroCatalogo=label;filters.Content=EtichettaAzione(label,"filter",Tema.Bianco);foreach(MenuItem other in menu.Items)other.IsChecked=other == item;RenderCards();};
            menu.Items.Add(item);
        }
        input.TextChanged += (_,_)=>{ricercaCatalogo=input.Text;placeholder.Visibility=input.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;RenderCards();};
        foreach(string label in new[]{"Nome A–Z","Nome Z–A","Attive prima","Più recenti"})
        {
            var item=new MenuItem {Header=label,IsCheckable=true,IsChecked=label == ordineCatalogo};
            item.Click+=(_,_)=>{ordineCatalogo=label;sort.ToolTip=Suggerimento("Ordina: "+label);foreach(MenuItem other in sortMenu.Items)other.IsChecked=other == item;RenderCards();};
            sortMenu.Items.Add(item);
        }
        sort.Click+=(_,_)=>{sortMenu.PlacementTarget=sort;sortMenu.IsOpen=true;};
        cards.SizeChanged += (_,_)=>ResizeCards();cards.Loaded += (_,_)=>ResizeCards();
        RenderCards();panel.Children.Add(cards);
        if(errors.Count > 0){var warning=Tema.Scritta("Estensioni da verificare:\n"+string.Join("\n",errors),12,Tema.BiancoTenue);warning.TextWrapping=TextWrapping.Wrap;panel.Children.Add(warning);}
        var catalogo=new Grid();catalogo.RowDefinitions.Add(new RowDefinition());catalogo.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
        catalogo.Children.Add(Scorri(panel));var story=new StoryArt {Margin=new Thickness(-26,18,-22,0)};Grid.SetRow(story,1);catalogo.Children.Add(story);lavoro.Content=catalogo;
    }
    FrameworkElement SchedaOrizzontale(ExtensionManifest plugin,bool enabled,System.Collections.Generic.List<string> issues,bool builtin)
    {
        var layout=new Grid {Height=116};
        layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(72)});
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(88)});
        var icon=new Border {Width=48,Height=48,CornerRadius=new CornerRadius(10),Background=Tema.Pennello(14,16,13,1),HorizontalAlignment=HorizontalAlignment.Left,
            Child=builtin ? Logo(32) : IconeEngine.Create("extension",24,Tema.Bianco)};
        layout.Children.Add(icon);
        var details=new StackPanel {VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)};
        var name=Tema.ScrittaUtente(plugin.Name,14,Tema.Bianco,FontWeights.SemiBold);name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=plugin.Name;
        if(builtin)
        {
            var titleRow=new DockPanel {HorizontalAlignment=HorizontalAlignment.Left};
            var verified=IconeEngine.Verified();verified.Margin=new Thickness(6,0,0,0);DockPanel.SetDock(verified,Dock.Right);titleRow.Children.Add(verified);titleRow.Children.Add(name);details.Children.Add(titleRow);
        }
        else details.Children.Add(name);
        var description=builtin ? Tema.Scritta(plugin.Description,12,Tema.BiancoTenue) : Tema.ScrittaUtente(plugin.Description ?? I18n.Translate("Estensione community"),12,Tema.BiancoTenue);description.TextWrapping=TextWrapping.Wrap;description.TextTrimming=TextTrimming.CharacterEllipsis;description.LineHeight=17;description.MaxHeight=34;description.Margin=new Thickness(0,5,0,5);description.ToolTip=plugin.Description;details.Children.Add(description);
        string origin=builtin ? "Vascend · Integrata" : (plugin.Author ?? "Community")+" · "+plugin.Version;
        var status=Tema.Scritta(issues.Count > 0 ? "Conflitto · "+issues.Count+" incompatibilità" : enabled ? origin : "Disattivata · "+origin,11,issues.Count > 0 ? Tema.Pennello(239,169,119,1) : Tema.BiancoTenue);
        status.TextTrimming=TextTrimming.CharacterEllipsis;status.ToolTip=issues.Count > 0 ? string.Join("\n",issues) : origin;details.Children.Add(status);Grid.SetColumn(details,1);layout.Children.Add(details);
        var track=new Border {Width=30,Height=16,CornerRadius=new CornerRadius(8),Background=enabled ? Tema.LimePennello : Tema.Pennello(255,255,255,.16),
            Child=new Border {Width=12,Height=12,CornerRadius=new CornerRadius(6),Background=enabled ? Tema.Pennello(19,20,18,1) : Tema.BiancoTenue,Margin=new Thickness(2),HorizontalAlignment=enabled ? HorizontalAlignment.Right : HorizontalAlignment.Left}};
        var toggleContent=new StackPanel {Orientation=Orientation.Horizontal};toggleContent.Children.Add(track);
        var stateLabel=Tema.Scritta(enabled ? "On" : "Off",10.5,Tema.BiancoTenue);stateLabel.Margin=new Thickness(6,0,0,0);toggleContent.Children.Add(stateLabel);
        var toggle=Bottone(toggleContent,()=>ToggleExtension(plugin.Id));toggle.Padding=new Thickness(7,6,7,6);toggle.Margin=new Thickness(0,0,14,0);toggle.HorizontalAlignment=HorizontalAlignment.Right;toggle.VerticalAlignment=VerticalAlignment.Center;
        toggle.ToolTip=Suggerimento(enabled ? "Disattiva "+plugin.Name : issues.Count > 0 ? string.Join("\n",issues) : "Attiva "+plugin.Name);
        System.Windows.Automation.AutomationProperties.SetName(toggle,(enabled ? "Disattiva " : "Attiva ")+plugin.Name);
        var openCard=Bottone(layout,()=>{if(builtin)Apri("Stile");else ApriEstensione(plugin);},radius:new CornerRadius(11));
        openCard.Tag="extension-card";openCard.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        openCard.Background=Superficie;openCard.BorderBrush=issues.Count > 0 ? Tema.Pennello(239,169,119,.4) : Contorno;
        openCard.Padding=new Thickness(12,0,14,0);openCard.Margin=new Thickness(0);
        System.Windows.Automation.AutomationProperties.SetName(openCard,"Apri "+plugin.Name);
        var card=new Grid {Margin=new Thickness(0,0,12,12),VerticalAlignment=VerticalAlignment.Top};
        card.Children.Add(openCard);card.Children.Add(toggle);
        return card;
    }


    internal static ExtensionManifest LeggiManifest(string directory)
    {
        var manifest = JsonSerializer.Deserialize<ExtensionManifest>(File.ReadAllText(Path.Combine(directory, "extension.json")), ManifestJson)
            ?? throw new Exception("Manifest vuoto.");
        if (manifest.ApiVersion != 1) throw new Exception("Versione API non supportata.");
        if (manifest.Id == null || !Regex.IsMatch(manifest.Id, @"^[a-z0-9][a-z0-9._-]{0,79}$") || manifest.Id == "vascend-ghiaccio" || manifest.Id == ExtensionState.BuiltinId) throw new Exception("ID non valido o riservato.");
        if (string.IsNullOrWhiteSpace(manifest.Name)) throw new Exception("Nome mancante.");
        manifest.ConflictsWith ??= new();manifest.ExclusiveResources ??= new();
        foreach(string id in manifest.ConflictsWith)
            if(id == null || !Regex.IsMatch(id,@"^[a-z0-9][a-z0-9._-]{0,79}$") || id == manifest.Id) throw new Exception("ID incompatibilità non valido.");
        foreach(string resource in manifest.ExclusiveResources)
            if(resource == null || !Regex.IsMatch(resource,@"^[a-z0-9][a-z0-9._-]{0,119}$")) throw new Exception("Risorsa esclusiva non valida.");
        manifest.Settings ??= new();
        var keys = new HashSet<string>();
        foreach (var setting in manifest.Settings)
        {
            if (setting == null || string.IsNullOrWhiteSpace(setting.Key) || !keys.Add(setting.Key)) throw new Exception("Chiave impostazione mancante o duplicata.");
            if (setting.Type != "string" && setting.Type != "boolean" && setting.Type != "number") throw new Exception("Tipo impostazione non supportato: " + setting.Type);
            if (setting.Default.ValueKind != JsonValueKind.Undefined &&
                !(setting.Type == "string" && setting.Default.ValueKind == JsonValueKind.String ||
                  setting.Type == "boolean" && (setting.Default.ValueKind == JsonValueKind.True || setting.Default.ValueKind == JsonValueKind.False) ||
                  setting.Type == "number" && setting.Default.ValueKind == JsonValueKind.Number)) throw new Exception("Valore predefinito non valido: " + setting.Key);
        }
        manifest.Directory = Path.GetFullPath(directory);
        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var entry = Path.GetFullPath(Path.Combine(directory, manifest.EntryAssembly));
            if (!entry.StartsWith(manifest.Directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(entry) || Path.GetExtension(entry) != ".dll") throw new Exception("Assembly non valido o fuori dalla cartella.");
            if (string.IsNullOrWhiteSpace(manifest.EntryType)) throw new Exception("EntryType mancante.");
        }
        return manifest;
    }

    void ImportaEstensione()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title=I18n.Translate("Scegli la cartella contenente extension.json") };
        if (dialog.ShowDialog(this) != true) return;
        string temporary = null;
        try
        {
            var extension = LeggiManifest(dialog.FolderName);
            var destination = Path.Combine(ExtensionsDirectory, extension.Id);
            if (Directory.Exists(destination)) throw new Exception("Questa estensione è già installata. Usa un ID diverso per una nuova estensione.");
            if (!string.IsNullOrWhiteSpace(extension.EntryAssembly) && I18n.ShowMessage(this,
                $"{extension.Name} contiene codice che verrà eseguito sul tuo computer quando apri la sua pagina.\n\nCaricala solo se ti fidi dell’autore.", "Carica estensione", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            Directory.CreateDirectory(ExtensionsDirectory);
            temporary = Path.Combine(Stile.Cartella, "extension-import-" + Guid.NewGuid().ToString("N"));
            CopiaEstensione(extension.Directory, temporary);
            LeggiManifest(temporary);
            ExtensionState.Set(extension.Id,false);
            Directory.Move(temporary, destination);
            temporary = null;
            Catalogo();
            Esito(extension.Name + " caricata e disattivata. Puoi attivarla dalla home.");
        }
        catch (Exception ex) { Esito("Caricamento non riuscito: " + ex.Message, true); }
        finally { if (temporary != null && Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }

    static void CopiaEstensione(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new Exception("I collegamenti nelle estensioni non sono supportati.");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new Exception("I collegamenti nelle estensioni non sono supportati.");
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
        foreach (var directory in Directory.GetDirectories(source)) CopiaEstensione(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    void ApriEstensione(ExtensionManifest extension)
    {
        bool readOnly=SolaLettura(extension);
        ResetPagina(extension.Name);
        var page = new DockPanel();
        var back = Pulsante("Estensioni", Catalogo, icon: "arrow-left");
        DockPanel.SetDock(back, Dock.Top);
        page.Children.Add(back);
        if(readOnly){var notice=AvvisoSolaLettura(extension);DockPanel.SetDock(notice,Dock.Top);page.Children.Add(notice);}
        lavoro.Content = page;
        try
        {
            var context = new ExtensionContext(extension.Directory, Path.Combine(Stile.Cartella, "extension-settings", extension.Id + ".json"));
            if (!readOnly && !string.IsNullOrWhiteSpace(extension.EntryAssembly))
            {
                if (!plugins.TryGetValue(extension.Id, out var plugin))
                {
                    var entry = Path.GetFullPath(Path.Combine(extension.Directory, extension.EntryAssembly));
                    var assembly = new ExtensionLoadContext(entry).LoadFromAssemblyPath(entry);
                    var type = assembly.GetType(extension.EntryType, true);
                    plugin = Activator.CreateInstance(type) as IEngineExtension ?? throw new Exception("Il tipo deve implementare IEngineExtension.");
                    var token=new System.Threading.CancellationTokenSource();
                    context.ActivationToken=token.Token;
                    try {if(plugin is IEngineExtensionLifecycle lifecycle)lifecycle.OnActivated(context);}
                    catch {token.Cancel();token.Dispose();throw;}
                    pluginTokens.Add(extension.Id,token);
                    plugins.Add(extension.Id, plugin);
                }
                if(pluginTokens.TryGetValue(extension.Id,out var activeToken))context.ActivationToken=activeToken.Token;
                page.Children.Add(plugin.CreatePage(context) ?? throw new Exception("L’estensione non ha restituito una pagina."));
                return;
            }
            var content = new StackPanel();
            content.Children.Add(Tema.ScrittaUtente(extension.Name, 26, Tema.Bianco, FontWeights.SemiBold));
            var description = Tema.ScrittaUtente(extension.Description ?? I18n.Translate("Impostazioni dell’estensione"), 14, Tema.BiancoTenue);
            description.TextWrapping = TextWrapping.Wrap;
            description.Margin = new Thickness(0, 10, 0, 24);
            content.Children.Add(description);
            var values = context.LoadSettings();
            var readers = new Dictionary<string, Func<JsonElement>>();
            foreach (var setting in extension.Settings)
            {
                var value = values.TryGetValue(setting.Key, out var saved) ? saved : setting.Default;
                var label = Tema.ScrittaUtente(setting.Label ?? setting.Key, 14, Tema.Bianco);
                label.Margin = new Thickness(0, 12, 0, 8);
                content.Children.Add(label);
                if (setting.Type == "boolean")
                {
                    var check = new CheckBox { Content = Tema.Scritta("Attiva"), Foreground = Tema.Bianco, IsChecked = value.ValueKind == JsonValueKind.True };
                    content.Children.Add(check);
                    readers[setting.Key] = () => JsonSerializer.SerializeToElement(check.IsChecked == true);
                }
                else
                {
                    var input = new TextBox { Text = value.ValueKind == JsonValueKind.Undefined ? "" : value.ToString(), MaxWidth = 560,
                        HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 320, Padding = new Thickness(10, 8, 10, 8), FontSize = 14 };
                    content.Children.Add(input);
                    readers[setting.Key] = () => setting.Type == "number"
                        ? JsonSerializer.SerializeToElement(double.Parse(input.Text, System.Globalization.CultureInfo.InvariantCulture))
                        : JsonSerializer.SerializeToElement(input.Text);
                }
            }
            if (extension.Settings.Count == 0) content.Children.Add(Tema.Scritta("Questa estensione non espone impostazioni.", 14, Tema.BiancoTenue));
            else if(!readOnly) Piede(() => {
                try
                {
                    var updated = new Dictionary<string, JsonElement>(values);
                    foreach (var pair in readers) updated[pair.Key] = pair.Value();
                    context.SaveSettings(updated);
                    Esito("Impostazioni salvate.");
                }
                catch (Exception ex) { Esito("Salvataggio non riuscito: " + ex.Message, true); }
            }, "Salva impostazioni");
            if(readOnly)
            {
                // Una DLL disattivata non viene caricata: l'anteprima usa le impostazioni del manifest.
                if(extension.Settings.Count == 0 && !string.IsNullOrWhiteSpace(extension.EntryAssembly))
                {
                    var help=Tema.Scritta("Questa DLL non dichiara impostazioni nel manifest. La pagina personalizzata è disponibile dopo l'attivazione.",13,Tema.BiancoTenue);help.TextWrapping=TextWrapping.Wrap;content.Children.Add(help);
                }
                page.Children.Add(new OpzioniSolaLettura(Scorri(content)));azioneApplica=null;piede.Content=null;
            }
            else page.Children.Add(Scorri(content));
        }
        catch (Exception ex)
        {
            var error = Tema.Scritta("Impossibile aprire l’estensione: " + ex.GetBaseException().Message, 14, Tema.BiancoTenue);
            error.TextWrapping = TextWrapping.Wrap;
            page.Children.Add(error);
        }
    }
}
