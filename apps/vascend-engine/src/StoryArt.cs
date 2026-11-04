using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;

namespace VascendEngine;

// Paesaggio conservato nel compositore: geometrie statiche create una volta,
// animazioni di trasformazioni/opacità senza timer e senza ridisegnare le celle.
sealed class StoryArt : FrameworkElement
{
    readonly DrawingVisual visual = new();
    readonly ScaleTransform viewport = new();
    readonly List<AnimationClock> clocks = new();
    Window owner;
    HwndSource source;
    bool resizing, playing;
    static Brush P(string hex) => Tema.Congela(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)));
    static readonly Brush Far = P("#292e22"), Near = P("#404c2b"), Pine = P("#88945b"), Ink = P("#131412"), Ground = P("#1c2117");
    static readonly Brush Lime = P("#d3e65d"), Eye = P("#d8ff2f"), Light = P("#eef7b4"), Stars = P("#697154");
    static readonly string[] Tree = { "..#..", ".###.", "..#..", ".###.", "#####", ".###.", "#####", "..#.." };
    static readonly string[] Lab = { ".....oo.....", ".....##.....", "..##.##.##..", "..########..", ".##########.", "############", "#ll##ll##ll#", "############", "#ll######ll#", "#ll##ll##ll#", "#####ll#####" };
    static readonly string[][] Wolf = {
        new[] { "..........#.#.", "..........###.", ".........####.", "#........###o#", "##.......####.", ".#########....", "..########....", "..#.#...#.#...", "..#.#...#.#..." },
        new[] { "..........#.#.", "..........###.", ".........####.", "#........###o#", "##.......####.", ".#########....", "..########....", ".#...#.#...#..", ".#...#.#...#.." }
    };
    static readonly string[][] Robot = {
        new[] { "..o..", "..#..", "#####", "#o#o#", "#####", ".###.", ".#.#." },
        new[] { "..o..", "..#..", "#####", "#o#o#", "#####", ".###.", "#...#" }
    };
    static readonly int[] Trees = {8,15,52,58,64,101,142,150,188,196,228};
    public StoryArt()
    {
        Height = 174;
        IsHitTestVisible = false;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
        AddVisualChild(visual);
        visual.Transform = viewport;
        BuildScene();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += (_, _) => UpdatePlayback();
    }
    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => index == 0 ? visual : throw new ArgumentOutOfRangeException(nameof(index));
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {
        base.OnRenderSizeChanged(info);
        viewport.ScaleX = ActualWidth / 240;
        viewport.ScaleY = ActualHeight / 40;
    }
    static DrawingGroup Drawing(Action<DrawingContext> draw)
    {
        var group = new DrawingGroup();
        using (var dc = group.Open()) draw(dc);
        group.Freeze();
        return group;
    }
    static void Rect(DrawingContext dc, Brush color, double x, double y, double w=1, double h=1)
        => dc.DrawRectangle(color, null, new Rect(x,y,w,h));
    static void Sprite(DrawingContext dc, string[] rows, double x, double y, Brush color)
    {
        for (int r=0;r<rows.Length;r++) for (int c=0;c<rows[r].Length;c++)
        {
            char cell = rows[r][c];
            if (cell != '.') Rect(dc,cell == 'o' ? Eye : cell == 'l' ? Light : color,x+c,y+r,1.02,1.02);
        }
    }
    void Animate(Animatable target, DependencyProperty property, AnimationTimeline animation)
    {
        Timeline.SetDesiredFrameRate(animation,30);
        var clock = animation.CreateClock();
        clock.Controller.Pause();
        target.ApplyAnimationClock(property,clock);
        clocks.Add(clock);
    }
    void Loop(Animatable target, DependencyProperty property, double from, double to, double seconds, bool reverse=false, double delay=0)
    {
        Animate(target,property,new DoubleAnimation(from,to,TimeSpan.FromSeconds(seconds)) {
            RepeatBehavior = RepeatBehavior.Forever, AutoReverse = reverse, BeginTime = TimeSpan.FromSeconds(delay)
        });
    }
    void Walk(DrawingGroup scene, string[][] poses, Brush color, double y, double start)
    {
        var move = new TranslateTransform(start,y);
        var actor = new DrawingGroup { Transform = move };
        for (int i=0;i<2;i++)
        {
            var pose = new DrawingGroup { Opacity = i == 0 ? 1 : 0 };
            pose.Children.Add(Drawing(dc => Sprite(dc,poses[i],0,0,color)));
            actor.Children.Add(pose);
            var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(4.0/9), RepeatBehavior = RepeatBehavior.Forever };
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(i == 0 ? 1 : 0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(i == 0 ? 0 : 1,KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.0/9))));
            Animate(pose,DrawingGroup.OpacityProperty,animation);
        }
        // I due personaggi percorrono lo stesso tragitto con partenze sfalsate.
        Loop(move,TranslateTransform.XProperty,-15,255,90);
        clocks[^1].Controller.Seek(TimeSpan.FromSeconds((start+15)/3),TimeSeekOrigin.BeginTime);
        scene.Children.Add(actor);
    }
    void BuildScene()
    {
        var scene = new DrawingGroup();
        scene.Children.Add(Drawing(dc => {
            Rect(dc,Tema.GhiaccioPannello,0,0,240,40);
            Rect(dc,Light,196,3,3,1); Rect(dc,Light,195,4,5,3); Rect(dc,Light,196,7,3,1); Rect(dc,Ink,198,3,3,4);
        }));
        // Cinque piccoli gruppi di lucciole, anziché 25 ridisegni per fotogramma.
        for (int group=0;group<5;group++)
        {
            int index=group;
            var stars = new DrawingGroup { Opacity = .5 };
            stars.Children.Add(Drawing(dc => {
                for (int i=index;i<25;i+=5) Rect(dc,Stars,(i*47+13)%240,2+(i*7)%12,.45,.45);
            }));
            Loop(stars,DrawingGroup.OpacityProperty,.25,.7,2.5+group*.3,true);
            scene.Children.Add(stars);
        }
        scene.Children.Add(Drawing(dc => {
            for (int x=0;x<240;x++)
            {
                double top=Math.Round(40-(15+8*Math.Sin(x/13.0)+5*Math.Sin(x/5.3+1)));
                Rect(dc,Far,x,top,1.02,40-top);
                double hill=Math.Round(40-(9+3*Math.Sin(x/9.0+2)+1.6*Math.Sin(x/3.7)));
                Rect(dc,Near,x,hill,1.02,40-hill);
            }
            foreach(int x in Trees) Sprite(dc,Tree,x,26,Pine);
            Sprite(dc,Lab,114,23,Lime);
            Rect(dc,Pine,135,22,1,12); Rect(dc,Pine,134,32,3,2);
        }));
        for(int i=0;i<3;i++)
        {
            var move = new TranslateTransform(119,21);
            var smoke = new DrawingGroup { Transform = move, Opacity = .15 };
            smoke.Children.Add(Drawing(dc => Rect(dc,Light,0,0,1.4,1.4)));
            Loop(move,TranslateTransform.YProperty,21,16,20.0/7,delay:i*.95);
            Loop(move,TranslateTransform.XProperty,119,121,20.0/7,delay:i*.95);
            Loop(smoke,DrawingGroup.OpacityProperty,.3,0,20.0/7,delay:i*.95);
            scene.Children.Add(smoke);
        }
        var rotation = new RotateTransform(0,135.5,22);
        var turbine = new DrawingGroup { Transform = rotation };
        turbine.Children.Add(Drawing(dc => {
            for(int i=0;i<4;i++)
            {
                dc.PushTransform(new RotateTransform(i*90,135.5,22));
                Rect(dc,Lime,135,15,1,6.5); Rect(dc,Light,136,15,1.5,4); dc.Pop();
            }
        }));
        Loop(rotation,RotateTransform.AngleProperty,0,360,24);
        scene.Children.Add(turbine);
        scene.Children.Add(Drawing(dc => {
            Rect(dc,Eye,134.5,21,2,2);
            Rect(dc,Ground,0,34,240,6);
            for(int i=0;i<30;i++)
            {
                double x=i*8+(i%3)*2,y=35+i%2;
                Rect(dc,Near,x,y,.6,1); Rect(dc,Near,x+1,y+1,.6,1);
            }
        }));
        Walk(scene,Wolf,Lime,25,20);
        Walk(scene,Robot,Pine,27,107);
        scene.Children.Add(Drawing(dc => {
            Rect(dc,Near,0,38.5,240,.5); Rect(dc,Pine,0,39,240,.35); Rect(dc,Lime,0,39.35,240,.65);
        }));
        using(var dc=visual.RenderOpen()) dc.DrawDrawing(scene);
    }
    void OnLoaded(object sender, RoutedEventArgs e)
    {
        owner = Window.GetWindow(this);
        if(owner != null)
        {
            owner.StateChanged += OwnerChanged;
            source = PresentationSource.FromVisual(owner) as HwndSource;
            source?.AddHook(WindowMessage);
        }
        SystemParameters.StaticPropertyChanged += SystemChanged;
        UpdatePlayback();
    }
    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if(owner != null) owner.StateChanged -= OwnerChanged;
        source?.RemoveHook(WindowMessage);
        SystemParameters.StaticPropertyChanged -= SystemChanged;
        source = null; owner = null; resizing = false;
        UpdatePlayback();
    }
    void OwnerChanged(object sender, EventArgs e) => UpdatePlayback();
    void SystemChanged(object sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) UpdatePlayback();
    }
    IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if(message == 0x0231) { resizing = true; UpdatePlayback(); } // WM_ENTERSIZEMOVE
        if(message == 0x0232) { resizing = false; UpdatePlayback(); } // WM_EXITSIZEMOVE
        return IntPtr.Zero;
    }
    void UpdatePlayback()
    {
        bool run=IsLoaded && IsVisible && !resizing && owner?.WindowState != WindowState.Minimized && SystemParameters.ClientAreaAnimation;
        if(playing == run) return;
        playing=run;
        foreach(var clock in clocks)
        {
            if(run) clock.Controller.Resume(); else clock.Controller.Pause();
        }
    }
}
