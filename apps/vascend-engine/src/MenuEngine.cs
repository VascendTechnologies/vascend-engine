using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VascendEngine;

partial class Finestra
{
    ContextMenu MenuScuro()
    {
        var menu=new ContextMenu {Background=Superficie,Foreground=Tema.Bianco,BorderBrush=Contorno,HasDropShadow=false,OverridesDefaultStyle=true,SnapsToDevicePixels=true};
        var border=new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty,Superficie);border.SetValue(Border.BorderBrushProperty,Contorno);
        border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(9));border.SetValue(Border.PaddingProperty,new Thickness(5));
        var stack=new FrameworkElementFactory(typeof(StackPanel));stack.SetValue(StackPanel.IsItemsHostProperty,true);
        stack.SetValue(System.Windows.Input.KeyboardNavigation.DirectionalNavigationProperty,System.Windows.Input.KeyboardNavigationMode.Cycle);
        border.AppendChild(stack);menu.Template=new ControlTemplate(typeof(ContextMenu)){VisualTree=border};
        var style=new Style(typeof(MenuItem));
        style.Setters.Add(new Setter(Control.OverridesDefaultStyleProperty,true));
        style.Setters.Add(new Setter(Control.ForegroundProperty,Tema.Bianco));style.Setters.Add(new Setter(Control.FontFamilyProperty,Tema.Testo));style.Setters.Add(new Setter(Control.FontSizeProperty,13.0));
        var surface=new FrameworkElementFactory(typeof(Border));surface.Name="Surface";
        surface.SetValue(Border.BackgroundProperty,Brushes.Transparent);surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));surface.SetValue(Border.PaddingProperty,new Thickness(10,8,14,8));surface.SetValue(FrameworkElement.MinWidthProperty,184.0);
        var dock=new FrameworkElementFactory(typeof(DockPanel));
        var slot=new FrameworkElementFactory(typeof(Grid));slot.SetValue(FrameworkElement.WidthProperty,22.0);slot.SetValue(DockPanel.DockProperty,Dock.Left);
        var check=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));check.Name="Check";
        check.SetValue(System.Windows.Shapes.Path.DataProperty,Geometry.Parse("M2,7 L6,11 L14,2"));
        check.SetValue(System.Windows.Shapes.Path.StrokeProperty,Tema.LimePennello);check.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty,1.7);
        check.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty,PenLineCap.Round);check.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty,PenLineCap.Round);
        check.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);check.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);slot.AppendChild(check);
        var icon=new FrameworkElementFactory(typeof(ContentPresenter));icon.Name="Icon";icon.SetValue(ContentPresenter.ContentSourceProperty,"Icon");icon.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);slot.AppendChild(icon);dock.AppendChild(slot);
        var header=new FrameworkElementFactory(typeof(ContentPresenter));header.SetValue(ContentPresenter.ContentSourceProperty,"Header");header.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);header.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);dock.AppendChild(header);
        surface.AppendChild(dock);var template=new ControlTemplate(typeof(MenuItem)){VisualTree=surface};
        var selected=new Trigger {Property=MenuItem.IsCheckedProperty,Value=true};selected.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"Check"));selected.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed,"Icon"));template.Triggers.Add(selected);
        var highlighted=new Trigger {Property=MenuItem.IsHighlightedProperty,Value=true};highlighted.Setters.Add(new Setter(Border.BackgroundProperty,Tema.Pennello(211,218,190,.11),"Surface"));template.Triggers.Add(highlighted);
        var disabled=new Trigger {Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.4,"Surface"));template.Triggers.Add(disabled);
        style.Setters.Add(new Setter(Control.TemplateProperty,template));menu.ItemContainerStyle=style;return menu;
    }
}
