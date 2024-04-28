
namespace CuteVideoEditor.Core.Models;

public readonly struct SizeModel : IEquatable<SizeModel>
{
    public readonly int Width, Height;

    public SizeModel(int width, int height) =>
        (Width, Height) = (width, height);

    public override bool Equals(object? obj) => obj is SizeModel model && Equals(model);
    public bool Equals(SizeModel other) => Width == other.Width && Height == other.Height;
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(SizeModel left, SizeModel right) => left.Equals(right);
    public static bool operator !=(SizeModel left, SizeModel right) => !(left == right);
}
