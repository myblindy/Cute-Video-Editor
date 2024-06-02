namespace CuteVideoEditor.Core.Models;

public enum VideoOutputType
{
    Mp4,
    Vp8,
    Vp9
}

public class VideoTranscodeOutput
{
    public required string FileName { get; set; }
    public required VideoOutputType OutputType { get; set; }
    public required uint Crf { get; set; }
    public required int PixelWidth { get; set; }
    public required int PixelHeight { get; set; }
    public required double FrameRateMultiplier { get; set; }
}
