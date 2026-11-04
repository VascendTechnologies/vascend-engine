using System;
using System.Windows;
using System.Windows.Media;
using BarraVascend;

namespace VascendEngine;

// Il ritaglio vale anche per i figli: il paesaggio non riempie gli angoli esclusi.
sealed class CorniceEngine : Cornice
{
    double width,height,unit;
    public CorniceEngine(){SizeChanged+=(_,_)=>AggiornaRitaglio();}
    public void AggiornaRitaglio()
    {
        double u=Unita;
        if(ActualWidth <= 0 || ActualHeight <= 0 || ActualWidth == width && ActualHeight == height && Math.Abs(u-unit)<.001)return;
        width=ActualWidth;height=ActualHeight;unit=u;
        Clip=Gradini.Riempimento(width,height,u,Profilo);
    }
}
