namespace CuteVideoEditor.Core.Models;

public readonly struct RectModel
{
    public readonly int CenterX, CenterY, Height, Width;
    public bool IsValid => Width > 0 && Height > 0;

    public RectModel(int centerX, int centerY, int width, int height) =>
        (CenterX, CenterY, Width, Height) = (centerX, centerY, width, height);
}
