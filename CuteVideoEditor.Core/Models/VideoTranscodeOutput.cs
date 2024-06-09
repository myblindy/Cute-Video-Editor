using CuteVideoEditor_Video;

namespace CuteVideoEditor.Core.Models;

public class VideoTranscodeOutput
{
    public required string FileName { get; set; }
    public required OutputType OutputType { get; set; }
    public required uint Crf { get; set; }
    public required int PixelWidth { get; set; }
    public required int PixelHeight { get; set; }
    public required double FrameRateMultiplier { get; set; }
}
