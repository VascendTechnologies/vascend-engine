using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VascendEngine;

// Icone vettoriali dell'host: stessa griglia, spessore e terminali, senza glifi Unicode.
static class IconeEngine
{
    static readonly Dictionary<string, Geometry> Forms = new();
    static IconeEngine()
    {
        Add("arrow-right", "M4,12 L20,12 M13,5 L20,12 L13,19");
        Add("arrow-left", "M20,12 L4,12 M11,5 L4,12 L11,19");
        Add("plus", "M12,5 L12,19 M5,12 L19,12");
        Add("refresh", "M19.5,8 A8,8 0 1 0 20,14 M19.5,3 L19.5,8.5 L14,8.5");
        Add("github", "M7,8 A3,3 0 1 0 7,2 A3,3 0 0 0 7,8 M7,16 A3,3 0 1 0 7,22 A3,3 0 0 0 7,16 M18,8 A3,3 0 1 0 18,2 A3,3 0 0 0 18,8 M7,8 L7,16 M18,8 L18,10 Q18,13 15,13 L10,13 Q7,13 7,16");
        Add("search", "M15,15 L21,21 M17,10 A7,7 0 1 0 3,10 A7,7 0 0 0 17,10");
        Add("sort", "M5,5 L5,20 M2,17 L5,20 L8,17 M12,5 L21,5 M12,10 L19,10 M12,15 L17,15");
        Add("filter", "M3,5 L21,5 M6,12 L18,12 M10,19 L14,19");
        Add("chevron", "M6,9 L12,15 L18,9");
        Add("folder", "M3,7 L3,5 Q3,4 4,4 L9,4 L12,7 L20,7 Q21,7 21,8 L21,18 Q21,20 19,20 L5,20 Q3,20 3,18 Z M3,9 L21,9");
        Add("upload", "M12,15 L12,3 M7,8 L12,3 L17,8 M4,14 L4,19 Q4,21 6,21 L18,21 Q20,21 20,19 L20,14");
        Add("save", "M5,3 L17,3 L21,7 L21,19 Q21,21 19,21 L5,21 Q3,21 3,19 L3,5 Q3,3 5,3 Z M7,3 L7,9 L16,9 L16,3 M7,21 L7,14 L17,14 L17,21");
        Add("check", "M5,12 L10,17 L20,6");
        Add("extension", "M4,4 L9,4 C8,0 16,0 15,4 L20,4 L20,9 C24,8 24,16 20,15 L20,20 L15,20 C16,16 8,16 9,20 L4,20 L4,15 C8,16 8,8 4,9 Z");
        Add("palette", "M12,3 A9,9 0 1 0 12,21 L14,21 Q17,21 15,18 Q13,15 17,14 L19,14 Q22,14 21,10 A9,9 0 0 0 12,3 M7,8 L7.1,8 M12,6.5 L12.1,6.5 M17,8 L17.1,8 M6,13 L6.1,13");
        Add("layout", "M4,3 L20,3 L20,21 L4,21 Z M8,3 L8,21 M4,8 L8,8 M4,13 L8,13 M4,18 L8,18");
        Add("island", "M8,8 L16,8 A4,4 0 0 1 16,16 L8,16 A4,4 0 0 1 8,8 Z M10,12 L14,12");
        Add("grid", "M3,3 L10,3 L10,10 L3,10 Z M14,3 L21,3 L21,10 L14,10 Z M3,14 L10,14 L10,21 L3,21 Z M14,14 L21,14 L21,21 L14,21 Z");
        Add("window", "M3,4 L21,4 L21,20 L3,20 Z M3,9 L21,9 M7,6.5 L7.1,6.5 M10,6.5 L10.1,6.5");
        Add("code", "M8,6 L2,12 L8,18 M16,6 L22,12 L16,18 M14,3 L10,21");
        Add("terminal", "M3,4 L21,4 L21,20 L3,20 Z M6,9 L10,12 L6,15 M13,16 L18,16");
        Add("minimize", "M5,12 L19,12");
        Add("maximize", "M5,5 L19,5 L19,19 L5,19 Z");
        Add("close", "M6,6 L18,18 M18,6 L6,18");
    }
    static void Add(string name, string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        Forms.Add(name,geometry);
    }
    public static FrameworkElement Verified()
    {
        var shape=Geometry.Parse("M12,1 L15,3 L19,3 L20,7 L23,10 L21,14 L21,18 L17,20 L14,23 L10,21 L6,21 L4,17 L1,14 L3,10 L3,6 L7,4 Z");shape.Freeze();
        var check=Geometry.Parse("M7,12 L10.5,15.5 L17,8.5");check.Freeze();
        var pen=new Pen(Tema.Pennello(19,20,18,1),1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();
        var drawing=new DrawingGroup();using(var dc=drawing.Open()){dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,24,24));dc.DrawGeometry(Tema.LimePennello,null,shape);dc.DrawGeometry(null,pen,check);}drawing.Freeze();
        var source=new DrawingImage(drawing);source.Freeze();return new Image{Source=source,Width=15,Height=15,VerticalAlignment=VerticalAlignment.Center,ToolTip="Estensione ufficiale Vascend"};
    }
    public static FrameworkElement Create(string name, double size=16, Brush color=null)
    {
        var pen = new Pen(color ?? Tema.Bianco,1.65) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
        pen.Freeze();
        var drawing = new DrawingGroup();
        // Mantiene un viewBox 24x24 identico per frecce, folder e simboli.
        using(var dc=drawing.Open())
        {
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,24,24));
            dc.DrawGeometry(null,pen,Forms[name]);
        }
        drawing.Freeze();
        var image = new DrawingImage(drawing); image.Freeze();
        return new Image { Source=image,Width=size,Height=size,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center };
    }
}
