using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BarraVascend;

// Aspetto condiviso tra anteprima Engine e widget reali del desktop.
static class WidgetAspetto
{
    public static readonly (string id,string nome,string descrizione)[] Stili = {
        ("classico","Classico","Orologio essenziale e firma, come nella versione originale."),
        ("editoriale","Editoriale","Carattere serif, firma corsiva e un dettaglio laterale."),
        ("terminale","Terminale","Carattere monospazio e pannello ispirato a una console."),
        ("vetro","Vetro","Pannello traslucido, angoli morbidi e bordo sottile.")
    };
    public static string StileDi(Widget w) => Array.Exists(Stili,s=>s.id == w.Aspetto) ? w.Aspetto : "classico";
    public static FontFamily Font(Widget w) => StileDi(w) switch {
        "editoriale" => new FontFamily("Georgia, Cambria"),
        "terminale" => new FontFamily("Vascend Grotesk Mono, Cascadia Mono, Consolas"),
        _ => Tema.Testo
    };
    public static TextBlock Testo(Widget w,string value,double dimensione,FontWeight? peso=null)
    {
        var t=new TextBlock {Text=value,FontFamily=Font(w),FontSize=Math.Max(6,dimensione),
            Foreground=new SolidColorBrush(Stile.Colore(string.IsNullOrEmpty(w.Colore) ? "#FFFFFF" : w.Colore)),
            FontWeight=peso ?? (StileDi(w) == "terminale" ? FontWeights.Medium : FontWeights.Normal)};
        System.Windows.Documents.Typography.SetNumeralAlignment(t,FontNumeralAlignment.Tabular);
        return t;
    }
    public static FrameworkElement Firma(Widget w,double dimensione)
    {
        if(!w.Firma) return null;
        string nome=(w.NomeFirma ?? "Vascend").Trim();
        if(nome.Length == 0) return null;
        string legacy=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),@".config\barra-vascend\danilov.png");
        if(StileDi(w) == "classico" && nome == "danilov" && File.Exists(legacy))
        {
            var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(legacy);bitmap.EndInit();bitmap.Freeze();
            var image=new Image {Width=254*dimensione/96,Height=32*dimensione/96,Source=bitmap,HorizontalAlignment=HorizontalAlignment.Center};
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);return image;
        }
        var text=Testo(w,nome,Math.Max(8,dimensione*.25));
        text.FontStyle=StileDi(w) is "editoriale" or "classico" ? FontStyles.Italic : FontStyles.Normal;
        text.FontWeight=FontWeights.Normal;text.HorizontalAlignment=HorizontalAlignment.Center;
        text.TextAlignment=TextAlignment.Center;text.Margin=new Thickness(0,dimensione*.025,0,0);
        text.MaxWidth=dimensione*8;text.TextTrimming=TextTrimming.CharacterEllipsis;
        text.Opacity=.8;return text;
    }
    public static FrameworkElement Decora(Widget w,FrameworkElement content,double scala=1)
    {
        string stile=StileDi(w);
        if(stile == "classico") return content;
        var accent=new SolidColorBrush(Stile.Colore(w.Colore ?? "#FFFFFF"));
        if(stile == "editoriale") return new Border {
            BorderBrush=accent,BorderThickness=new Thickness(2*scala,0,0,0),Padding=new Thickness(16*scala,4*scala,8*scala,4*scala),Child=content
        };
        if(stile == "terminale")
        {
            var panel=new StackPanel();
            var label=Testo(w,w.Tipo switch {"orologio"=>"LOCAL / TIME", "data"=>"LOCAL / DATE", "sistema"=>"SYSTEM / LIVE", "musica"=>"NOW / PLAYING", _=>"VASCEND / WIDGET"},Math.Max(7,10*scala),FontWeights.Medium);
            label.Opacity=.55;label.Margin=new Thickness(0,0,0,8*scala);panel.Children.Add(label);panel.Children.Add(content);
            return new Border {Background=new SolidColorBrush(Color.FromArgb(220,15,18,15)),BorderBrush=new SolidColorBrush(Color.FromArgb(85,155,178,128)),BorderThickness=new Thickness(1),Padding=new Thickness(18*scala,12*scala,18*scala,14*scala),Child=panel};
        }
        return new Border {CornerRadius=new CornerRadius(18*scala),Background=new SolidColorBrush(Color.FromArgb(165,24,32,38)),BorderBrush=new SolidColorBrush(Color.FromArgb(60,235,246,255)),BorderThickness=new Thickness(1),Padding=new Thickness(22*scala,12*scala,22*scala,16*scala),Child=content};
    }
}
