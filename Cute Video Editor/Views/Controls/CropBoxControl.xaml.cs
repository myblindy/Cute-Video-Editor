using CuteVideoEditor.Core.Models;
using CuteVideoEditor.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CuteVideoEditor.Views.Controls;

public sealed partial class CropBoxControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(nameof(ViewModel), typeof(MainViewModel), typeof(CropBoxControl), new PropertyMetadata(null));
    public MainViewModel? ViewModel
    {
        get { return (MainViewModel?)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    readonly double resizeHandleRadius;
    public CropBoxControl()
    {
        InitializeComponent();
        resizeHandleRadius = (double)Resources["ResizeHandleRadius"];
    }

    enum HitTestResult { None, Move, SizeSE }
    HitTestResult HitTest(double x, double y)
    {
        if (ViewModel is null) return HitTestResult.None;
        var x1 = GetX(ViewModel.CurrentCropRect, ViewModel.VideoOverlayScale);
        var y1 = GetY(ViewModel.CurrentCropRect, ViewModel.VideoOverlayScale);
        var x2 = x1 + GetWidth(ViewModel.CurrentCropRect, ViewModel.VideoOverlayScale);
        var y2 = y1 + GetHeight(ViewModel.CurrentCropRect, ViewModel.VideoOverlayScale);

        // bottom right handle
        if (x >= x2 - resizeHandleRadius && x <= x2 + resizeHandleRadius && y >= y2 - resizeHandleRadius && y <= y2 + resizeHandleRadius)
            return HitTestResult.SizeSE;

        // inside
        if (x >= x1 && x <= x2 && y >= y1 && y <= y2)
            return HitTestResult.Move;
        return HitTestResult.None;
    }

    HitTestResult dragType;
    Point? dragStartPoint;
    RectModel cropRectangleBeforeDrag;
    const int minDragDistance = 4;
    bool actualDragStarted;
    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (ViewModel?.MediaPlaybackState is not Windows.Media.Playback.MediaPlaybackState.Playing
            && e.Pointer.PointerDeviceType is PointerDeviceType.Mouse
            && e.GetCurrentPoint(this) is { } pt)
        {
            if (HitTest(pt.Position.X, pt.Position.Y) is { } ht
                 && ht is not HitTestResult.None)
            {
                CapturePointer(e.Pointer);
                dragStartPoint = pt.Position;
                cropRectangleBeforeDrag = ViewModel!.CurrentCropRect.Rect;
                actualDragStarted = false;
                dragType = ht;
                e.Handled = true;
            }
        }
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        if (dragStartPoint.HasValue && e.Pointer.PointerDeviceType is PointerDeviceType.Mouse)
        {
            ReleasePointerCapture(e.Pointer);
            dragStartPoint = null;
            e.Handled = true;
        }
    }

    static readonly InputCursor moveCursor = InputCursor.CreateFromCoreCursor(new(Windows.UI.Core.CoreCursorType.SizeAll, 0));
    static readonly InputCursor sizeNWSECursor = InputCursor.CreateFromCoreCursor(new(Windows.UI.Core.CoreCursorType.SizeNorthwestSoutheast, 0));
    protected override void OnPointerMoved(PointerRoutedEventArgs e)
    {
        if (ViewModel?.MediaPlaybackState is not Windows.Media.Playback.MediaPlaybackState.Playing
            && e.Pointer.PointerDeviceType is PointerDeviceType.Mouse
            && e.GetCurrentPoint(this) is { } pt)
        {
            void CheckForMinimumDragDistance()
            {
                if (dragStartPoint.HasValue && !actualDragStarted
                    && (Math.Abs(pt.Position.X - dragStartPoint.Value.X) > minDragDistance || Math.Abs(pt.Position.Y - dragStartPoint.Value.Y) > minDragDistance))
                {
                    actualDragStarted = true;
                }
            }

            RectModel ClampRectModel(RectModel rectModel, bool preserveAspectRatio) =>
                rectModel.Clamp(new RectModel(
                    ViewModel!.MediaPixelSize.Width / 2, ViewModel.MediaPixelSize.Height / 2,
                    ViewModel.MediaPixelSize.Width, ViewModel.MediaPixelSize.Height), preserveAspectRatio);

            // if cursor position is inside box
            switch (dragStartPoint.HasValue ? dragType : HitTest(pt.Position.X, pt.Position.Y))
            {
                case HitTestResult.Move:
                    CheckForMinimumDragDistance();

                    if (dragStartPoint.HasValue && actualDragStarted)
                    {
                        // update the crop rect, this will automatically materialize the frame for us if necessary
                        var currentCropRect = ViewModel!.CurrentCropRect;
                        var newRectModel = new RectModel(
                            (int)(cropRectangleBeforeDrag.CenterX + (pt.Position.X - dragStartPoint.Value.X) / ViewModel.VideoOverlayScale),
                            (int)(cropRectangleBeforeDrag.CenterY + (pt.Position.Y - dragStartPoint.Value.Y) / ViewModel.VideoOverlayScale),
                            currentCropRect.Rect.Width,
                            currentCropRect.Rect.Height);
                        newRectModel = ClampRectModel(newRectModel, true);
                        ViewModel.CurrentCropRect = new(newRectModel, currentCropRect.Type);
                    }

                    // set the move cursor
                    ProtectedCursor = moveCursor;
                    e.Handled = true;
                    return;

                case HitTestResult.SizeSE:
                    CheckForMinimumDragDistance();

                    if (dragStartPoint.HasValue && actualDragStarted)
                    {
                        var currentCropRect = ViewModel!.CurrentCropRect;

                        // if we haven't frozen the first frame, force the aspect ratio to be maintained
                        var dx = (pt.Position.X - dragStartPoint.Value.X) / ViewModel.VideoOverlayScale;
                        var dy = !ViewModel.FreezeCropSizeMode
                            ? (pt.Position.Y - dragStartPoint.Value.Y) / ViewModel.VideoOverlayScale
                            : dx / cropRectangleBeforeDrag.Width * cropRectangleBeforeDrag.Height;

                        var newRectModel = new RectModel(
                            (int)(cropRectangleBeforeDrag.CenterX + dx / 2),
                            (int)(cropRectangleBeforeDrag.CenterY + dy / 2),
                            (int)(cropRectangleBeforeDrag.Width + dx),
                            (int)(cropRectangleBeforeDrag.Height + dy));
                        newRectModel = ClampRectModel(newRectModel, ViewModel.FreezeCropSizeMode);
                        ViewModel.CurrentCropRect = new(newRectModel, currentCropRect.Type);
                    }

                    // set the resize cursor
                    ProtectedCursor = sizeNWSECursor;
                    e.Handled = true;
                    return;
            }
        }

        ProtectedCursor = null;
    }

    #region View Helpers
    public static double GetHeight(CropRectModel cropRect, double scale) =>
        cropRect.Rect.Height * scale;
    public static double GetShadowHeight(CropRectModel cropRect, double scale) =>
        GetHeight(cropRect, scale) + 4;

    public static double GetWidth(CropRectModel cropRect, double scale) =>
        cropRect.Rect.Width * scale;
    public static double GetShadowWidth(CropRectModel cropRect, double scale) =>
        GetWidth(cropRect, scale) + 4;

    public static double GetX(CropRectModel cropRect, double scale) =>
        (cropRect.Rect.CenterX - cropRect.Rect.Width / 2.0) * scale;

    public static double GetY(CropRectModel cropRect, double scale) =>
        (cropRect.Rect.CenterY - cropRect.Rect.Height / 2.0) * scale;

    public static Brush GetBrush(CropRectModel cropRect) =>
        new SolidColorBrush(cropRect.Type switch
        {
            CropRectType.KeyFrame => Colors.White,
            CropRectType.Interpolated => Colors.Aqua,
            CropRectType.FirstFrameUnfrozen => Colors.Pink,
            CropRectType.None => Colors.Transparent,
            _ => throw new NotImplementedException()
        });
    #endregion
}
