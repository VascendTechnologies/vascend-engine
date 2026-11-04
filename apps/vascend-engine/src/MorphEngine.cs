using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VascendEngine;

sealed class CornerMorph : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty=DependencyProperty.Register(nameof(From),typeof(CornerRadius),typeof(CornerMorph));
    public static readonly DependencyProperty ToProperty=DependencyProperty.Register(nameof(To),typeof(CornerRadius),typeof(CornerMorph));
    public CornerRadius From {get=>(CornerRadius)GetValue(FromProperty);set=>SetValue(FromProperty,value);}
    public CornerRadius To {get=>(CornerRadius)GetValue(ToProperty);set=>SetValue(ToProperty,value);}
    public override Type TargetPropertyType=>typeof(CornerRadius);
    protected override Freezable CreateInstanceCore()=>new CornerMorph();
    public override object GetCurrentValue(object origin,object destination,AnimationClock clock)
    {
        double p=clock.CurrentProgress ?? 0;p=1-Math.Pow(1-p,3);
        double L(double a,double b)=>a+(b-a)*p;
        var a=From;var b=To;return new CornerRadius(L(a.TopLeft,b.TopLeft),L(a.TopRight,b.TopRight),L(a.BottomRight,b.BottomRight),L(a.BottomLeft,b.BottomLeft));
    }
}
static class MorphEngine
{
    static CornerRadius Shape(CornerRadius initial,double delta)
    {
        double Radius(double value)=>value == 0 ? 0 : Math.Max(3,value+delta);
        return new CornerRadius(Radius(initial.TopLeft),Radius(initial.TopRight),Radius(initial.BottomRight),Radius(initial.BottomLeft));
    }
    static void AnimateScale(ScaleTransform transform,DependencyProperty property,double value,double milliseconds)
    {
        double from=(double)transform.GetValue(property);transform.BeginAnimation(property,null);transform.SetValue(property,value);
        transform.BeginAnimation(property,new DoubleAnimation(from,value,TimeSpan.FromMilliseconds(milliseconds)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
    }
    public static void Setup(Button button,CornerRadius initial,bool primary)
    {
        bool over=false,pressed=false;
        void Update()
        {
            if(!button.IsLoaded)return;
            if(button.Template.FindName("Surface",button) is not Border surface)return;
            bool hot=(over || button.IsKeyboardFocused) && button.IsEnabled;
            double duration=pressed ? 80 : 170;
            var target=Shape(initial,pressed ? -3 : hot ? 5 : 0);
            var from=surface.CornerRadius;surface.BeginAnimation(Border.CornerRadiusProperty,null);surface.CornerRadius=target;
            if(surface.RenderTransform is not ScaleTransform scale){scale=new ScaleTransform(1,1);surface.RenderTransform=scale;surface.RenderTransformOrigin=new Point(.5,.5);}
            var baseColor=(button.Background as SolidColorBrush)?.Color ?? Colors.Transparent;
            Color destination=hot && !primary ? Color.FromArgb(36,211,218,190) : baseColor;
            if(hot && primary)destination=Color.FromArgb(baseColor.A,(byte)Math.Min(255,baseColor.R+7),(byte)Math.Min(255,baseColor.G+7),(byte)Math.Min(255,baseColor.B+7));
            Color previous=(surface.Background as SolidColorBrush)?.Color ?? baseColor;
            var brush=new SolidColorBrush(destination);surface.SetCurrentValue(Border.BackgroundProperty,brush);
            if(!SystemParameters.ClientAreaAnimation)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);
                scale.ScaleX=scale.ScaleY=pressed ? .965 : 1;return;
            }
            surface.BeginAnimation(Border.CornerRadiusProperty,new CornerMorph{From=from,To=target,Duration=TimeSpan.FromMilliseconds(duration),FillBehavior=FillBehavior.Stop});
            double pressedScale=button.Tag as string == "extension-card" ? .99 : .965;
            AnimateScale(scale,ScaleTransform.ScaleXProperty,pressed ? pressedScale : 1,duration);
            AnimateScale(scale,ScaleTransform.ScaleYProperty,pressed ? pressedScale : 1,duration);
            brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(previous,destination,TimeSpan.FromMilliseconds(duration)){FillBehavior=FillBehavior.Stop});
        }
        button.MouseEnter+=(_,_)=>{over=true;Update();};button.MouseLeave+=(_,_)=>{over=false;pressed=false;Update();};
        button.PreviewMouseLeftButtonDown+=(_,_)=>{pressed=true;Update();};button.PreviewMouseLeftButtonUp+=(_,_)=>{pressed=false;Update();};
        button.LostMouseCapture+=(_,_)=>{pressed=false;Update();};
        button.IsKeyboardFocusedChanged+=(_,_)=>Update();
        button.PreviewKeyDown+=(_,e)=>{if(e.Key is Key.Space or Key.Enter){pressed=true;Update();}};
        button.PreviewKeyUp+=(_,e)=>{if(e.Key is Key.Space or Key.Enter){pressed=false;Update();}};
        button.IsEnabledChanged+=(_,_)=>{pressed=false;button.Opacity=button.IsEnabled ? 1 : .45;Update();};
        button.Unloaded+=(_,_)=>{
            if(button.Template.FindName("Surface",button) is not Border surface)return;
            surface.BeginAnimation(Border.CornerRadiusProperty,null);surface.CornerRadius=initial;
            if(surface.RenderTransform is ScaleTransform scale){scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);scale.ScaleX=scale.ScaleY=1;}
        };
    }
}
