namespace CuteVideoEditor.Core.Models;

public readonly struct CropFrameEntryModel(long frameNumber, RectModel cropRectangle)
{
    public long FrameNumber { get; } = frameNumber;
    public RectModel CropRectangle { get; } = cropRectangle;
}
