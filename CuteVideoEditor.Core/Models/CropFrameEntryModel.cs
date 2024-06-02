namespace CuteVideoEditor.Core.Models;

public readonly struct CropFrameEntryModel(long frameNumber, RectModel rect)
{
    public long FrameNumber { get; } = frameNumber;
    public RectModel CropRectangle { get; } = rect;
}
