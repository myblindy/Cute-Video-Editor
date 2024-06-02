namespace CuteVideoEditor.Core.Helpers;

public static class TimeSpanExtensions
{
    public static TimeSpan Clamp(this TimeSpan value, in TimeSpan min, in TimeSpan max) =>
        value < min ? min : value > max ? max : value;
}
