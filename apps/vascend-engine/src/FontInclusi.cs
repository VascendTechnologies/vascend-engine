using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BarraVascend;

namespace VascendEngine;

static class FontInclusi
{
    public const string Famiglia="Vascend Grotesk Mono";
    public static readonly string[] Pesi={"Regular","Medium","SemiBold"};
    public static FontFamily Anteprima => new(new Uri("pack://application:,,,/VascendEngine;component/"),"./Assets/Fonts/#Vascend Grotesk Mono");
    public static byte[] Dati(string peso)
    {
        var resource=Application.GetResourceStream(new Uri("pack://application:,,,/VascendEngine;component/Assets/Fonts/VascendGroteskMono-"+peso+".ttf")) ?? throw new Exception("Font incluso non trovato.");
        using var stream=resource.Stream;using var data=new MemoryStream();stream.CopyTo(data);return data.ToArray();
    }
    public static string Installa()
    {
        string directory=Path.Combine(Stile.Cartella,"font-inclusi");Directory.CreateDirectory(directory);
        foreach(string peso in Pesi)
        {
            byte[] data=Dati(peso);string hash=Convert.ToHexString(SHA256.HashData(data))[..10].ToLowerInvariant();
            string file=Path.Combine(directory,"VascendGroteskMono-"+peso+"-"+hash+".ttf");
            if(!File.Exists(file))File.WriteAllBytes(file,data);
            Sistema.InstallaFont(file);
        }
        return "Vascend Grotesk Mono installato per Windows: Regular, Medium e SemiBold.";
    }
}
partial class Finestra
{
    FrameworkElement SezioneFontInclusi()
    {
        var body=new StackPanel {MaxWidth=840,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(8)};
        body.Children.Add(Tema.Scritta("Vascend Grotesk Mono",26,Tema.Bianco,FontWeights.SemiBold));
        var description=Tema.Scritta("Incluso in Vascend Technologies · Regular, Medium e SemiBold",13,Tema.BiancoTenue);description.Margin=new Thickness(0,10,0,24);body.Children.Add(description);
        foreach(string peso in FontInclusi.Pesi)
        {
            var row=new StackPanel {Margin=new Thickness(18,14,18,18)};
            row.Children.Add(Tema.Scritta(peso,11,Tema.BiancoTenue));
            var sample=new TextBlock {Text="Vascend / Build something yours.\n0123456789  {} [] => !=",FontFamily=FontInclusi.Anteprima,FontSize=23,Foreground=Tema.Bianco,
                FontWeight=peso == "Medium" ? FontWeights.Medium : peso == "SemiBold" ? FontWeights.SemiBold : FontWeights.Normal,Margin=new Thickness(0,10,0,0)};
            row.Children.Add(sample);
            body.Children.Add(new Border {Background=Superficie,BorderBrush=Contorno,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Child=row,Margin=new Thickness(0,0,0,12)});
        }
        var help=Tema.Scritta("Il font è integrato nell'estensione e nell'anteprima. Installalo per usarlo anche in editor, terminali e nelle altre app di Windows. Lo trovi in cima ai selettori dei font.",13,Tema.BiancoTenue);
        help.TextWrapping=TextWrapping.Wrap;help.Margin=new Thickness(0,8,0,0);body.Children.Add(help);
        Piede(()=>Esegui(("Font Vascend",FontInclusi.Installa)),"Installa font incluso");return Scorri(body);
    }
}
