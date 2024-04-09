using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CuteVideoEditor.Views.Controls;

public sealed partial class CropBoxControl : UserControl
{
    public static readonly DependencyProperty RectangleProperty =
        DependencyProperty.Register(nameof(Rectangle), typeof(CropRect), typeof(CropBoxControl), new PropertyMetadata(default(CropBoxControl)));
    public CropRect Rectangle
    {
        get { return (CropRect)GetValue(RectangleProperty); }
        set { SetValue(RectangleProperty, value); }
    }

    public static readonly DependencyProperty OverlayScaleProperty =
        DependencyProperty.Register(nameof(OverlayScale), typeof(double), typeof(CropBoxControl), new PropertyMetadata(1.0));
    public double OverlayScale
    {
        get { return (double)GetValue(OverlayScaleProperty); }
        set { SetValue(OverlayScaleProperty, value); }
    }

    public CropBoxControl()
    {
        InitializeComponent();
    }

    static readonly InputCursor moveCursor = InputCursor.CreateFromCoreCursor(new(Windows.UI.Core.CoreCursorType.SizeAll, 0));
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType is PointerDeviceType.Mouse)
        {
            var x1 = GetX1(Rectangle, OverlayScale);
            var y1 = GetY1(Rectangle, OverlayScale);
            var x2 = GetX2(Rectangle, OverlayScale);
            var y2 = GetY2(Rectangle, OverlayScale);

            var pt = e.GetCurrentPoint(this);

            // if cursor position is inside box
            if (pt.Position.X >= x1 && pt.Position.X <= x2 && pt.Position.Y >= y1 && pt.Position.Y <= y2)
            {
                ProtectedCursor = moveCursor;
                e.Handled = true;
                return;
            }
        }

        ProtectedCursor = null;
    }

    public static double GetX1(CropRect cropRect, double scale) =>
        (cropRect.Rect.CenterX - cropRect.Rect.Width / 2.0) * scale;

    public static double GetY1(CropRect cropRect, double scale) =>
        (cropRect.Rect.CenterY - cropRect.Rect.Height / 2.0) * scale;

    public static double GetX2(CropRect cropRect, double scale) =>
        (cropRect.Rect.CenterX + cropRect.Rect.Width / 2.0) * scale;

    public static double GetY2(CropRect cropRect, double scale) =>
        (cropRect.Rect.CenterY + cropRect.Rect.Height / 2.0) * scale;

    public static Brush GetBrush(CropRect cropRect) =>
        new SolidColorBrush(cropRect.Type switch
        {
            CropRectType.Frozen => Colors.White,
            CropRectType.Interpolated => Colors.Aqua,
            CropRectType.FirstFrameUnfrozen => Colors.Pink,
            CropRectType.None => Colors.Transparent,
            _ => throw new NotImplementedException()
        });
}
