using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace VascendEngine;

partial class Finestra
{
    FrameworkElement SelettoreLingua()
    {
        var flag=new Image {Width=26,Height=18,Stretch=Stretch.Fill,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(10,0,10,0)};
        AutomationProperties.SetName(flag,"Language flag");
        var movement=new TranslateTransform();
        var indicator=new Border {Name="LanguageIndicator",Width=38,Height=26,CornerRadius=new CornerRadius(8),Background=Tema.LimePennello,
            HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3,0,0,0),RenderTransform=movement,IsHitTestVisible=false};
        var track=new Grid {Width=84,Height=32};track.Children.Add(indicator);
        var labels=new[]{Tema.Scritta("EN",10.5,weight:FontWeights.SemiBold),Tema.Scritta("IT",10.5,weight:FontWeights.SemiBold)};
        var buttons=new Button[2];
        var options=new StackPanel {Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(3,0,0,0)};
        for(int index=0;index<2;index++)
        {
            string code=index==0 ? "en" : "it";
            var button=Bottone(labels[index],()=>I18n.SetLanguage(code),radius:new CornerRadius(8));
            button.Width=38;button.Height=26;button.Padding=new Thickness(0);button.Margin=new Thickness(0,0,2,0);
            button.Background=Brushes.Transparent;button.BorderBrush=Brushes.Transparent;
            button.ToolTip=code=="en" ? "English" : "Italiano";
            AutomationProperties.SetName(button,(string)button.ToolTip);
            buttons[index]=button;options.Children.Add(button);
        }
        track.Children.Add(options);
        var row=new StackPanel {Orientation=Orientation.Horizontal};row.Children.Add(flag);
        row.Children.Add(new Border {Width=1,Height=16,Background=Contorno,VerticalAlignment=VerticalAlignment.Center});row.Children.Add(track);
        var shell=new Border {CornerRadius=new CornerRadius(12),BorderBrush=Contorno,BorderThickness=new Thickness(1),Background=Tema.Pennello(211,218,190,.035),
            Child=row,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,14,0)};
        void Update(bool animate)
        {
            bool english=I18n.Language=="en";double target=english ? 0 : 40;
            double from=movement.X;movement.BeginAnimation(TranslateTransform.XProperty,null);movement.X=target;
            flag.Source=BandieraLingua(english);flag.ToolTip=english ? "English" : "Italiano";
            for(int index=0;index<2;index++)
            {
                bool selected=english== (index==0);labels[index].Foreground=selected ? Tema.GhiaccioPannello : Tema.BiancoTenue;
                AutomationProperties.SetItemStatus(buttons[index],selected ? "Selected" : "");
            }
            if(!animate || !SystemParameters.ClientAreaAnimation)return;
            movement.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(from,target,TimeSpan.FromMilliseconds(240)) {EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
            indicator.BeginAnimation(Border.CornerRadiusProperty,new CornerMorph {From=new CornerRadius(16,5,16,5),To=new CornerRadius(8),Duration=TimeSpan.FromMilliseconds(280),FillBehavior=FillBehavior.Stop});
        }
        void Changed(object sender,EventArgs args)=>Update(shell.IsLoaded);
        Update(false);I18n.LanguageChanged+=Changed;Closed+=(_,_)=>I18n.LanguageChanged-=Changed;
        return shell;
    }

    // Vector flags remain sharp at every DPI and do not depend on emoji fonts.
    static DrawingImage BandieraLingua(bool english)
    {
        var drawing=new DrawingGroup();
        using(var dc=drawing.Open())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0,0,26,18),2,2));
            void Box(Brush brush,double x,double y,double width,double height)=>dc.DrawRectangle(brush,null,new Rect(x,y,width,height));
            if(english)
            {
                Box(Tema.Pennello(34,49,92,1),0,0,26,18);
                dc.DrawLine(new Pen(Brushes.White,5),new Point(0,0),new Point(26,18));
                dc.DrawLine(new Pen(Brushes.White,5),new Point(26,0),new Point(0,18));
                var red=Tema.Pennello(204,41,60,1);
                dc.DrawLine(new Pen(red,2),new Point(0,0),new Point(26,18));
                dc.DrawLine(new Pen(red,2),new Point(26,0),new Point(0,18));
                Box(Brushes.White,9,0,8,18);Box(Brushes.White,0,5,26,8);
                Box(red,11,0,4,18);Box(red,0,7,26,4);
            }
            else
            {
                Box(Tema.Pennello(0,146,70,1),0,0,9,18);Box(Brushes.White,9,0,8,18);Box(Tema.Pennello(206,43,55,1),17,0,9,18);
            }
            dc.Pop();
        }
        drawing.Freeze();var image=new DrawingImage(drawing);image.Freeze();return image;
    }
}
