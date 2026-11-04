using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace VascendEngine;

// Le chiavi restano in italiano nel codice; traduce soltanto il testo mostrato,
// senza cambiare identificatori, file di stile o valori delle estensioni.
static class I18n
{
    static readonly Dictionary<string,string> English=LoadEnglish();
    static readonly Regex Fragments=new(@"(?<![\p{L}\p{N}_])(?:"+string.Join("|",English.Keys.OrderByDescending(k=>k.Length).Select(Regex.Escape))+@")(?![\p{L}\p{N}_])",RegexOptions.Compiled|RegexOptions.CultureInvariant);
    static readonly List<WeakReference<LocalizedTextBlock>> Labels=new();
    static readonly List<WeakReference<ContextMenu>> Menus=new();
    static readonly DependencyProperty OriginalHeader=DependencyProperty.RegisterAttached("OriginalHeader",typeof(string),typeof(I18n));
    static string language=LoadLanguage();
    public static event EventHandler LanguageChanged;
    public static string Language=>language;
    public static CultureInfo Culture=>CultureInfo.GetCultureInfo(language == "it" ? "it-IT" : "en-US");
    static string Preferences=>Environment.GetEnvironmentVariable("VASCEND_ENGINE_PREFERENCES") is string path && path.Length>0
        ? path : Path.Combine(BarraVascend.Stile.Cartella,"app-preferences.json");
    static Dictionary<string,string> LoadEnglish()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("VascendEngine.Assets.Locales.en.json") ?? throw new InvalidOperationException("English language resources are missing.");
        return JsonSerializer.Deserialize<Dictionary<string,string>>(stream) ?? new();
    }
    static string LoadLanguage()
    {
        try
        {
            if(File.Exists(Preferences))
            {
                using var json=JsonDocument.Parse(File.ReadAllText(Preferences));
                if(json.RootElement.TryGetProperty("language",out var value) && value.GetString() is "it" or "en")return value.GetString();
            }
        }
        catch { }
        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "it" ? "it" : "en";
    }
    public static string Translate(string source)
    {
        if(language == "it" || string.IsNullOrEmpty(source))return source;
        if(English.TryGetValue(source,out string translated))return translated;
        source=Regex.Replace(source,@"\b(\d+) estension(e|i)\b",m=>m.Groups[1].Value+(m.Groups[2].Value == "e" ? " extension" : " extensions"),RegexOptions.CultureInvariant);
        return Fragments.Replace(source,m=>English[m.Value]);
    }
    public static void InitializeCulture()=>CultureInfo.CurrentUICulture=Culture;
    public static void SetLanguage(string selected)
    {
        if(selected is not ("it" or "en"))throw new ArgumentException("Unsupported language.",nameof(selected));
        if(selected == language)return;
        JsonObject preferences=new();
        if(File.Exists(Preferences))preferences=JsonNode.Parse(File.ReadAllText(Preferences)) as JsonObject ?? new();
        preferences["language"]=selected;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(Preferences)));
        string temporary=Preferences+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temporary,preferences.ToJsonString(new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,Preferences,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
        language=selected;InitializeCulture();
        Labels.RemoveAll(reference=>!reference.TryGetTarget(out _));
        foreach(var reference in Labels.ToArray())if(reference.TryGetTarget(out var label))label.RefreshTranslation();
        Menus.RemoveAll(reference=>!reference.TryGetTarget(out _));
        foreach(var reference in Menus.ToArray())if(reference.TryGetTarget(out var menu))LocalizeMenu(menu);
        LanguageChanged?.Invoke(null,EventArgs.Empty);
    }
    public static void Track(LocalizedTextBlock label)
    {
        if(Labels.Count%256 == 0)Labels.RemoveAll(reference=>!reference.TryGetTarget(out _));
        Labels.Add(new(label));
    }
    public static void TrackMenu(ContextMenu menu)
    {
        Menus.Add(new(menu));menu.Opened+=(_,_)=>LocalizeMenu(menu);
    }
    static void LocalizeMenu(ItemsControl menu)
    {
        foreach(object item in menu.Items)
        {
            if(item is not MenuItem entry)continue;
            if(entry.Header is string text)
            {
                string original=entry.GetValue(OriginalHeader) as string;
                if(original == null){original=text;entry.SetValue(OriginalHeader,original);}
                entry.Header=Translate(original);
            }
            if(entry.HasItems)LocalizeMenu(entry);
        }
    }
    public static MessageBoxResult ShowMessage(Window owner,string message,string caption,MessageBoxButton buttons)
        =>MessageBox.Show(owner,Translate(message),Translate(caption),buttons);
}

sealed class LocalizedTextBlock : TextBlock
{
    string source="";
    bool translating;
    public LocalizedTextBlock(){I18n.Track(this);}
    static LocalizedTextBlock() => TextProperty.OverrideMetadata(typeof(LocalizedTextBlock),new FrameworkPropertyMetadata("",OnTextChanged));
    static void OnTextChanged(DependencyObject sender,DependencyPropertyChangedEventArgs args)
    {
        var label=(LocalizedTextBlock)sender;
        if(!label.translating){label.source=args.NewValue as string ?? "";label.RefreshTranslation();}
    }
    public void RefreshTranslation()
    {
        string translated=I18n.Translate(source);
        if(Text == translated)return;
        translating=true;try{SetCurrentValue(TextProperty,translated);}finally{translating=false;}
    }
}
