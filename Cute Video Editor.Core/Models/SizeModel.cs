namespace CuteVideoEditor.Core.Models;

public readonly struct SizeModel
{
    public readonly int Width, Height;

    public SizeModel(int width, int height) =>
        (Width, Height) = (width, height);
}
