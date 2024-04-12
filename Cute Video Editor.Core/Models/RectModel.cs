
namespace CuteVideoEditor.Core.Models;

public readonly struct RectModel : IEquatable<RectModel>
{
    public readonly int CenterX, CenterY, Height, Width;
    public bool IsValid => Width > 0 && Height > 0;

    public RectModel(int centerX, int centerY, int width, int height) =>
        (CenterX, CenterY, Width, Height) = (centerX, centerY, width, height);

    public static RectModel Interpolate(RectModel r1, RectModel r2, double v) => new(
        (int)(r1.CenterX + (r2.CenterX - r1.CenterX) * v),
        (int)(r1.CenterY + (r2.CenterY - r1.CenterY) * v),
        (int)(r1.Width + (r2.Width - r1.Width) * v),
        (int)(r1.Height + (r2.Height - r1.Height) * v));

    public static RectModel Extrapolate(RectModel r1, RectModel r2, int frameDistanceBetween12, int frameDistance3) => new(
        r2.CenterX + (r2.CenterX - r1.CenterX) * frameDistance3 / frameDistanceBetween12,
        r2.CenterY + (r2.CenterY - r1.CenterY) * frameDistance3 / frameDistanceBetween12,
        r2.Width + (r2.Width - r1.Width) * frameDistance3 / frameDistanceBetween12,
        r2.Height + (r2.Height - r1.Height) * frameDistance3 / frameDistanceBetween12);

    public override bool Equals(object obj)
    {
        return obj is RectModel model && Equals(model);
    }

    public bool Equals(RectModel other)
    {
        return CenterX == other.CenterX &&
               CenterY == other.CenterY &&
               Height == other.Height &&
               Width == other.Width &&
               IsValid == other.IsValid;
    }

    public override int GetHashCode() => HashCode.Combine(CenterX, CenterY, Height, Width);

    public static bool operator ==(RectModel left, RectModel right) => left.Equals(right);

    public static bool operator !=(RectModel left, RectModel right) => !(left == right);
}
