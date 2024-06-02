
namespace CuteVideoEditor.Core.Models;

public readonly struct RectModel : IEquatable<RectModel>
{
    public readonly int CenterX, CenterY, Height, Width;
    public bool IsValid => Width > 0 && Height > 0;

    public RectModel(int centerX, int centerY, int width, int height) =>
        (CenterX, CenterY, Width, Height) = (centerX, centerY, width, height);

    public RectModel Clamp(in RectModel bounds, bool preserveAspectRatio)
    {
        if (Width > bounds.Width)
            if (preserveAspectRatio)
            {
                double aspectRatio = (double)Width / Height;
                return (new RectModel(bounds.CenterX, CenterY, bounds.Width, (int)(bounds.Width / aspectRatio))).Clamp(bounds, preserveAspectRatio);
            }
            else
                return (new RectModel(bounds.CenterX, CenterY, bounds.Width, Height)).Clamp(bounds, preserveAspectRatio);
        if (Height > bounds.Height)
            if (preserveAspectRatio)
            {
                double aspectRatio = (double)Width / Height;
                return (new RectModel(CenterX, bounds.CenterY, (int)(bounds.Height * aspectRatio), bounds.Height)).Clamp(bounds, preserveAspectRatio);
            }
            else
                return (new RectModel(CenterX, bounds.CenterY, Width, bounds.Height)).Clamp(bounds, preserveAspectRatio);
        return new(Math.Clamp(CenterX, bounds.CenterX - bounds.Width / 2 + Width / 2, bounds.CenterX + bounds.Width / 2 - Width / 2),
            Math.Clamp(CenterY, bounds.CenterY - bounds.Height / 2 + Height / 2, bounds.CenterY + bounds.Height / 2 - Height / 2),
            Math.Clamp(Width, 0, bounds.Width),
            Math.Clamp(Height, 0, bounds.Height));
    }

    public static RectModel operator /(in RectModel r, double v) => new(
        (int)(r.CenterX / v), (int)(r.CenterY / v), (int)(r.Width / v), (int)(r.Height / v));

    public static RectModel Interpolate(in RectModel r1, in RectModel r2, double v) => new(
        (int)(r1.CenterX + (r2.CenterX - r1.CenterX) * v),
        (int)(r1.CenterY + (r2.CenterY - r1.CenterY) * v),
        (int)(r1.Width + (r2.Width - r1.Width) * v),
        (int)(r1.Height + (r2.Height - r1.Height) * v));

    public static RectModel Extrapolate(in RectModel r1, in RectModel r2, int frameDistanceBetween12, int frameDistance3) => new(
        r2.CenterX + (r2.CenterX - r1.CenterX) * frameDistance3 / frameDistanceBetween12,
        r2.CenterY + (r2.CenterY - r1.CenterY) * frameDistance3 / frameDistanceBetween12,
        r2.Width + (r2.Width - r1.Width) * frameDistance3 / frameDistanceBetween12,
        r2.Height + (r2.Height - r1.Height) * frameDistance3 / frameDistanceBetween12);

    public override bool Equals(object? obj) => obj is RectModel model && Equals(model);

    public bool Equals(RectModel other) =>
        CenterX == other.CenterX && CenterY == other.CenterY && Height == other.Height && Width == other.Width;

    public override int GetHashCode() => HashCode.Combine(CenterX, CenterY, Height, Width);

    public static bool operator ==(RectModel left, RectModel right) => left.Equals(right);

    public static bool operator !=(RectModel left, RectModel right) => !(left == right);
}
