namespace CuteVideoEditor.Core.Models;

public readonly struct CropFrameEntryModel(int frameNumber, RectModel rect)
{
    public int FrameNumber { get; } = frameNumber;
    public RectModel Rect { get; } = rect;
}

