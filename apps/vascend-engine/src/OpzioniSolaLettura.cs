using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace VascendEngine;

sealed class OpzioniSolaLettura : Decorator
{
    public OpzioniSolaLettura(UIElement content)
    {
        Child=content;
        Loaded+=(_,_)=>BloccaControlli(Child);
        PreviewMouseDown+=(_,e)=>{
            for(DependencyObject node=e.OriginalSource as DependencyObject;node != null;)
            {
                if(node is ScrollBar)return;
                if(node is Visual)node=VisualTreeHelper.GetParent(node);else node=LogicalTreeHelper.GetParent(node);
            }
            e.Handled=true;
        };
        PreviewKeyDown+=(_,e)=>{
            if(e.Key is not (Key.PageDown or Key.PageUp or Key.Up or Key.Down or Key.Home or Key.End or Key.Tab))e.Handled=true;
        };
        PreviewTextInput+=(_,e)=>e.Handled=true;
    }
    static void BloccaControlli(DependencyObject node)
    {
        if(node == null || node is ScrollBar)return;
        if(node is ButtonBase or TextBoxBase or Selector or Slider or BarraVascend.Tasto)
        {
            if(node is UIElement element){element.IsEnabled=false;element.Opacity=.55;}
            return;
        }
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)BloccaControlli(VisualTreeHelper.GetChild(node,i));
    }
}
partial class Finestra
{
    bool SolaLettura(ExtensionManifest manifest)=>!activation.Enabled(manifest.Id) || activation.Issues(manifest).Count > 0;
    FrameworkElement AvvisoSolaLettura(ExtensionManifest manifest)
    {
        var issues=activation.Issues(manifest);
        string text=!activation.Enabled(manifest.Id) ? "Sola lettura · Estensione disattivata. Puoi consultare le opzioni; per modificarle, attivala dalla home." : "Sola lettura · Risolvi il conflitto dalla home per modificare queste opzioni.";
        var note=Tema.Scritta(text,12,Tema.BiancoTenue);note.TextWrapping=TextWrapping.Wrap;
        if(issues.Count > 0)note.ToolTip=string.Join("\n",issues);
        return new Border {Background=Superficie,BorderBrush=Contorno,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(12,9,12,9),Margin=new Thickness(0,4,0,12),Child=note};
    }
}
