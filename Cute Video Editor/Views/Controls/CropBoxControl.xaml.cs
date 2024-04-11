using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Windows.Input;
using Windows.Foundation;
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

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(CropBoxControl), new PropertyMetadata(default(MainViewModel)));
    public MainViewModel ViewModel
    {
        get { return (MainViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public CropBoxControl()
    {
        InitializeComponent();
    }

    enum HitTestResult { None, Move }
    HitTestResult HitTest(double x, double y)
    {
        var x1 = GetX(Rectangle, OverlayScale);
        var y1 = GetY(Rectangle, OverlayScale);
        var x2 = x1 + GetWidth(Rectangle, OverlayScale);
        var y2 = y1 + GetHeight(Rectangle, OverlayScale);

        // inside
        if (x >= x1 && x <= x2 && y >= y1 && y <= y2)
            return HitTestResult.Move;
        return HitTestResult.None;
    }

    Point? dragStartPoint;
    RectModel cropRectangleBeforeDrag;
    int minDragDistance = 4;
    bool actualDragStarted;
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (ViewModel.MediaPlaybackState is not Windows.Media.Playback.MediaPlaybackState.Playing
            && e.Pointer.PointerDeviceType is PointerDeviceType.Mouse
            && e.GetCurrentPoint(this) is { } pt)
        {
            switch (HitTest(pt.Position.X, pt.Position.Y))
            {
                case HitTestResult.Move:
                    CapturePointer(e.Pointer);
                    dragStartPoint = pt.Position;
                    cropRectangleBeforeDrag = Rectangle.Rect;
                    actualDragStarted = false;
                    e.Handled = true;
                    break;
            }
        }
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        if (dragStartPoint.HasValue && e.Pointer.PointerDeviceType is PointerDeviceType.Mouse)
        {
            dragStartPoint = null;
            e.Handled = true;
        }
    }

    static readonly InputCursor moveCursor = InputCursor.CreateFromCoreCursor(new(Windows.UI.Core.CoreCursorType.SizeAll, 0));
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        if (ViewModel.MediaPlaybackState is not Windows.Media.Playback.MediaPlaybackState.Playing
            && e.Pointer.PointerDeviceType is PointerDeviceType.Mouse
            && e.GetCurrentPoint(this) is { } pt)
        {
            // if cursor position is inside box
            if (HitTest(pt.Position.X, pt.Position.Y) is HitTestResult.Move)
            {
                // are we dragging?
                if (dragStartPoint.HasValue && !actualDragStarted
                    && (Math.Abs(pt.Position.X - dragStartPoint.Value.X) > minDragDistance || Math.Abs(pt.Position.Y - dragStartPoint.Value.Y) > minDragDistance))
                {
                    actualDragStarted = true;
                }
                if (dragStartPoint.HasValue && actualDragStarted)
                {
                    // update the crop rect, this will automatically materialize the frame for us if necessary
                    ViewModel.CurrentCropRect = new(new(
                        (int)(cropRectangleBeforeDrag.CenterX + (pt.Position.X - dragStartPoint.Value.X) / OverlayScale),
                        (int)(cropRectangleBeforeDrag.CenterY + (pt.Position.Y - dragStartPoint.Value.Y) / OverlayScale),
                        Rectangle.Rect.Width,
                        Rectangle.Rect.Height), Rectangle.Type);
                }

                // set the move cursor
                ProtectedCursor = moveCursor;
                e.Handled = true;
                return;
            }
        }

        ProtectedCursor = null;
    }

    public static double GetHeight(CropRect cropRect, double scale) =>
        cropRect.Rect.Height * scale;
    public static double GetShadowHeight(CropRect cropRect, double scale) =>
        GetHeight(cropRect, scale) + 4;

    public static double GetWidth(CropRect cropRect, double scale) =>
        cropRect.Rect.Width * scale;
    public static double GetShadowWidth(CropRect cropRect, double scale) =>
        GetWidth(cropRect, scale) + 4;

    public static double GetX(CropRect cropRect, double scale) =>
        (cropRect.Rect.CenterX - cropRect.Rect.Width / 2.0) * scale;

    public static double GetY(CropRect cropRect, double scale) =>
        (cropRect.Rect.CenterY - cropRect.Rect.Height / 2.0) * scale;

    public static Brush GetBrush(CropRect cropRect) =>
        new SolidColorBrush(cropRect.Type switch
        {
            CropRectType.KeyFrame => Colors.White,
            CropRectType.Interpolated => Colors.Aqua,
            CropRectType.FirstFrameUnfrozen => Colors.Pink,
            CropRectType.None => Colors.Transparent,
            _ => throw new NotImplementedException()
        });
}
