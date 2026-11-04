using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VascendEngine;

static class Program
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static IEnumerable<DependencyObject> Nodes(DependencyObject node)
    {
        yield return node;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in Nodes(VisualTreeHelper.GetChild(node,i)))yield return child;
    }
    static void Pump()=>Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));
    static RenderTargetBitmap Render(FrameworkElement root)
    {
        root.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(root);return image;
    }
    static void Save(FrameworkElement root,string directory,string name)
    {
        if(directory==null)return;
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(320)};
        timer.Tick+=(_,_)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
        Directory.CreateDirectory(directory);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(Render(root)));using var stream=File.Create(Path.Combine(directory,name));png.Save(stream);
    }
    [STAThread] static void Main(string[] args)
    {
        string temp=Path.Combine(Path.GetTempPath(),"vascend-smoke-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        string preferences=Path.Combine(temp,"preferences.json");File.WriteAllText(preferences,"{\"keep\":true}");
        File.WriteAllText(Path.Combine(temp,"extension-state.json"),"{\"vascend-technologies\":false}");
        Environment.SetEnvironmentVariable("VASCEND_ENGINE_PREFERENCES",preferences);
        var assembly=typeof(IEngineExtension).Assembly;var language=assembly.GetType("VascendEngine.I18n");
        void SetLanguage(string value)=>language.GetMethod("SetLanguage").Invoke(null,new object[]{value});
        Check((string)language.GetProperty("Language").GetValue(null)=="en","English is the default regardless of system language");
        SetLanguage("it");
        var app=new Application();var type=assembly.GetType("VascendEngine.Finestra");
        var window=(Window)Activator.CreateInstance(type,new object[]{null});
        var activation=Activator.CreateInstance(assembly.GetType("VascendEngine.ExtensionActivation"),new object[]{temp});
        type.GetField("activation",Private).SetValue(window,activation);
        type.GetMethod("Catalogo",Private).Invoke(window,null);
        window.ShowActivated=false;window.ShowInTaskbar=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-10000;window.Top=-10000;
        window.Width=1280;window.Height=800;
        string screenshots=args.Length==2 && args[0]=="--screenshots" ? args[1] : null;
        try
        {
            window.Show();Pump();var root=(FrameworkElement)window.Content;
            bool Has(string value)=>Nodes(root).OfType<TextBlock>().Any(label=>label.Text==value);
            Check(Has("Estensioni"),"Italian catalog");
            var englishButton=Nodes(root).OfType<Button>().Single(button=>System.Windows.Automation.AutomationProperties.GetName(button)=="English");
            var flag=Nodes(root).OfType<Image>().Single(image=>System.Windows.Automation.AutomationProperties.GetName(image)=="Language flag");var previousFlag=flag.Source;
            englishButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check(!ReferenceEquals(previousFlag,flag.Source) && flag.Width>=26,"Visible flag changes with selected language");
            var indicator=Nodes(root).OfType<Border>().Single(border=>border.Name=="LanguageIndicator");
            Check(!SystemParameters.ClientAreaAnimation || ((TranslateTransform)indicator.RenderTransform).HasAnimatedProperties && indicator.HasAnimatedProperties,"Language indicator slides and morphs");
            Check(Has("Extensions") && ReferenceEquals(root,window.Content),"English switch preserves page");
            using(var json=JsonDocument.Parse(File.ReadAllText(preferences)))Check(json.RootElement.GetProperty("language").GetString()=="en" && json.RootElement.GetProperty("keep").GetBoolean(),"Language persistence preserves preferences");
            var menu=(ContextMenu)type.GetMethod("MenuScuro",Private).Invoke(window,null);var item=new MenuItem{Header="Attive"};menu.Items.Add(item);
            menu.PlacementTarget=root;menu.IsOpen=true;Pump();Check((string)item.Header=="Enabled","English menu");menu.IsOpen=false;
            SetLanguage("it");Pump();Check(Has("Estensioni") && (string)item.Header=="Attive","Italian labels and menu restored");SetLanguage("en");Pump();
            var image=Render(root);int w=image.PixelWidth,h=image.PixelHeight;var pixels=new byte[w*h*4];image.CopyPixels(pixels,w*4,0);
            Check(pixels[3]==0 && pixels[((h-1)*w+w-1)*4+3]==0,"Transparent stepped corners");
            int p=((h-1)*w+w/2)*4;Check(pixels[p]==49 && pixels[p+1]==61 && pixels[p+2]==57,"Bottom border drawn above landscape");
            Save(root,screenshots,"engine-home.png");
            string style=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".config","vascend-engine","stile.json");string before=File.Exists(style)?File.ReadAllText(style):null;
            foreach(string page in new[]{"Stile","Font","Barra","Isola","Widget","Windows","VS Code","Vascend IDE","Terminali"})
            {
                type.GetMethod("Apri",Private).Invoke(window,new object[]{page});Pump();
                Check(Nodes(root).Any(node=>node.GetType().Name=="OpzioniSolaLettura"),"Disabled extension page: "+page);
                Check(type.GetField("azioneApplica",Private).GetValue(window)==null,"No apply action: "+page);
            }
            type.GetMethod("Apri",Private).Invoke(window,new object[]{"Stile"});Pump();
            Check(Nodes(root).OfType<Border>().Any(border=>border.Child is StackPanel tabs && tabs.Children.OfType<Grid>().Count()==9),"Nine tabs share one segmented surface");
            var fontTab=Nodes(root).OfType<Button>().First(button=>Nodes(button).OfType<TextBlock>().Any(label=>label.Text=="Font"));
            fontTab.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();
            Check((string)type.GetField("sezione",Private).GetValue(window)=="Font" && Has("Vascend Grotesk Mono"),"Segment click opens its settings page");
            Save(root,screenshots,"engine-extension.png");
            Check((File.Exists(style)?File.ReadAllText(style):null)==before,"Browsing leaves saved style unchanged");
            SetLanguage("it");Pump();Check(Has("Stile"),"Internal navigation translates back to Italian");
            Check(!window.AllowsTransparency,"Native window compositing retained");
        }
        finally{window.Close();Directory.Delete(temp,true);}
    }
}
