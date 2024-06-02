namespace CuteVideoEditor.Core.Models;

public enum CropRectType { None, FirstFrameUnfrozen, KeyFrame, Interpolated }
public readonly struct CropRectModel(RectModel rect, CropRectType type)
{
    public RectModel Rect { get; } = rect;
    public CropRectType Type { get; } = type;
}