using System;
using System.Windows;
using System.Windows.Media;
using BarraVascend;

namespace VascendEngine;

// Il ritaglio vale per i figli; il contorno è un visual finale, sopra il paesaggio.
sealed class CorniceEngine : Cornice
{
    readonly DrawingVisual contorno=new();
    double width,height,unit;
    public CorniceEngine(){AddVisualChild(contorno);SizeChanged+=(_,_)=>AggiornaRitaglio();}
    protected override int VisualChildrenCount=>base.VisualChildrenCount+1;
    protected override Visual GetVisualChild(int index)=>index == base.VisualChildrenCount ? contorno : base.GetVisualChild(index);
    public void AggiornaRitaglio()
    {
        double u=Unita;
        if(ActualWidth<=0 || ActualHeight<=0 || ActualWidth==width && ActualHeight==height && Math.Abs(u-unit)<.001)return;
        width=ActualWidth;height=ActualHeight;unit=u;
        Clip=Gradini.Riempimento(width,height,u,Profilo);
        using var dc=contorno.RenderOpen();
        if(Bordo!=null)dc.DrawGeometry(Bordo,null,Gradini.Contorno(width,height,u,Profilo));
    }
}
